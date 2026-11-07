using UnityEngine;

namespace APC.Data
{
    /// <summary>품목 1종 (GDD 6.1, 6.2). 테이프 색으로 구분한다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Item", fileName = "Item_")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("예: A1")] string id;
        [SerializeField] string displayName;
        [SerializeField] Color tapeColor = Color.white;
        [SerializeField, Tooltip("색약 대응용 상자 옆면 아이콘 (없어도 됨)")] Sprite icon;
        [SerializeField, Min(0), Tooltip("주문 보상 = Σ(단가 × 수량)")] int unitPrice = 10;

        public string Id => id;
        public string DisplayName => displayName;
        public Color TapeColor => tapeColor;
        public Sprite Icon => icon;
        public int UnitPrice => unitPrice;
    }
}
