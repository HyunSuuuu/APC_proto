using APC.Core;
using APC.Economy;
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
        [SerializeField] TMP_Text returnValue;
        [SerializeField] TMP_Text bonusValue;
        [SerializeField] TMP_Text wageValue;
        [SerializeField] TMP_Text totalValue;
        [SerializeField] Button confirmButton;
        [SerializeField, Min(0.05f)] float rowDuration = 0.35f;

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

            foreach (var text in new[] { outboundValue, returnValue, bonusValue, wageValue, totalValue }) text.text = "0";

            sequence?.Kill();
            group.alpha = 0f;
            panel.localScale = Vector3.one * 0.85f;
            sequence = DOTween.Sequence()
                .Append(group.DOFade(1f, 0.15f))
                .Join(panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .Append(Count(outboundValue, result.OutboundIncome, "+"))
                .Append(Count(returnValue, result.ReturnIncome, "+"))
                .Append(Count(bonusValue, result.QuotaBonus, "+"))
                .Append(Count(wageValue, result.Wages, "−"))
                .Append(Count(totalValue, result.Total, result.Total >= 0 ? "+" : "−"))
                .Join(totalValue.rectTransform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f))
                .OnComplete(() => confirmButton.interactable = true)
                .SetUpdate(true)
                .SetLink(gameObject);
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
