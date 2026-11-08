using System.Collections.Generic;
using APC.Orders;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 주문서 큐의 카드 1장 (GDD 12.2): 주문 번호, 상태 배지(글자 + 색), 품목 색 아이콘 × (실은 수 / 필요 수).
    /// 큐 배치는 OrderQueueView가 한다.
    /// </summary>
    public sealed class OrderCardView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text numberLabel;
        [SerializeField] Image badge;
        [SerializeField] TMP_Text badgeLabel;
        [SerializeField] RectTransform lineRoot;
        [SerializeField, Tooltip("품목 1줄: Image(색 아이콘) + 자식 TMP(수량)")] Image lineTemplate;

        [Header("배지 색")]
        [SerializeField] Color waitingColor = new(0.55f, 0.55f, 0.58f);
        [SerializeField] Color arrivedColor = new(0.25f, 0.55f, 0.95f);
        [SerializeField] Color departedColor = new(0.3f, 0.75f, 0.4f);
        [SerializeField] Color doneLineColor = new(0.3f, 0.7f, 0.35f);
        [SerializeField] Color lineColor = new(0.25f, 0.22f, 0.2f);

        readonly List<TMP_Text> lineLabels = new();
        OrderState? lastState;

        public Order Order { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public void Bind(Order order)
        {
            Order = order;
            numberLabel.text = $"#{order.Number}";
            lineTemplate.gameObject.SetActive(false);

            foreach (var line in order.Lines)
            {
                var row = Instantiate(lineTemplate, lineRoot);
                row.gameObject.SetActive(true);
                row.color = line.Item.TapeColor;
                lineLabels.Add(row.GetComponentInChildren<TMP_Text>());
            }

            order.State.Subscribe(ApplyState).AddTo(this);
            order.Changed.Subscribe(_ => RefreshLines()).AddTo(this);
            RefreshLines();
        }

        /// <summary>새 주문: 위에서 내려오며 커진다.</summary>
        public void PlayEnter()
        {
            Rect.DOKill(true);
            Rect.localScale = Vector3.one * 0.3f;
            group.alpha = 0f;
            Rect.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
            group.DOFade(1f, 0.2f).SetLink(gameObject);
        }

        /// <summary>출발: 한 번 튄 뒤 위로 사라진다. 끝나면 스스로 지운다.</summary>
        public float PlayExit()
        {
            if (!gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return 0f;
            }

            const float duration = 0.55f;
            Rect.DOKill(true);
            DOTween.Sequence()
                .Append(Rect.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f))
                .Append(Rect.DOAnchorPosY(Rect.anchoredPosition.y + 60f, 0.25f).SetEase(Ease.InQuad))
                .Join(group.DOFade(0f, 0.25f))
                .SetLink(gameObject)
                .OnComplete(() => Destroy(gameObject));
            return duration;
        }

        void RefreshLines()
        {
            for (var i = 0; i < lineLabels.Count; i++)
            {
                var line = Order.Lines[i];
                lineLabels[i].text = $"{line.Loaded}/{line.Quantity}";
                lineLabels[i].color = line.Remaining == 0 ? doneLineColor : lineColor;
            }
        }

        void ApplyState(OrderState state)
        {
            (badgeLabel.text, badge.color) = state switch
            {
                OrderState.Waiting => ("대기", waitingColor),
                OrderState.Departed => ("출발", departedColor),
                _ => ("도착", arrivedColor),
            };

            var changedLabel = lastState.HasValue && Label(lastState.Value) != Label(state);
            lastState = state;
            if (!changedLabel) return;
            badge.rectTransform.DOKill(true);
            badge.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6, 0.5f).SetLink(gameObject);
        }

        static int Label(OrderState state) => state is OrderState.Arriving ? (int)OrderState.Docked : (int)state;
    }
}
