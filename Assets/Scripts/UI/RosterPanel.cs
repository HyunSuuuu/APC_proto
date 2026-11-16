using System.Collections.Generic;
using APC.Core;
using APC.Staff;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 클립보드 책상 팝업의 배치 탭 (GDD 11.3): 하차 · 보관 · 상차 · 반품 4열에 알바 카드, 열 제목에 인원 수, 오른쪽 해고 칸.
    /// 반품 열은 반품 구역을 지은 뒤에 열린다 (StaffService.IsRoleOpen). 잠긴 열에 놓으면 제자리.
    /// 카드를 다른 열로 끌면 역할 변경, 해고 칸에 놓으면 확인 창 → [해고] (고용금은 돌려받지 않음).
    /// 명단 규칙은 StaffService가 정한다.
    /// </summary>
    public sealed class RosterPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("직무 열 (하차 · 보관 · 상차 · 반품)")] RosterColumn[] columns;
        [SerializeField] RosterCard cardTemplate;
        [SerializeField, Tooltip("끄는 동안 카드를 올려 둘 곳 (레이아웃 밖, 맨 위)")] RectTransform dragLayer;
        [SerializeField, Tooltip("알바 n/최대")] TMP_Text headerLabel;

        [Header("해고 확인")]
        [SerializeField] GameObject confirm;
        [SerializeField] TMP_Text confirmLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;

        readonly List<RosterCard> cards = new();
        StaffWorker pendingFire;

        public RectTransform DragLayer => dragLayer;

        void Awake()
        {
            cardTemplate.gameObject.SetActive(false);
            confirm.SetActive(false);
            confirmButton.onClick.AddListener(ConfirmFire);
            cancelButton.onClick.AddListener(CloseConfirm);
        }

        void Start()
        {
            if (StaffService.Instance != null) StaffService.Instance.OnWorkersChanged.Subscribe(_ => Refresh()).AddTo(this);
        }

        void OnEnable() => Refresh();

        void OnDisable() => CloseConfirm();

        /// <summary>카드를 놓았다. 다른 역할 열이면 역할 변경, 해고 칸이면 확인 창. 어디든 카드는 제자리(역할 열)로 돌아간다.</summary>
        internal void Drop(RosterCard card, RosterColumn column)
        {
            var worker = card.Worker;
            var staff = StaffService.Instance;
            if (column != null && worker != null && staff != null)
            {
                if (column.Fire) AskFire(worker);
                else if (staff.IsRoleOpen(column.Role)) staff.SetRole(worker, column.Role);
            }
            Refresh();
        }

        void AskFire(StaffWorker worker)
        {
            pendingFire = worker;
            confirmLabel.text = $"{worker.DisplayName}을(를) 해고할까요?\n<size=75%>고용금은 돌려받지 않습니다</size>";
            confirm.SetActive(true);
        }

        void ConfirmFire()
        {
            if (pendingFire != null && StaffService.Instance != null) StaffService.Instance.Fire(pendingFire);
            CloseConfirm();
        }

        void CloseConfirm()
        {
            pendingFire = null;
            if (confirm != null) confirm.SetActive(false);
        }

        void Refresh()
        {
            var staff = StaffService.Instance;
            var gm = GameManager.Instance;
            if (staff == null || gm == null) return;

            var workers = staff.Workers;
            headerLabel.text = $"알바 {workers.Count}/{staff.MaxStaff}   ·   카드를 끌어 직무 변경";
            while (cards.Count < workers.Count) cards.Add(Instantiate(cardTemplate, dragLayer));

            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var visible = i < workers.Count;
                card.gameObject.SetActive(visible);
                if (!visible) continue;
                var worker = workers[i];
                card.Bind(worker, gm.Database.Staff);
                card.transform.SetParent(ColumnFor(worker).Content, worldPositionStays: false);
                card.transform.SetAsLastSibling();
            }

            foreach (var column in columns) column.SetCount(staff.CountRole(column.Role), staff.IsRoleOpen(column.Role));
        }

        RosterColumn ColumnFor(StaffWorker worker)
        {
            foreach (var column in columns)
            {
                if (column.Role == worker.Role) return column;
            }
            return columns[0];
        }
    }
}
