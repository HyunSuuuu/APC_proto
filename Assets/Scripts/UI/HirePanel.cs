using APC.Core;
using APC.Economy;
using APC.Staff;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 계약 PC 팝업의 고용 탭 (GDD 11.1). 후보 카드 3장 + 오른쪽 위 다음 교체까지 남은 시간 · [새로고침](무료).
    /// 도장(Stamp)은 팝업에 하나 있고 계약 탭과 같이 쓴다.
    /// 고용해도 팝업은 열린 채라 연달아 고용할 수 있다. 후보 · 고용 규칙은 StaffService가 정한다.
    /// </summary>
    public sealed class HirePanel : MonoBehaviour
    {
        [SerializeField] HireCard[] cards;
        [SerializeField, Tooltip("알바 n/최대 · 보유 재화")] TMP_Text headerLabel;
        [SerializeField, Tooltip("다음 후보 교체(파트 끝)까지")] TMP_Text timerLabel;
        [SerializeField] Button refreshButton;

        void Awake() => refreshButton.onClick.AddListener(() => StaffService.Instance?.RefreshCandidates());

        void Start()
        {
            if (StaffService.Instance != null) StaffService.Instance.OnCandidatesChanged.Subscribe(_ => Refresh()).AddTo(this);
            GameManager.Instance.Wallet.Cash.Subscribe(_ => Refresh()).AddTo(this);
        }

        void OnEnable() => Refresh();

        int shownSeconds = int.MinValue; // 글자가 바뀔 때만 넣는다 — 매 프레임 문자열 · TMP 메시 다시 만들기 (2026-11-25)

        void Update()
        {
            var parts = PartService.Instance;
            var seconds = parts == null || parts.Part.CurrentValue == 0 ? -1 : Mathf.CeilToInt(parts.Remaining.CurrentValue);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            timerLabel.text = seconds < 0 ? "계약 후 교체 시작" : $"다음 교체 {seconds / 60:00}:{seconds % 60:00}";
        }

        /// <summary>도장이 후보 카드에 찍혔다. 고용했으면 true.</summary>
        public bool TryHire(HireCard card)
        {
            var staff = StaffService.Instance;
            if (staff == null || card.Candidate == null) return false;
            if (staff.TryHire(card.Candidate, out var block))
            {
                card.ShowHired(); // 다시 그리기는 OnCandidatesChanged가
                return true;
            }
            card.ShowBlocked(block);
            return false;
        }

        void Refresh()
        {
            var staff = StaffService.Instance;
            var gm = GameManager.Instance;
            if (staff == null || gm == null) return;

            headerLabel.text = $"알바 {staff.Workers.Count}/{staff.MaxStaff}   ·   보유 재화 {gm.Wallet.Cash.CurrentValue:N0}";
            var cash = gm.Wallet.Cash.CurrentValue;
            for (var i = 0; i < cards.Length; i++)
            {
                var visible = i < staff.Candidates.Count;
                cards[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var candidate = staff.Candidates[i];
                cards[i].Bind(i + 1, candidate, gm.Database.Staff, cash >= staff.GetHireCost(candidate));
            }
        }
    }
}
