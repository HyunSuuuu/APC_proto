using APC.Data;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 배치 탭(알바 명부)의 직군 칸 1개 (GDD 11.3): 하차 · 보관 · 상차 · 반품 · 지게차. 둥근 상자 안에 제목 `하차 (2)` + 그 직군 알바 줄(RosterCard).
    /// 열리지 않은 직군(반품 · 지게차, 그 구역 전)은 어둡게 · `잠김`.
    /// </summary>
    public sealed class RosterColumn : MonoBehaviour
    {
        [SerializeField] StaffRole role;
        [SerializeField, Tooltip("칸 제목 (`하차 (2)`)")] TMP_Text titleLabel;
        [SerializeField] string displayName;
        [SerializeField, Tooltip("알바 줄이 들어가는 곳 (격자 레이아웃)")] RectTransform content;

        Color? baseColor;

        public StaffRole Role => role;
        public RectTransform Content => content;

        public void SetCount(int count, bool open = true)
        {
            if (titleLabel != null) titleLabel.text = open ? $"{displayName} <color=#8C6747>({count})</color>" : $"{displayName} <size=70%>(잠김)</size>";
            if (TryGetComponent<UnityEngine.UI.Graphic>(out var background))
            {
                baseColor ??= background.color;
                background.color = open ? baseColor.Value : baseColor.Value * new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
