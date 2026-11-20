using UnityEngine;

namespace APC.Data
{
    /// <summary>비용 · 보상 · 경험치 · 파트 정산 (GDD 7.6, 8.5, 9.4, 9.5, 10.1). 출고 단가는 회사(CompanyDefinition)에, 알바 비용은 StaffRules에 있다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("시작")]
        [SerializeField, Min(0), Tooltip("시작 보유 재화 (GDD 9.1)")] int startingCash = 50;
        [SerializeField, Tooltip("시작 순서 발판 비용 [건물, 하차 1, A보관, 상차 1, 사무실] (GDD 9.6 임시). 앞 넷 합 = 시작 보유 재화")]
        int[] startBuildCosts = { 20, 10, 10, 10, 100 };

        [Header("구매 패드")]
        [SerializeField, Min(0.1f), Tooltip("금액과 관계없이 전액이 들어가는 시간 (초)")] float padFillDuration = 1.5f;

        [Header("업그레이드 (2순위) — 단계별 비용")]
        [SerializeField] int[] playerCarryUpgradeCosts = { 100, 200, 350 };
        [SerializeField] int[] playerSpeedUpgradeCosts = { 100, 200, 350 };
        [SerializeField, Tooltip("선반 단계 2 · 3단계 비용 (보관 모듈 하나, GDD 7.3 · 9.4 임시). 단계 = 이 배열 길이 + 1")] int[] shelfStageCosts = { 150, 300 };
        [SerializeField] int[] storageCapacityUpgradeCosts = { 150, 300 };

        [Header("현금 더미 (GDD 7.6)")]
        [SerializeField, Min(1), Tooltip("지폐 다발 1개의 금액. 나머지는 마지막 다발에 포함 (52원 = 6개)")] int cashPerBundle = 10;
        [SerializeField, Min(1), Tooltip("현금 존에 보이는 다발 최대 수 (넘으면 +금액 숫자)")] int cashMaxVisibleBundles = 40;
        [SerializeField, Min(0.1f), Tooltip("황금 트럭 금 더미 흡수 반경 (m, 멈출 필요 없음). 현금은 2026-11-19부터 상차장 모듈 칸에 들어서면 줍는다 (CashZone.useModuleArea)")] float cashPickupRadius = 1.5f;
        [SerializeField, Min(0.01f), Tooltip("다발이 하나씩 빨려 들어가는 간격 (초)")] float cashAbsorbInterval = 0.05f;
        [SerializeField, Min(0.05f), Tooltip("다발 하나가 플레이어에게 날아가는 시간 (초)")] float cashFlyDuration = 0.3f;

        [Header("파트 · 정산 (GDD 9.5)")]
        [SerializeField, Min(10f), Tooltip("파트 길이 (초)")] float partLengthSeconds = 180f;
        [SerializeField, Min(1), Tooltip("사무실 저금통 지폐 높이 단계 (GDD 9.5, 3단계)")] int piggyStepCount = 3;
        [SerializeField, Min(1), Tooltip("저금통 가득 기준 최소값")] int piggyFullMin = 300;
        [SerializeField, Min(1f), Tooltip("저금통 가득 기준 = MAX(최소값, 직전 파트 정산액 × 이 값)")] float piggyFullRatio = 1.2f;

        [Header("남은 시간 보너스 (GDD 9.5, M17-3 — 더 할 일이 없으면)")]
        [SerializeField, Min(0.1f), Tooltip("남은 시간이 0까지 줄어드는 시간 (초, 남은 시간과 관계없이) `임시`")] float idleBonusDrainSeconds = 3f;
        [SerializeField, Min(1f), Tooltip("보너스 한 번에 해당하는 남은 시간 (초). 모자란 나머지는 버림")] float idleBonusSecondsPerUnit = 10f;
        [SerializeField, Min(0), Tooltip("남은 시간 idleBonusSecondsPerUnit초마다 저금통에 더하는 금액 `임시`")] int idleBonusPerUnit = 5;
        [SerializeField, Min(0.1f), Tooltip("할 일이 남았는지 확인하는 간격 (초)")] float idleCheckInterval = 0.5f;
        [SerializeField, Min(0f), Tooltip("'할 일 없음'이 이만큼(초) 이어져야 보너스 시작 — 모듈 팝 · 트럭 교대 같은 잠깐의 빈틈 무시 `임시`")] float idleConfirmSeconds = 2f;

        [Header("경험치 — 해금 · 업그레이드로만 얻는다 (GDD 10.1, 2026-11-14)")]
        [SerializeField, Min(0), Tooltip("모듈 해금 발판 · 업그레이드 1번")] int xpPurchase = 3;

        public int StartingCash => startingCash;

        /// <summary>시작 순서 발판 i번째 비용 (0 건물 · 1 하차 1 · 2 A보관 · 3 상차 1 · 4 사무실). 없으면 0.</summary>
        public int StartBuildCost(int index) => startBuildCosts != null && index < startBuildCosts.Length ? startBuildCosts[index] : 0;
        public float PadFillDuration => padFillDuration;
        public int[] PlayerCarryUpgradeCosts => playerCarryUpgradeCosts;
        public int[] PlayerSpeedUpgradeCosts => playerSpeedUpgradeCosts;
        public int[] ShelfStageCosts => shelfStageCosts;
        public int[] StorageCapacityUpgradeCosts => storageCapacityUpgradeCosts;
        public int CashPerBundle => cashPerBundle;
        public int CashMaxVisibleBundles => cashMaxVisibleBundles;
        public float CashPickupRadius => cashPickupRadius;
        public float CashAbsorbInterval => cashAbsorbInterval;
        public float CashFlyDuration => cashFlyDuration;        public float PartLengthSeconds => partLengthSeconds;
        public int PiggyStepCount => piggyStepCount;
        public int PiggyFullMin => piggyFullMin;
        public float PiggyFullRatio => piggyFullRatio;
        public float IdleBonusDrainSeconds => idleBonusDrainSeconds;
        public float IdleBonusSecondsPerUnit => idleBonusSecondsPerUnit;
        public int IdleBonusPerUnit => idleBonusPerUnit;
        public float IdleCheckInterval => idleCheckInterval;
        public float IdleConfirmSeconds => idleConfirmSeconds;
        public int XpPurchase => xpPurchase;
    }
}
