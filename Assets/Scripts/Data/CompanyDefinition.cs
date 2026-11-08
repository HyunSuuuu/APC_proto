using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>계약 회사 (GDD 6.2, 6.3). 계약하면 품목마다 선반이 1개씩 생긴다 (품목당 선반 1개).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Company", fileName = "Company_")]
    public sealed class CompanyDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, Min(1)] int unlockLevel = 1;
        [SerializeField] List<ItemDefinition> items = new();
        [SerializeField, Min(1)] int inboundTruckCapacity = 8;
        [SerializeField, Tooltip("이 회사 선반이 생기는 선반 구역 = SiteLayout 배치 id (예: R1C2 = 선반 구역 A, E2 = 선반 구역 B). 그 구역이 열려 있어야 계약할 수 있다. 비우면 아무 빈 자리")]
        string shelfAreaId;

        public string Id => id;
        public string DisplayName => displayName;
        public int UnlockLevel => unlockLevel;
        public IReadOnlyList<ItemDefinition> Items => items;
        public int InboundTruckCapacity => inboundTruckCapacity;
        public string ShelfAreaId => shelfAreaId;
    }
}
