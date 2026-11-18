using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>회사 (GDD 6.2, 6.3). 회사 1곳 = 품목 1종. 입고 시작 레벨이 되면 자동으로 들어온다 (계약 없음).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Company", fileName = "Company_")]
    public sealed class CompanyDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, Min(1), Tooltip("입고 시작 레벨. 이 레벨이 되면 입고 · 주문 가중치에 자동으로 들어간다")] int unlockLevel = 1;
        [SerializeField, Min(0), Tooltip("입고 트럭 · 주문 회사를 고르는 가중치 (GDD 6.2). 입고 중인 회사끼리 비율로 나눈다")] int weight = 100;
        [SerializeField, Tooltip("대형 화물 회사 (지게차 전용, GDD 5.1 · M11-8). 지금은 표시만")] bool isLarge;
        [SerializeField, Tooltip("이 회사 품목 (1종)")] List<ItemDefinition> items = new();
        [SerializeField, Min(1), Tooltip("하차 트럭 1대 상자 수 (1단계, GDD 6.3)")] int inboundTruckCapacity = 8;
        [SerializeField, Min(1), Tooltip("하차 트럭 2단계 상자 수 (LogisticsConfig.truckStage2Level부터, GDD 7.1). 주황 = 1단계와 같게")] int inboundTruckCapacityStage2 = 15;
        [SerializeField, Min(0), Tooltip("단가: 출고 상자 1개당 현금 (GDD 6.2, 8.5). 출발 순간 실린 상자 수 × 이 값")] int contractUnitPrice = 10;
        [SerializeField, Tooltip("이 회사 선반이 생기는 선반 구역 = SiteLayout 배치 id (예: StoreA1 = A보관, StoreB = B보관). 거기 자리가 없으면 아무 빈 자리. 비우면 아무 빈 자리 (M11-5 선반 모듈 전 임시)")]
        string shelfAreaId;

        public string Id => id;
        public string DisplayName => displayName;
        public int UnlockLevel => unlockLevel;
        public int Weight => weight;
        public bool IsLarge => isLarge;
        public IReadOnlyList<ItemDefinition> Items => items;
        public int InboundTruckCapacity => inboundTruckCapacity;
        public int InboundTruckCapacityStage2 => inboundTruckCapacityStage2;
        public int ContractUnitPrice => contractUnitPrice;
        public string ShelfAreaId => shelfAreaId;
    }
}
