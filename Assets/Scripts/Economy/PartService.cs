using APC.Contracts;
using APC.Core;
using APC.Staff;
using R3;
using UnityEngine;

namespace APC.Economy
{
    /// <summary>파트 정산 내역 (정산 팝업에 표시, GDD 9.5).</summary>
    public readonly struct SettlementResult
    {
        public SettlementResult(int part, int outboundIncome, WagePlan wagePlan, int trucks = 0, int boxes = 0, int returns = 0, bool tutorial = false, int timeBonus = 0)
        {
            Part = part;
            TimeBonus = timeBonus;
            Tutorial = tutorial;
            Trucks = trucks;
            Boxes = boxes;
            Returns = returns;
            OutboundIncome = outboundIncome;
            WagePlan = wagePlan ?? WagePlan.Empty;
            Wages = WagePlan.Paid;
        }

        public int Part { get; }

        /// <summary>번 돈 = 이번 파트 저금통 (주운 돈).</summary>
        public int OutboundIncome { get; }

        /// <summary>알바 시급 — 실제로 낸 금액 (밀린 시급 포함, GDD 11.7).</summary>
        public int Wages { get; }

        /// <summary>누가 받았고 누가 미지급 · 퇴사인지 ([확인] 때 StaffService.ApplyWages).</summary>
        public WagePlan WagePlan { get; }

        /// <summary>남은 시간 보너스 (그날 1 · 2파트 합, 이미 저금통에 들어 있음 — M17-3).</summary>
        public int TimeBonus { get; }

        public int Total => OutboundIncome + TimeBonus - Wages;

        /// <summary>오늘(하루 = 1 · 2파트) 떠난 상차 트럭 수 · 실린 상자 수 · 처리한 반품 수 (영수증 표시용).</summary>
        public int Trucks { get; }

        /// <summary>튜토리얼 2파트 끝 정산 (일차에 안 셈, 영수증 `튜토리얼`).</summary>
        public bool Tutorial { get; }
        public int Boxes { get; }
        public int Returns { get; }
    }

    /// <summary>
    /// 시간 흐름 · 파트 종료 정산 (GDD 9.5, 11.7). 씬에 하나 둔다 (Site/Logistics).
    /// 게임 시작(첫 색 입고) 순간 1파트가 시작되고(파트마다 180초, 첫 파트 90초는 2026-11-15 삭제), `Time.deltaTime`으로 흐르므로 팝업 · 일시정지 중엔 멈춘다.
    /// 파트가 끝나면 정산 내역(번 돈 · 알바 월급 · 합계, 할당량 삭제)을 만들어 OnSettlementReady로 알리고(정산 팝업이 연다), [확인] 시 Confirm → 저금통을 지갑으로 옮기고 다음 파트.
    /// 파트는 **1 · 2가 반복**된다 (2026-11-19): 1 = 하차 파트(하차 트럭만 하차장에 들어옴), 2 = 상차 파트(새 상차 트럭만 옴).
    /// 튜토리얼(사무실 전, 2026-11-20): 트럭 뒷문 단계에서 시계 없는 1파트(BeginTutorialPart) → 선반에 놓으면 2파트(ShiftTutorial, 교대 배너) → 상차 · 돈 줍기 뒤 튜토리얼이 EndPart → 튜토리얼 정산 → [확인] → 진짜 1파트. 튜토리얼 정산은 일차(Day)에 안 센다.
    /// 사무실 전엔 저금통이 없어 돈이 바로 지갑으로 가므로, 그때 정산의 번 돈 = 그날 상차 트럭이 받은 돈(dayIncome).
    /// 정산은 **2파트가 끝날 때만** = 하루 정산 (2026-11-20). 1파트가 끝나면 정산 없이 바로 2파트(OnShift → 파트 교대 배너). 시급은 하루 한 파트 몫 (2026-11-20 확정).
    /// 사무실 전(파트 0)은 둘 다 열림 (튜토리얼 앞부분). 정차한 트럭은 파트가 바뀌어도 남는다.
    /// 남은 시간 보너스 (M17-3, 2026-11-20): 더 할 일이 없으면(`Logistics.WorkCheck`) 남은 시간이 3초 만에 0까지 줄고, 10초당 5씩 저금통에 들어간다 (그날 합 = 영수증 `시간 보너스`).
    /// </summary>
    public sealed class PartService : MonoBehaviour
    {
        public static PartService Instance { get; private set; }

