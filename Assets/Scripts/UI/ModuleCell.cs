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
    public sealed class ModuleCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
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

        Image truckBar;

        /// <summary>
        /// 하차장 주차장 쪽 변에 붉은 바 (M20-3). side = 칸 방향 (x = 열 +1 오른쪽, y = 행 +1 아래), null이면 숨김.
        /// 바는 칸 바깥(이웃 칸 쪽)에 붙는다 — 주차장이 나올 자리.
        /// </summary>
        public void ShowTruckSide(Vector2Int? side, Color color, float thickness)
        {
            if (side is not { } d)
            {
                if (truckBar != null) truckBar.gameObject.SetActive(false);
                return;
            }
            if (truckBar == null)
            {
                var go = new GameObject("TruckBar", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                truckBar = go.GetComponent<Image>();
                truckBar.raycastTarget = false;
            }
            truckBar.gameObject.SetActive(true);
            truckBar.color = color;
            var rect = truckBar.rectTransform;
            var anchor = new Vector2(0.5f + d.x * 0.5f, 0.5f - d.y * 0.5f); // 행 +1 = 아래
            var horizontal = d.x == 0;
            rect.anchorMin = new Vector2(horizontal ? 0f : anchor.x, horizontal ? anchor.y : 0f);
            rect.anchorMax = new Vector2(horizontal ? 1f : anchor.x, horizontal ? anchor.y : 1f);
            rect.pivot = new Vector2(0.5f - d.x * 0.5f, 0.5f + d.y * 0.5f); // 바깥으로 뻗게
            rect.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
            rect.anchoredPosition = Vector2.zero;
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

        /// <summary>누르기만 하면(끌지 않음) 회전할 칸으로 고른다 (GDD 4.4 [회전], M20-3). 끌기가 시작되면 Unity가 클릭을 보내지 않는다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            var panel = GetComponentInParent<ModulePanel>();
            if (panel != null) panel.Select(this);
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
