using APC.Contracts;
using APC.Core;
using APC.Orders;
using APC.Staff;
using R3;
using UnityEngine;

namespace APC.Economy
{
    /// <summary>파트 정산 내역 (정산 팝업에 표시, GDD 9.5).</summary>
    public readonly struct SettlementResult
    {
        public SettlementResult(int part, int outboundIncome, int quotaBonus, WagePlan wagePlan)
        {
            Part = part;
            OutboundIncome = outboundIncome;
            QuotaBonus = quotaBonus;
            WagePlan = wagePlan ?? WagePlan.Empty;
            Wages = WagePlan.Paid;
        }

        public int Part { get; }

        /// <summary>출고 수입 = 이번 파트 저금통 (주운 현금).</summary>
        public int OutboundIncome { get; }

        /// <summary>할당량 보너스 (M10). 지금은 0.</summary>
        public int QuotaBonus { get; }

        /// <summary>알바 시급 — 실제로 낸 금액 (밀린 시급 포함, GDD 11.7).</summary>
        public int Wages { get; }

        /// <summary>누가 받았고 누가 미지급 · 퇴사인지 ([확인] 때 StaffService.ApplyWages).</summary>
        public WagePlan WagePlan { get; }

        public int Total => OutboundIncome + QuotaBonus - Wages;
    }

    /// <summary>
    /// 시간 흐름 · 파트 종료 정산 (GDD 9.5, 11.7). 씬에 하나 둔다 (Site/Logistics).
    /// 첫 계약 순간 1파트가 시작되고(첫 파트 90초, 이후 180초), `Time.deltaTime`으로 흐르므로 팝업 · 일시정지 중엔 멈춘다.
    /// 파트가 끝나면 정산 내역을 만들어 OnSettlementReady로 알리고(정산 팝업이 연다), [확인] 시 Confirm → 저금통을 보유 재화로 옮기고 다음 파트.
    /// </summary>
    public sealed class PartService : MonoBehaviour
    {
        public static PartService Instance { get; private set; }

        readonly ReactiveProperty<int> part = new(0);
        readonly ReactiveProperty<float> remaining = new(0f);
        readonly Subject<SettlementResult> settlementReady = new();

        SettlementResult? pending;
        int departures;

        /// <summary>현재 파트 번호 (1부터). 0이면 아직 시작 전 (계약 전).</summary>
        public ReadOnlyReactiveProperty<int> Part => part;

        /// <summary>이번 파트 남은 시간 (초).</summary>
        public ReadOnlyReactiveProperty<float> Remaining => remaining;

        /// <summary>파트가 끝나 정산 내역이 나왔다 (정산 팝업이 구독).</summary>
        public Observable<SettlementResult> OnSettlementReady => settlementReady;

        public bool IsRunning => part.Value > 0 && pending == null;

        /// <summary>직전 파트 정산액 (저금통 가득 기준용).</summary>
        public int LastSettlement { get; private set; }

        /// <summary>이번 파트에 출발한 출고 트럭 수 (할당량, M10).</summary>
        public int DeparturesThisPart => departures;

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
            // 계약하는 순간 1파트 시작 (GDD 11.7)
            ContractService.Instance.OnContracted
                .Subscribe(_ =>
                {
                    if (part.Value == 0) BeginPart(1);
                })
                .AddTo(this);

            if (OrderService.Instance != null)
                OrderService.Instance.OnRemoved.Subscribe(_ => departures++).AddTo(this);
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
            var result = new SettlementResult(part.Value, wallet.Piggy.CurrentValue, quotaBonus: 0, wages);
            pending = result;
            settlementReady.OnNext(result);
        }

        /// <summary>정산 팝업 [확인]: 저금통 + 보너스 − 시급을 보유 재화로 옮기고 다음 파트를 시작한다.</summary>
        public void Confirm()
        {
            if (pending == null) return;
            var result = pending.Value;
            pending = null;

            if (StaffService.Instance != null) StaffService.Instance.ApplyWages(result.WagePlan);
            GameManager.Instance.Wallet.SettlePiggy(result.QuotaBonus - result.Wages);
            LastSettlement = result.Total;
            BeginPart(result.Part + 1);
        }

        void BeginPart(int number)
        {
            var economy = GameManager.Instance.Database.Economy;
            departures = 0;
            CurrentPartLength = number == 1 ? economy.FirstPartLengthSeconds : economy.PartLengthSeconds;
            remaining.Value = CurrentPartLength;
            part.Value = number;
        }
    }
}