        readonly ReactiveProperty<int> part = new(0);
        readonly ReactiveProperty<float> remaining = new(0f);
        readonly Subject<SettlementResult> settlementReady = new();
        readonly Subject<Unit> shift = new();
        readonly Subject<Unit> dayEnding = new();

        SettlementResult? pending;
        int settled; // 끝낸 하루 정산 수 (튜토리얼 제외)

        /// <summary>튜토리얼 하차 트럭 상자 수 (`임시`, 2026-11-20). 이만큼 선반에 오르면 2파트로 교대.</summary>
        public const int TutorialBoxes = 5;

        /// <summary>사무실 전 튜토리얼 구간 (파트 0 또는 튜토리얼 파트): 하차 트럭은 첫 대만 받는다.</summary>
        public static bool TutorialPhase => Instance != null && !Site.StartSequence.OfficeBuilt && (Instance.Tutorial || Instance.part.Value == 0);
        int dayTrucks, dayBoxes, dayReturns, dayIncome, dayBonus; // 하루 집계, 정산 [확인] 때 0

        // 남은 시간 보너스 (M17-3): 할 일이 없으면 남은 시간을 drainSeconds 동안 0까지 줄이며 저금통에 보너스
        bool draining;
        float drainRate, drainStart, idleCheckTimer;
        float idleSince = -1f; // '할 일 없음'이 시작된 시각 (게임 시간), 아니면 −1
        int partBonus;

        /// <summary>남은 시간을 보너스로 바꾸는 중 (시계가 빠르게 줄어듦).</summary>
        public bool BonusDraining => draining;

        /// <summary>현재 파트 번호 (1 = 하차 · 2 = 상차, 반복). 0이면 아직 시작 전 (사무실 전).</summary>
        public ReadOnlyReactiveProperty<int> Part => part;

        /// <summary>이번 파트 남은 시간 (초).</summary>
        public ReadOnlyReactiveProperty<float> Remaining => remaining;

        /// <summary>파트가 끝나 정산 내역이 나왔다 (정산 팝업이 구독).</summary>
        public Observable<SettlementResult> OnSettlementReady => settlementReady;

        /// <summary>1파트가 끝나 정산 없이 2파트로 넘어갔다 (파트 교대 배너가 구독).</summary>
        public Observable<Unit> OnShift => shift;

        /// <summary>2파트가 끝나 정산 내역을 만들기 직전 (상차장 트럭 회차 — 실은 만큼 저금통에 넣으면 이번 정산에 잡힘).</summary>
        public Observable<Unit> OnDayEnding => dayEnding;

        public bool IsRunning => part.Value > 0 && pending == null;

        /// <summary>튜토리얼 파트 중 (시계가 흐르지 않고 정산 없음).</summary>
        public bool Tutorial { get; private set; }

        /// <summary>지금 일차 = 끝낸 하루 정산 수 + 1 (튜토리얼은 세지 않음).</summary>
        public int Day => settled + 1;

        /// <summary>튜토리얼 하차 파트를 시작한다 (파트 0일 때만, 시계 없음).</summary>
        public void BeginTutorialPart()
        {
            if (part.Value != 0) return;
            Tutorial = true;
            remaining.Value = 0f;
            part.Value = InboundPart;
        }

        /// <summary>튜토리얼: 하차 파트 → 상차 파트 (파트 교대 배너).</summary>
        public void ShiftTutorial()
        {
            if (!Tutorial || part.Value != InboundPart) return;
            part.Value = OutboundPart;
            shift.OnNext(Unit.Default);
        }

