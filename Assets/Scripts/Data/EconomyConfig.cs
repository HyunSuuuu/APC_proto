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

        [Header("시설 구매")]
        [SerializeField, Min(0)] int extraShelfCost = 60;
        [SerializeField, Min(0)] int inboundBay3Cost = 300;
        [SerializeField, Min(0)] int outboundBay3Cost = 300;
        [SerializeField, Min(0)] int packingTable2Cost = 400;

        [Header("업그레이드 (2순위) — 단계별 비용")]
        [SerializeField] int[] playerCarryUpgradeCosts = { 100, 200, 350 };
        [SerializeField] int[] playerSpeedUpgradeCosts = { 100, 200, 350 };
        [SerializeField] int[] shelfCapacityUpgradeCosts = { 80, 150, 250, 380, 550 };
        [SerializeField] int[] storageCapacityUpgradeCosts = { 150, 300 };

        [Header("주문 보상")]
        [SerializeField, Min(1f), Tooltip("복합 주문 = Σ(단가 × 수량) × 배율")] float compoundOrderMultiplier = 1.3f;

        [Header("경험치")]
        [SerializeField, Min(0)] int xpSingleOrder = 10;
        [SerializeField, Min(0)] int xpCompoundOrder = 20;
        [SerializeField, Min(0), Tooltip("시설 구매 · 확장 · 업그레이드 · 고용")] int xpPurchase = 5;
        [SerializeField, Min(0)] int xpReturnInspection = 2;

        public int StartingCash => startingCash;
        public float PadFillDuration => padFillDuration;
        public int ExtraShelfCost => extraShelfCost;
        public int InboundBay3Cost => inboundBay3Cost;
        public int OutboundBay3Cost => outboundBay3Cost;
        public int PackingTable2Cost => packingTable2Cost;
        public int[] PlayerCarryUpgradeCosts => playerCarryUpgradeCosts;
        public int[] PlayerSpeedUpgradeCosts => playerSpeedUpgradeCosts;
        public int[] ShelfCapacityUpgradeCosts => shelfCapacityUpgradeCosts;
        public int[] StorageCapacityUpgradeCosts => storageCapacityUpgradeCosts;
        public float CompoundOrderMultiplier => compoundOrderMultiplier;
        public int XpSingleOrder => xpSingleOrder;
        public int XpCompoundOrder => xpCompoundOrder;
        public int XpPurchase => xpPurchase;
        public int XpReturnInspection => xpReturnInspection;
    }
}
