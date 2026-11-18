using UnityEngine;

namespace APC.Data
{
    /// <summary>품목 1종 (GDD 6.1, 6.2). 색별 상자 모델로 구분한다 (M15-2). 테이프 색은 UI · 선반 색으로 남음.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Item", fileName = "Item_")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("예: A1")] string id;
        [SerializeField] string displayName;
        [SerializeField, Tooltip("품목 색 (주문 카드 글자 · 선반 · 모듈 패널). 상자 모양은 아래 모델")] Color tapeColor = Color.white;
        [SerializeField, Tooltip("색약 대응용 상자 옆면 아이콘 (없어도 됨)")] Sprite icon;
        [SerializeField, Min(0), Tooltip("주문 보상 = Σ(단가 × 수량)")] int unitPrice = 10;

        [Header("상자 모델 (M15-2)")]
        [SerializeField, Min(0.1f), Tooltip("모델 크기 배율 (일반 · 반품 · 열린 상자 같이). 색 상자 0.3m × 1.667 = 옛 상자 0.5m (2026-11-19 사용자 요청 '크게')")] float modelScale = 1.667f;
        [SerializeField, Tooltip("일반 상자 (예: Brown_Box)")] GameObject boxModel;
        [SerializeField, Tooltip("찌그러진 상자 (예: Brown_Return_Box). 비우면 일반 상자")] GameObject returnModel;
        [SerializeField, Tooltip("재포장 중 열린 상자 (예: Brown_Open_Box, M15-5)")] GameObject openModel;

        public string Id => id;
        public string DisplayName => displayName;
        public Color TapeColor => tapeColor;
        public Sprite Icon => icon;
        public int UnitPrice => unitPrice;
        public float ModelScale => modelScale;
        public GameObject BoxModel => boxModel;
        public GameObject ReturnModel => returnModel != null ? returnModel : boxModel;
        public GameObject OpenModel => openModel;
    }
}
