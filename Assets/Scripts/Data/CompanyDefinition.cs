using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>계약 회사 (GDD 6.2, 6.3).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Company", fileName = "Company_")]
    public sealed class CompanyDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, Min(1)] int unlockLevel = 1;
        [SerializeField] List<ItemDefinition> items = new();
        [SerializeField, Min(1)] int inboundTruckCapacity = 8;
        [SerializeField, Tooltip("계약 시 생성되는 분류 선반. 항목 1개 = 선반 1개 (품목 전용)")]
        List<ItemDefinition> shelvesOnContract = new();

        public string Id => id;
        public string DisplayName => displayName;
        public int UnlockLevel => unlockLevel;
        public IReadOnlyList<ItemDefinition> Items => items;
        public int InboundTruckCapacity => inboundTruckCapacity;
        public IReadOnlyList<ItemDefinition> ShelvesOnContract => shelvesOnContract;
    }
}
