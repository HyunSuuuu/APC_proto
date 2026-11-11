using APC.Data;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 배치 탭의 칸 1개 (GDD 11.3): 역할 열(하차 · 분류 · 피킹) 또는 해고 칸. 알바 카드(RosterCard)를 끌어다 놓는 곳.
    /// 칸 배경(Image)이 레이캐스트를 받아야 한다.
    /// </summary>
    public sealed class RosterColumn : MonoBehaviour
    {
        [SerializeField, Tooltip("켜면 해고 칸")] bool fire;
        [SerializeField] StaffRole role;
        [SerializeField, Tooltip("열 제목 (`하차 (2)`), 해고 칸은 비워 둔다")] TMP_Text titleLabel;
        [SerializeField] string displayName;
        [SerializeField, Tooltip("알바 카드가 들어가는 곳 (세로 레이아웃)")] RectTransform content;

        public bool Fire => fire;
        public StaffRole Role => role;
        public RectTransform Content => content;

        public void SetCount(int count)
        {
            if (titleLabel != null) titleLabel.text = $"{displayName} ({count})";
        }
    }
}