        /// <summary>상차 트럭이 떠났다 (OutboundBay).</summary>
        public void CountShipment(int boxes, int reward)
        {
            dayTrucks++;
            dayBoxes += boxes;
            dayIncome += reward;
        }

        /// <summary>반품 하나를 재포장대에서 처리했다 (성공 · 실패 모두, RepackTable).</summary>
        public void CountReturn() => dayReturns++;

        /// <summary>하차 파트 번호 (1 · 2 반복 중 1).</summary>
        public const int InboundPart = 1;

        /// <summary>상차 파트 번호 (1 · 2 반복 중 2).</summary>
        public const int OutboundPart = 2;

        /// <summary>줄의 하차 트럭이 하차장에 들어가도 되는가: 사무실 전, 또는 하차 파트 진행 중.</summary>
        public static bool InboundOpen => IsOpen(InboundPart);

        /// <summary>새 상차 트럭이 들어와도 되는가: 사무실 전, 또는 상차 파트 진행 중.</summary>
        public static bool OutboundOpen => IsOpen(OutboundPart);

        static bool IsOpen(int number) => Instance == null || Instance.part.Value == 0 || (Instance.part.Value == number && Instance.IsRunning);

        /// <summary>직전 파트 정산액 (저금통 가득 기준용).</summary>
        public int LastSettlement { get; private set; }

        /// <summary>이번 파트 길이 (초). 고용한 파트의 시급 비율에 쓴다.</summary>
        public float CurrentPartLength { get; private set; }

