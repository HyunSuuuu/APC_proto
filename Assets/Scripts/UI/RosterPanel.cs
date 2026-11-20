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
    /// 클립보드 책상 팝업의 배치 탭 = 알바 명부 (GDD 11.3, UI 샘플 ClipboardScene 직원 명부 방식 2026-11-19).
    /// 하차 · 보관 · 상차 · 반품 · 지게차 직군 칸(둥근 상자, 제목에 인원 수)에 그 직군 알바 줄(스탯 게이지 · 시급 · [해고]). 세로 스크롤.
    /// 반품 · 지게차 칸은 그 구역을 지은 뒤에 열린다 (StaffService.IsRoleOpen, 표시만).
    /// [해고] → 확인 창 → [해고] (고용금은 돌려받지 않음). 직무 변경 · 끌어서 옮기기는 없다.
    /// 명단 규칙은 StaffService가 정한다.
    /// </summary>
    public sealed class RosterPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("직군 칸 (하차 · 보관 · 상차 · 반품 · 지게차)")] RosterColumn[] columns;
        [SerializeField] RosterCard cardTemplate;
        [SerializeField, Tooltip("알바 n/최대")] TMP_Text headerLabel;

        [Header("해고 확인")]
        [SerializeField] GameObject confirm;
        [SerializeField] TMP_Text confirmLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;

        readonly List<RosterCard> cards = new();
        StaffWorker pendingFire;

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

        /// <summary>줄의 [해고]를 눌렀다.</summary>
        internal void AskFire(StaffWorker worker)
        {
            if (worker == null) return;
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
            headerLabel.text = $"알바 {workers.Count}/{staff.MaxStaff}";
            while (cards.Count < workers.Count) cards.Add(Instantiate(cardTemplate, cardTemplate.transform.parent));

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
