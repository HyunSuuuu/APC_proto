using DG.Tweening;
using TMPro;
using UnityEngine;

namespace APC.BrokeCash
{
    /// <summary>
    /// 깨기 연출 화면의 좌측 하단 "정산할 돈" 칸 (R&amp;D). 돈 조각이 CashFlyEffect로 아이콘에 도착할 때마다 Add로 숫자가 오르고 튄다.
    /// 다 들어오면 Finish(큰 튐), 0원이면 "…텅", 끝나면 아래 가운데 "영수증 출력" 안내가 올라온다.
    /// </summary>
    public sealed class BrokeCashHud : MonoBehaviour
    {
        [SerializeField] TMP_Text valueLabel;
        [SerializeField, Tooltip("CashFlyEffect가 날아갈 곳 (아이콘)")] RectTransform icon;
        [SerializeField, Tooltip("0원일 때 \"…텅\"")] TMP_Text emptyLabel;
        [SerializeField, Tooltip("끝나면 올라오는 \"영수증 출력\" 안내")] RectTransform receipt;
        [SerializeField] Color valueColor = new(1f, 0.83f, 0.3f);
        [SerializeField] Color flashColor = new(1f, 0.95f, 0.69f);

        int value;
        Vector2 receiptHome;
        bool cached;

        public RectTransform Icon => icon;

        void Cache()
        {
            if (cached) return;
            cached = true;
            receiptHome = receipt.anchoredPosition;
        }

        public void ResetHud()
        {
            Cache();
            DOTween.Kill(this);
            value = 0;
            valueLabel.text = "0";
            valueLabel.color = valueColor;
            valueLabel.transform.localScale = Vector3.one;
            emptyLabel.gameObject.SetActive(false);
            receipt.anchoredPosition = receiptHome + Vector2.down * 300f;
        }

        public void Add(int amount)
        {
            value += amount;
            valueLabel.text = value.ToString("N0");
            valueLabel.transform.DOKill(true);
            valueLabel.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 6, 0.5f).SetUpdate(true).SetId(this).SetLink(gameObject);
        }

        /// <summary>건너뛰기: 바로 최종 금액.</summary>
        public void SetValue(int amount)
        {
            value = amount;
            valueLabel.text = value.ToString("N0");
        }

        public void Finish()
        {
            valueLabel.transform.DOKill(true);
            valueLabel.transform.DOPunchScale(Vector3.one * 0.4f, 0.45f, 5, 0.6f).SetUpdate(true).SetId(this).SetLink(gameObject);
            valueLabel.color = flashColor;
            valueLabel.DOColor(valueColor, 0.45f).SetUpdate(true).SetId(this).SetLink(gameObject);
        }

        public void ShowEmpty()
        {
            emptyLabel.gameObject.SetActive(true);
            emptyLabel.alpha = 0f;
            DOVirtual.Float(0f, 1f, 0.3f, a => emptyLabel.alpha = a).SetUpdate(true).SetId(this).SetLink(gameObject);
        }

        public void ShowReceipt()
        {
            receipt.DOAnchorPos(receiptHome, 0.45f).SetEase(Ease.OutBack).SetUpdate(true).SetId(this).SetLink(gameObject);
        }
    }
}
