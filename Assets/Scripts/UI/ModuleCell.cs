using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 모듈 탭 미니맵의 칸 1개 (GDD 4.4). 보관 · 반품 칸만 끌 수 있고, 다른 칸에 놓으면 ModulePanel.Drop이 이동 · 교체 · 실패를 정한다.
    /// 끄는 동안은 맨 위로 올려 손가락을 따라가고, 원래 자리에는 반투명 자리 표시(ghost)가 남는다. 놓으면 패널이 다시 그려 제자리(또는 새 자리)로 간다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ModuleCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;
        [SerializeField, Range(0f, 1f), Tooltip("끄는 동안 원래 자리에 남는 표시의 불투명도")] float ghostAlpha = 0.35f;

        CanvasGroup group;
        Vector2 grabOffset;
        bool dragging;
        GameObject ghost;

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
            ghost = Instantiate(gameObject, transform.parent);
            ghost.name = name + "_Ghost";
            ghost.GetComponent<ModuleCell>().enabled = false; // 꺼진 컴포넌트는 끌기 이벤트를 받지 않는다
            var ghostGroup = ghost.GetComponent<CanvasGroup>();
            ghostGroup.alpha = ghostAlpha;
            ghostGroup.blocksRaycasts = true; // 원래 자리에 놓으면 제자리 (FindCell이 자기로 돌려줌)
            ghost.transform.SetSiblingIndex(transform.GetSiblingIndex());
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
            var target = FindCell(eventData); // 레이캐스트를 다시 켜기 전에 찾는다 (켜면 손가락 아래의 자기 자신이 잡힘)
            EndDragState();
            var panel = GetComponentInParent<ModulePanel>();
            if (panel != null) panel.Drop(this, target);
        }

        void EndDragState()
        {
            dragging = false;
            if (group != null) group.blocksRaycasts = true;
            if (ghost != null) Destroy(ghost);
            ghost = null;
        }

        static readonly System.Collections.Generic.List<RaycastResult> Hits = new();

        ModuleCell FindCell(PointerEventData eventData)
        {
            Hits.Clear();
            EventSystem.current.RaycastAll(eventData, Hits);
            foreach (var hit in Hits)
            {
                var cell = hit.gameObject.GetComponentInParent<ModuleCell>();
                if (cell == null) continue;
                if (!cell.enabled) return this; // 자리 표시 = 원래 자리
                if (cell != this) return cell;
            }
            return null;
        }
    }
}
