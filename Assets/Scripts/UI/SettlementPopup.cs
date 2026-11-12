using System.Collections.Generic;
using APC.Core;
using APC.Economy;
using APC.Staff;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 정산 팝업 (GDD 9.5, 12.3). 파트가 끝나면 열리고 게임이 멈춘다. 줄마다 금액이 차례로 올라가고,
    /// [확인]을 누르면 PartService.Confirm → 저금통이 보유 재화로 옮겨지며(HUD 카운트) 다음 파트가 시작된다.
    /// 이 오브젝트는 항상 켜 두고 자식 body만 켜고 끈다.
    /// </summary>
    public sealed class SettlementPopup : MonoBehaviour
    {
        [SerializeField] GameObject body;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform panel;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text outboundValue;
        [SerializeField] TMP_Text bonusValue;
        [SerializeField] TMP_Text wageValue;
        [SerializeField] TMP_Text totalValue;
        [SerializeField, Tooltip("미지급 · 퇴사 알바 명단 + 경고 (GDD 11.7)")] TMP_Text staffLabel;
        [SerializeField] Button confirmButton;
        [SerializeField, Min(0.05f)] float rowDuration = 0.35f;
        [SerializeField] Color totalColor = new(0.2f, 0.55f, 0.3f);
        [SerializeField, Tooltip("합계가 음수일 때 (시급이 수입보다 많음)")] Color negativeColor = new(0.8f, 0.2f, 0.15f);

        Sequence sequence;
        bool open;

        public bool IsOpen => open;

        void Awake()
        {
            confirmButton.onClick.AddListener(Confirm);
            body.SetActive(false);
        }

        void Start()
        {
            if (PartService.Instance != null) PartService.Instance.OnSettlementReady.Subscribe(Open).AddTo(this);
        }

        void Open(SettlementResult result)
        {
            if (open) return;
            open = true;
            GameManager.Instance.OpenPopup();
            body.SetActive(true);
            titleLabel.text = $"{result.Part}파트 정산";
            confirmButton.interactable = false;

            foreach (var text in new[] { outboundValue, bonusValue, wageValue, totalValue }) text.text = "0";
            staffLabel.text = StaffText(result.WagePlan);
            totalValue.color = result.Total >= 0 ? totalColor : negativeColor;

            sequence?.Kill();
            group.alpha = 0f;
            panel.localScale = Vector3.one * 0.85f;
            sequence = DOTween.Sequence()
                .Append(group.DOFade(1f, 0.15f))
                .Join(panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .Append(Count(outboundValue, result.OutboundIncome, "+"))
                .Append(Count(bonusValue, result.QuotaBonus, "+"))
                .Append(Count(wageValue, result.Wages, "−"))
                .Append(Count(totalValue, result.Total, result.Total >= 0 ? "+" : "−"))
                .Join(totalValue.rectTransform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f))
                .OnComplete(() => confirmButton.interactable = true)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary>`미지급: 알바 3, 알바 5 — 다음 정산 때 못 받으면 떠납니다` / `퇴사: 알바 2`. 둘 다 없으면 빈 글자.</summary>
        static string StaffText(WagePlan plan)
        {
            var lines = new List<string>();
            if (plan.Unpaid.Count > 0)
            {
                var names = new List<string>();
                foreach (var (worker, _) in plan.Unpaid) names.Add(worker.DisplayName);
                lines.Add($"미지급: {string.Join(", ", names)} — 다음 정산 때 못 받으면 떠납니다");
            }
            if (plan.Quitting.Count > 0)
            {
                var names = new List<string>();
                foreach (var worker in plan.Quitting) names.Add(worker.DisplayName);
                lines.Add($"퇴사: {string.Join(", ", names)}");
            }
            return string.Join("\n", lines);
        }

        Tween Count(TMP_Text text, int value, string sign)
        {
            var magnitude = Mathf.Abs(value);
            return DOVirtual.Float(0f, magnitude, magnitude == 0 ? 0.08f : rowDuration, v => text.text = $"{sign}{Mathf.RoundToInt(v):N0}")
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        void Confirm()
        {
            if (!open) return;
            open = false;
            confirmButton.interactable = false;

            sequence?.Kill();
            sequence = DOTween.Sequence()
                .Append(group.DOFade(0f, 0.15f))
                .Join(panel.DOScale(0.9f, 0.15f))
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    body.SetActive(false);
                    GameManager.Instance.ClosePopup();
                    PartService.Instance.Confirm(); // 게임이 다시 흐를 때 HUD 카운트가 보이도록 닫은 뒤에
                });
        }
    }
}