        /// <summary>사무실 저금통이 가득 찬 것으로 보는 금액 = MAX(최소값, 직전 정산액 × 배율).</summary>
        public int PiggyFullAmount
        {
            get
            {
                var economy = GameManager.Instance.Database.Economy;
                return Mathf.Max(economy.PiggyFullMin, Mathf.RoundToInt(LastSettlement * economy.PiggyFullRatio));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[PartService] 씬에 PartService가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // 첫 회사(A사) 입고가 시작되는 순간 = 게임 시작에 1파트 시작 (GDD 11.7)
            ContractService.Instance.OnContracted
                .Subscribe(_ =>
                {
                    if ((part.Value == 0 || Tutorial) && Site.StartSequence.OfficeBuilt) BeginPart(1);
                })
                .AddTo(this);
        }

        /// <summary>사무실을 지었을 때 (StartSequence, GDD 9.6): 파트 시계가 멈춰 있었으면 1파트를 시작한다.</summary>
        public void BeginFirstPart()
        {
            if ((part.Value == 0 || Tutorial) && ContractService.Instance != null && ContractService.Instance.HasAnyContract) BeginPart(1);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            part.Dispose();
            remaining.Dispose();
            settlementReady.Dispose();
            shift.Dispose();
            dayEnding.Dispose();
        }

        void Update()
        {
            if (!IsRunning || Tutorial) return;
            var deltaTime = Time.deltaTime;
            if (!draining)
            {
                idleCheckTimer -= deltaTime;
                if (idleCheckTimer <= 0f)
                {
                    var economy = GameManager.Instance.Database.Economy;
                    idleCheckTimer = economy.IdleCheckInterval;
                    if (HasWorkLeft()) idleSince = -1f;
                    else if (idleSince < 0f) idleSince = Time.time;
                    if (idleSince >= 0f && Time.time - idleSince >= economy.IdleConfirmSeconds) StartBonusDrain(); // 잠깐의 빈틈은 무시
                }
            }
            if (draining)
            {
                remaining.Value = Mathf.Max(0f, remaining.Value - drainRate * deltaTime);
                PayTimeBonus();
            }
            else remaining.Value = Mathf.Max(0f, remaining.Value - deltaTime);
            if (remaining.Value <= 0f) EndPart();
        }

        /// <summary>이 파트에 할 일이 남았는가 (1 = 하차 · 2 = 상차, GDD 9.5 · M17-3).</summary>
        bool HasWorkLeft() => part.Value == InboundPart ? Logistics.WorkCheck.CanUnload() : Logistics.WorkCheck.CanLoad();

        /// <summary>남은 시간 보너스 시작 (디버그 'Time bonus'도 부름): 남은 시간과 관계없이 `idleBonusDrainSeconds` 만에 0.</summary>
        public void StartBonusDrain()
        {
            if (!IsRunning || Tutorial || draining || remaining.Value <= 0f) return;
            draining = true;
            drainStart = remaining.Value;
            drainRate = drainStart / GameManager.Instance.Database.Economy.IdleBonusDrainSeconds;
            partBonus = 0;
        }

        /// <summary>줄어든 시간 `idleBonusSecondsPerUnit`초마다 `idleBonusPerUnit`씩 저금통에 (모자란 나머지는 버림).</summary>
        void PayTimeBonus()
        {
            var economy = GameManager.Instance.Database.Economy;
            var units = Mathf.FloorToInt((drainStart - remaining.Value) / economy.IdleBonusSecondsPerUnit + 0.0001f);
            var target = Mathf.Min(units, Mathf.FloorToInt(drainStart / economy.IdleBonusSecondsPerUnit + 0.0001f)) * economy.IdleBonusPerUnit;
            var add = target - partBonus;
            if (add <= 0) return;
            partBonus = target;
            dayBonus += add;
            GameManager.Instance.Wallet.AddToPiggy(add); // 사무실 전엔 바로 지갑 (Wallet.DirectToCash)
        }

        /// <summary>파트를 지금 끝낸다 (디버그 '파트 즉시 종료').</summary>
        public void EndPart()
        {
            if (!IsRunning) return;
            if (Tutorial && part.Value == InboundPart)
            {
                ShiftTutorial(); // 디버그 'End Part'
                return;
            }
            if (part.Value == InboundPart)
            {
                // 하차 파트는 수익이 없어 정산 없이 교대 (시급은 하루 정산 때 한 파트 몫)
                BeginPart(OutboundPart);
                shift.OnNext(Unit.Default);
                return;
            }
            dayEnding.OnNext(Unit.Default);
            var wallet = GameManager.Instance.Wallet;
            // 시급은 정산 금액 + 보유 재화로 고용 순서대로 (GDD 11.7)
            var available = wallet.Piggy.CurrentValue + wallet.Cash.CurrentValue;
            var wages = StaffService.Instance != null ? StaffService.Instance.PlanWages(available) : WagePlan.Empty;
            var income = Site.StartSequence.OfficeBuilt ? Mathf.Max(0, wallet.Piggy.CurrentValue - dayBonus) : dayIncome; // 사무실 전엔 저금통 없음 (이미 지갑에). 보너스는 따로 한 줄
            var result = new SettlementResult(part.Value, income, wages, dayTrucks, dayBoxes, dayReturns, Tutorial, dayBonus);
            pending = result;
            settlementReady.OnNext(result);
        }

        /// <summary>정산 팝업 [확인]: 저금통 − 월급을 지갑으로 옮기고 다음 파트를 시작한다.</summary>
        public void Confirm()
        {
            if (pending == null) return;
            var result = pending.Value;
            pending = null;

            if (StaffService.Instance != null) StaffService.Instance.ApplyWages(result.WagePlan);
            GameManager.Instance.Wallet.SettlePiggy(-result.Wages);
            LastSettlement = result.Total;
            if (!result.Tutorial) settled++;
            dayTrucks = dayBoxes = dayReturns = dayIncome = dayBonus = 0;
            BeginPart(result.Part == InboundPart ? OutboundPart : InboundPart); // 1 · 2 반복 (2026-11-19)
        }

        void BeginPart(int number)
        {
            Tutorial = false;
            draining = false;
            partBonus = 0;
            idleSince = -1f;
            idleCheckTimer = GameManager.Instance.Database.Economy.IdleCheckInterval;
            CurrentPartLength = GameManager.Instance.Database.Economy.PartLengthSeconds;
            remaining.Value = CurrentPartLength;
            part.Value = number;
        }
    }
}
