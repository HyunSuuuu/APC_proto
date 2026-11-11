using System.Collections.Generic;
using APC.Data;
using APC.Staff;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 배치 탭의 알바 카드 1장 (GDD 11.3): 역할 색 · 이름 · 스탯 · 시급.
    /// 다른 역할 열에 끌어다 놓으면 역할 변경, 해고 칸에 놓으면 해고 확인 (RosterPanel.Drop).
    /// 끄는 동안은 레이아웃에서 빠지도록 드래그 층으로 옮겨 둔다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RosterCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Image colorBar;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text statsLabel;

        static readonly List<RaycastResult> Hits = new();

        CanvasGroup group;
        Vector2 grabOffset;

        public StaffWorker Worker { get; private set; }
        public bool Dragging { get; private set; }

        void Awake() => group = GetComponent<CanvasGroup>();

        void OnDisable() => EndDragState();

        public void Bind(StaffWorker worker, StaffRules rules)
        {
            Worker = worker;
            colorBar.color = rules.GetRoleColor(worker.Role);
            nameLabel.text = worker.Owed > 0 ? $"{worker.DisplayName}  <color=#D9262B>₩{worker.Owed:N0} 밀림</color>" : worker.DisplayName;
            var stats = worker.Stats;
            statsLabel.text = $"이동 {stats.moveSpeed} · 적재 {stats.carry} · 시급 {rules.GetWage(stats.Total):N0}";
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var panel = GetComponentInParent<RosterPanel>();
            if (panel == null) return;
            Dragging = true;
            group.blocksRaycasts = false; // 아래 칸이 레이캐스트에 잡히게
            transform.SetParent(panel.DragLayer, worldPositionStays: true);
            grabOffset = (Vector2)transform.position - eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Dragging) transform.position = eventData.position + grabOffset; // Screen Space Overlay: 월드 = 화면 좌표
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Dragging) return;
            EndDragState();
            var panel = GetComponentInParent<RosterPanel>();
            if (panel != null) panel.Drop(this, FindColumn(eventData));
        }

        void EndDragState()
        {
            Dragging = false;
            if (group != null) group.blocksRaycasts = true;
        }

        static RosterColumn FindColumn(PointerEventData eventData)
        {
            Hits.Clear();
            EventSystem.current.RaycastAll(eventData, Hits);
            foreach (var hit in Hits)
            {
                var column = hit.gameObject.GetComponentInParent<RosterColumn>();
                if (column != null) return column;
            }
            return null;
        }
    }
}
