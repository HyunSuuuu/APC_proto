using UnityEngine;

namespace APC.Data
{
    /// <summary>비용 · 보상 · 경험치 · 파트 정산 (GDD 7.6, 8.5, 9.4, 9.5, 10.1). 출고 단가는 회사(CompanyDefinition)에, 알바 비용은 StaffRules에 있다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("시작")]
        [SerializeField, Min(0), Tooltip("시작 보유 재화 (GDD 9.1)")] int startingCash = 50;

        [Header("구매 패드")]
        [SerializeField, Min(0.1f), Tooltip("금액과 관계없이 전액이 들어가는 시간 (초)")] float padFillDuration = 1.5f;

        [Header("업그레이드 (2순위) — 단계별 비용")]
        [SerializeField] int[] playerCarryUpgradeCosts = { 100, 200, 350 };
        [SerializeField] int[] playerSpeedUpgradeCosts = { 100, 200, 350 };
        [SerializeField] int[] shelfCapacityUpgradeCosts = { 80, 150, 250, 380, 550 };
        [SerializeField] int[] storageCapacityUpgradeCosts = { 150, 300 };

        [Header("현금 더미 (GDD 7.6)")]
        [SerializeField, Min(1), Tooltip("지폐 다발 1개의 금액. 나머지는 마지막 다발에 포함 (52원 = 6개)")] int cashPerBundle = 10;
        [SerializeField, Min(1), Tooltip("현금 존에 보이는 다발 최대 수 (넘으면 +금액 숫자)")] int cashMaxVisibleBundles = 40;
        [SerializeField, Min(0.1f), Tooltip("현금 존 중심에서 이 거리(m) 안에 들어오면 흡수 (멈출 필요 없음)")] float cashPickupRadius = 1.5f;
        [SerializeField, Min(0.01f), Tooltip("다발이 하나씩 빨려 들어가는 간격 (초)")] float cashAbsorbInterval = 0.05f;
        [SerializeField, Min(0.05f), Tooltip("다발 하나가 플레이어에게 날아가는 시간 (초)")] float cashFlyDuration = 0.3f;

        [Header("파트 · 정산 (GDD 9.5)")]
        [SerializeField, Min(10f), Tooltip("파트 길이 (초)")] float partLengthSeconds = 180f;
        [SerializeField, Min(10f), Tooltip("첫 파트만 짧게 (초)")] float firstPartLengthSeconds = 90f;
        [SerializeField, Min(1), Tooltip("사무실 저금통 높이 단계")] int piggyStepCount = 10;
        [SerializeField, Min(1), Tooltip("저금통 가득 기준 최소값")] int piggyFullMin = 300;
        [SerializeField, Min(1f), Tooltip("저금통 가득 기준 = MAX(최소값, 직전 파트 정산액 × 이 값)")] float piggyFullRatio = 1.2f;

        [Header("경험치 (출고 트럭 출발 순간)")]
        [SerializeField, Min(0)] int xpSingleOrder = 10;
        [SerializeField, Min(0)] int xpCompoundOrder = 20;
        [SerializeField, Min(0), Tooltip("시설 구매 · 업그레이드 · 알바 고용 (구매 패드)")] int xpPurchase = 5;
        [SerializeField, Min(0), Tooltip("반품 재포장 1개")] int xpReturnInspection = 5;

        public int StartingCash => startingCash;
        public float PadFillDuration => padFillDuration;
        public int[] PlayerCarryUpgradeCosts => playerCarryUpgradeCosts;
        public int[] PlayerSpeedUpgradeCosts => playerSpeedUpgradeCosts;
        public int[] ShelfCapacityUpgradeCosts => shelfCapacityUpgradeCosts;
        public int[] StorageCapacityUpgradeCosts => storageCapacityUpgradeCosts;
        public int CashPerBundle => cashPerBundle;
        public int CashMaxVisibleBundles => cashMaxVisibleBundles;
        public float CashPickupRadius => cashPickupRadius;
        public float CashAbsorbInterval => cashAbsorbInterval;
        public float CashFlyDuration => cashFlyDuration;        public float PartLengthSeconds => partLengthSeconds;
        public float FirstPartLengthSeconds => firstPartLengthSeconds;
        public int PiggyStepCount => piggyStepCount;
        public int PiggyFullMin => piggyFullMin;
        public float PiggyFullRatio => piggyFullRatio;
        public int XpSingleOrder => xpSingleOrder;
        public int XpCompoundOrder => xpCompoundOrder;
        public int XpPurchase => xpPurchase;
        public int XpReturnInspection => xpReturnInspection;
    }
}
