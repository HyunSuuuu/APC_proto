using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 모듈 탭 미니맵의 칸 1개 (GDD 4.4). 보관 · 반품 칸만 끌 수 있고, 다른 칸에 놓으면 ModulePanel.Drop이 이동 · 교체 · 실패를 정한다.
    /// 끄는 동안은 맨 위로 올려 손가락을 따라가고, 원래 자리에는 반투명 자리 표시(ghost)가 남는다. 놓으면 패널이 다시 그려 제자리(또는 새 자리)로 간다.
    /// 세로 배치: 칸은 가로 스크롤 도면 안에 있다 — 못 끄는 칸을 끌면 도면 스크롤로 넘기고, 끄는 칸은 마스크 밖(패널)으로 꺼낸다.
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

        Outline ring;
        Tween ringPulse;
        Image changedDot;

        /// <summary>
        /// 칸 테두리 (시안 module_placement_ui_mobile): 종류별 얇은 테두리, 고른 칸 = 굵은 호박색, 놓을 수 있는 칸 = 깜박이는 호박색 (pulse).
        /// changed = 적용 전 자리 · 방향이 바뀐 모듈 (오른쪽 위 초록 점). width 0 = 테두리 없음.
        /// </summary>
        public void SetMarks(Color border, float width, bool pulse, bool changed)
        {
            if (ring == null)
            {
                if (!background.TryGetComponent(out ring)) ring = background.gameObject.AddComponent<Outline>(); // ?? 는 에디터 가짜 null에 걸림
                ring.useGraphicAlpha = false;
            }
            ringPulse?.Kill();
            ring.enabled = width > 0f;
            ring.effectColor = border;
            ring.effectDistance = new Vector2(width, -width);
            if (pulse)
            {
                ringPulse = DOTween.To(() => ring.effectColor.a, a => ring.effectColor = new Color(border.r, border.g, border.b, a), border.a * 0.2f, 0.65f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
            }

            if (changed && changedDot == null)
            {
                changedDot = new GameObject("Changed", typeof(RectTransform)).AddComponent<Image>();
                changedDot.transform.SetParent(transform, false);
                changedDot.sprite = background.sprite;
                changedDot.type = Image.Type.Sliced;
                changedDot.pixelsPerUnitMultiplier = 0.2f; // 거의 원
                changedDot.color = new Color(0.02f, 0.59f, 0.41f); // emerald-600
                changedDot.raycastTarget = false;
                var rect = changedDot.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.one;
                rect.anchoredPosition = new Vector2(-4f, -4f);
                rect.sizeDelta = new Vector2(30f, 30f);
            }
            if (changedDot != null) changedDot.gameObject.SetActive(changed);
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

        /// <summary>칸 크기 (세로 배치는 도면 높이에 맞춰 바뀜). 글자 칸은 좌우 8 · 위아래 10 안쪽.</summary>
        public void Resize(Vector2 size)
        {
            Rect.sizeDelta = size;
            label.rectTransform.sizeDelta = size - new Vector2(16f, 20f);
        }

        ScrollRect forwardTo; // 못 끄는 칸을 끌면 도면(ScrollRect)이 대신 받는다
        ModulePanel panel;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Draggable)
            {
                // 칸이 끌기 이벤트를 받아 버리면 부모 ScrollRect가 못 받으므로 넘겨 준다 (세로 배치 좌우 스크롤)
                forwardTo = GetComponentInParent<ScrollRect>();
                if (forwardTo != null) forwardTo.OnBeginDrag(eventData);
                return;
            }
            dragging = true;
            panel = GetComponentInParent<ModulePanel>();
            group.blocksRaycasts = false; // 아래 칸이 레이캐스트에 잡히게
            ghost = Instantiate(gameObject, transform.parent);
            ghost.name = name + "_Ghost";
            ghost.GetComponent<ModuleCell>().enabled = false; // 꺼진 컴포넌트는 끌기 이벤트를 받지 않는다
            var ghostGroup = ghost.GetComponent<CanvasGroup>();
            ghostGroup.alpha = ghostAlpha;
            ghostGroup.blocksRaycasts = true; // 원래 자리에 놓으면 제자리 (FindCell이 자기로 돌려줌)
            ghost.transform.SetSiblingIndex(transform.GetSiblingIndex());
            if (panel != null) transform.SetParent(panel.transform, true); // 도면 마스크 밖으로 — 끄는 칸이 잘리지 않게 (놓으면 패널 Render가 제자리 부모로)
            transform.SetAsLastSibling();
            grabOffset = (Vector2)transform.position - eventData.position;
            if (panel != null)
            {
                panel.SetDragPoint(eventData.position);
                panel.BeginDrag(this); // 고른 칸 + 놓을 수 있는 칸 표시
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forwardTo != null)
            {
                forwardTo.OnDrag(eventData);
                return;
            }
            if (!dragging) return;
            transform.position = eventData.position + grabOffset; // Screen Space Overlay: 월드 = 화면 좌표
            if (panel != null) panel.SetDragPoint(eventData.position); // 도면 가장자리면 자동 스크롤
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (forwardTo != null)
            {
                forwardTo.OnEndDrag(eventData);
                forwardTo = null;
                return;
            }
            if (!dragging) return;
            var target = FindCell(eventData); // 레이캐스트를 다시 켜기 전에 찾는다 (켜면 손가락 아래의 자기 자신이 잡힘)
            var owner = panel;
            EndDragState();
            if (owner != null) owner.Drop(this, target);
        }

        /// <summary>누르기만 하면(끌지 않음) ModulePanel.Select — 칸 고르기(설명 · [회전]) 또는 고른 모듈을 이 칸으로 옮기기. 끌기가 시작되면 Unity가 클릭을 보내지 않는다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            var panel = GetComponentInParent<ModulePanel>();
            if (panel != null) panel.Select(this);
        }

        void EndDragState()
        {
            dragging = false;
            forwardTo = null;
            if (panel != null) panel.SetDragPoint(null);
            panel = null;
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
