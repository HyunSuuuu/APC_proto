using UnityEngine;

namespace APC.Data
{
    /// <summary>비용 · 보상 · 경험치 (GDD 8.5, 9.4, 10.1). 알바 관련 비용은 StaffRules에 있다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("시작")]
        [SerializeField, Min(0)] int startingCash = 0;

        [Header("구매 패드")]
        [SerializeField, Min(0.1f), Tooltip("금액과 관계없이 전액이 들어가는 시간 (초)")] float padFillDuration = 1.5f;

        [Header("업그레이드 (2순위) — 단계별 비용")]
        [SerializeField] int[] playerCarryUpgradeCosts = { 100, 200, 350 };
        [SerializeField] int[] playerSpeedUpgradeCosts = { 100, 200, 350 };
        [SerializeField] int[] shelfCapacityUpgradeCosts = { 80, 150, 250, 380, 550 };
        [SerializeField] int[] storageCapacityUpgradeCosts = { 150, 300 };

        [Header("주문 보상")]
        [SerializeField, Min(1f), Tooltip("복합 주문 = Σ(단가 × 수량) × 배율")] float compoundOrderMultiplier = 1.3f;

        [Header("현금 더미 (GDD 7.6)")]
        [SerializeField, Min(1), Tooltip("뭉치 1개의 금액. 나머지는 마지막 뭉치에 포함 (52원 = 6뭉치)")] int cashPerBundle = 10;
        [SerializeField, Min(0.1f), Tooltip("현금 존 중심에서 이 거리(m) 안에 들어오면 흡수 (멈출 필요 없음)")] float cashPickupRadius = 2f;
        [SerializeField, Min(0.01f), Tooltip("뭉치가 하나씩 빨려 들어가는 간격 (초)")] float cashAbsorbInterval = 0.05f;
        [SerializeField, Min(0.05f), Tooltip("뭉치 하나가 플레이어에게 날아가는 시간 (초)")] float cashFlyDuration = 0.3f;

        [Header("경험치")]
        [SerializeField, Min(0)] int xpSingleOrder = 10;
        [SerializeField, Min(0)] int xpCompoundOrder = 20;
        [SerializeField, Min(0), Tooltip("업그레이드 · 고용 (구매 패드)")] int xpPurchase = 5;
        [SerializeField, Min(0)] int xpReturnInspection = 2;

        public int StartingCash => startingCash;
        public float PadFillDuration => padFillDuration;
        public int[] PlayerCarryUpgradeCosts => playerCarryUpgradeCosts;
        public int[] PlayerSpeedUpgradeCosts => playerSpeedUpgradeCosts;
        public int[] ShelfCapacityUpgradeCosts => shelfCapacityUpgradeCosts;
        public int[] StorageCapacityUpgradeCosts => storageCapacityUpgradeCosts;
        public float CompoundOrderMultiplier => compoundOrderMultiplier;
        public int CashPerBundle => cashPerBundle;
        public float CashPickupRadius => cashPickupRadius;
        public float CashAbsorbInterval => cashAbsorbInterval;
        public float CashFlyDuration => cashFlyDuration;
        public int XpSingleOrder => xpSingleOrder;
        public int XpCompoundOrder => xpCompoundOrder;
        public int XpPurchase => xpPurchase;
        public int XpReturnInspection => xpReturnInspection;
    }
}
