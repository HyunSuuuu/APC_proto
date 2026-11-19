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
        public SettlementResult(int part, int outboundIncome, WagePlan wagePlan)
        {
            Part = part;
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

        public int Total => OutboundIncome - Wages;
    }

    /// <summary>
    /// 시간 흐름 · 파트 종료 정산 (GDD 9.5, 11.7). 씬에 하나 둔다 (Site/Logistics).
    /// 게임 시작(첫 색 입고) 순간 1파트가 시작되고(파트마다 180초, 첫 파트 90초는 2026-11-15 삭제), `Time.deltaTime`으로 흐르므로 팝업 · 일시정지 중엔 멈춘다.
    /// 파트가 끝나면 정산 내역(번 돈 · 알바 월급 · 합계, 할당량 삭제)을 만들어 OnSettlementReady로 알리고(정산 팝업이 연다), [확인] 시 Confirm → 저금통을 지갑으로 옮기고 다음 파트.
    /// </summary>
    public sealed class PartService : MonoBehaviour
    {
        public static PartService Instance { get; private set; }

        readonly ReactiveProperty<int> part = new(0);
        readonly ReactiveProperty<float> remaining = new(0f);
        readonly Subject<SettlementResult> settlementReady = new();

        SettlementResult? pending;

        /// <summary>현재 파트 번호 (1부터). 0이면 아직 시작 전 (계약 전).</summary>
        public ReadOnlyReactiveProperty<int> Part => part;

        /// <summary>이번 파트 남은 시간 (초).</summary>
        public ReadOnlyReactiveProperty<float> Remaining => remaining;

        /// <summary>파트가 끝나 정산 내역이 나왔다 (정산 팝업이 구독).</summary>
        public Observable<SettlementResult> OnSettlementReady => settlementReady;

        public bool IsRunning => part.Value > 0 && pending == null;

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
                    if (part.Value == 0 && Site.StartSequence.OfficeBuilt) BeginPart(1);
                })
                .AddTo(this);
        }

        /// <summary>사무실을 지었을 때 (StartSequence, GDD 9.6): 파트 시계가 멈춰 있었으면 1파트를 시작한다.</summary>
        public void BeginFirstPart()
        {
            if (part.Value == 0 && ContractService.Instance != null && ContractService.Instance.HasAnyContract) BeginPart(1);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            part.Dispose();
            remaining.Dispose();
            settlementReady.Dispose();
        }

        void Update()
        {
            if (!IsRunning) return;
            remaining.Value = Mathf.Max(0f, remaining.Value - Time.deltaTime);
            if (remaining.Value <= 0f) EndPart();
        }

        /// <summary>파트를 지금 끝낸다 (디버그 '파트 즉시 종료').</summary>
        public void EndPart()
        {
            if (!IsRunning) return;
            var wallet = GameManager.Instance.Wallet;
            // 시급은 정산 금액 + 보유 재화로 고용 순서대로 (GDD 11.7)
            var available = wallet.Piggy.CurrentValue + wallet.Cash.CurrentValue;
            var wages = StaffService.Instance != null ? StaffService.Instance.PlanWages(available) : WagePlan.Empty;
            var result = new SettlementResult(part.Value, wallet.Piggy.CurrentValue, wages);
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
            BeginPart(result.Part + 1);
        }

        void BeginPart(int number)
        {
            CurrentPartLength = GameManager.Instance.Database.Economy.PartLengthSeconds;
            remaining.Value = CurrentPartLength;
            part.Value = number;
        }
    }
}
