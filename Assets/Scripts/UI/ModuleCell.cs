using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 모듈 탭 미니맵의 칸 1개 (GDD 4.4). 보관 · 반품 칸만 끌 수 있고, 다른 칸에 놓으면 ModulePanel.Drop이 이동 · 교체 · 실패를 정한다.
    /// 끄는 동안은 맨 위로 올려 손가락을 따라가고, 놓으면 패널이 다시 그려 제자리(또는 새 자리)로 간다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ModuleCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;

        CanvasGroup group;
        Vector2 grabOffset;
        bool dragging;

        /// <summary>미니맵 칸 (x = 열, y = 행).</summary>
        public Vector2Int Cell { get; set; }
        public bool Draggable { get; set; }
        public RectTransform Rect => (RectTransform)transform;

        void Awake() => group = GetComponent<CanvasGroup>();

        void OnDisable() => EndDragState();

        public void Show(string text, Color color, Color textColor)
        {
            label.text = text;
            label.color = textColor;
            background.color = color;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Draggable) return;
            dragging = true;
            group.blocksRaycasts = false; // 아래 칸이 레이캐스트에 잡히게
            transform.SetAsLastSibling();
            grabOffset = (Vector2)transform.position - eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging) transform.position = eventData.position + grabOffset; // Screen Space Overlay: 월드 = 화면 좌표
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            EndDragState();
            var panel = GetComponentInParent<ModulePanel>();
            if (panel != null) panel.Drop(this, FindCell(eventData));
        }

        void EndDragState()
        {
            dragging = false;
            if (group != null) group.blocksRaycasts = true;
        }

        static readonly System.Collections.Generic.List<RaycastResult> Hits = new();

        static ModuleCell FindCell(PointerEventData eventData)
        {
            Hits.Clear();
            EventSystem.current.RaycastAll(eventData, Hits);
            foreach (var hit in Hits)
            {
                var cell = hit.gameObject.GetComponentInParent<ModuleCell>();
                if (cell != null) return cell;
            }
            return null;
        }
    }
}
