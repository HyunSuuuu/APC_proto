using R3;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// UI 하나의 세로 배치 (M21-1). 씬에 놓인 RectTransform 값 = 가로, 세로 값은 인스펙터에 넣는다.
    /// 화면 방향(<see cref="ScreenLayout"/>)이 바뀌면 가로 ↔ 세로 값을 바꿔 끼운다.
    /// 세로 값 잡는 법: Game 뷰를 세로로 두고 RectTransform을 맞춘 뒤 ⋮ 메뉴 [지금 RectTransform → 세로 값] → 씬 값은 가로로 되돌린다.
    /// 주의: 책상 팝업(ContractPopup) body 안에는 붙이지 않는다 (팝업이 자기 배치를 따로 저장 · 복원한다).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class OrientationRect : MonoBehaviour
    {
        [Header("세로 배치")]
        [SerializeField] Vector2 portraitAnchorMin = new(0.5f, 0.5f);
        [SerializeField] Vector2 portraitAnchorMax = new(0.5f, 0.5f);
        [SerializeField] Vector2 portraitPivot = new(0.5f, 0.5f);
        [SerializeField] Vector2 portraitPosition;
        [SerializeField] Vector2 portraitSize = new(100f, 100f);
        [SerializeField, Min(0f), Tooltip("세로에서 TMP 자동 크기 최소 글씨 (0 = 바꾸지 않음)")] float portraitFontSizeMin;

        RectTransform rect;
        TMP_Text text;
        Vector2 anchorMin, anchorMax, pivot, position, size;
        float fontSizeMin;

        void Awake()
        {
            rect = (RectTransform)transform;
            text = GetComponent<TMP_Text>();
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            pivot = rect.pivot;
            position = rect.anchoredPosition;
            size = rect.sizeDelta;
            if (text != null) fontSizeMin = text.fontSizeMin;
            ScreenLayout.IsPortrait.Subscribe(Apply).AddTo(this);
        }

        void Apply(bool portrait)
        {
            rect.anchorMin = portrait ? portraitAnchorMin : anchorMin;
            rect.anchorMax = portrait ? portraitAnchorMax : anchorMax;
            rect.pivot = portrait ? portraitPivot : pivot;
            rect.anchoredPosition = portrait ? portraitPosition : position;
            rect.sizeDelta = portrait ? portraitSize : size;
            if (text != null) text.fontSizeMin = portrait && portraitFontSizeMin > 0f ? portraitFontSizeMin : fontSizeMin;
        }

        // Play 중 인스펙터 값을 바꾸면 바로 반영
        void OnValidate()
        {
            if (Application.isPlaying && rect != null) Apply(ScreenLayout.Portrait);
        }

        [ContextMenu("지금 RectTransform → 세로 값")]
        void CopyCurrentToPortrait()
        {
            var r = (RectTransform)transform;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "세로 값 복사");
#endif
            portraitAnchorMin = r.anchorMin;
            portraitAnchorMax = r.anchorMax;
            portraitPivot = r.pivot;
            portraitPosition = r.anchoredPosition;
            portraitSize = r.sizeDelta;
        }
    }
}
