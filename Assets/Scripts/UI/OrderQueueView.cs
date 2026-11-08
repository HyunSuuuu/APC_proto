using System.Collections.Generic;
using APC.Orders;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// HUD 상단 오른쪽 주문서 큐 (GDD 12.2). 가장 오래된 주문이 오른쪽 끝, 새 주문은 그 왼쪽에 붙는다.
    /// 보이는 슬롯은 5개이고 넘치면 왼쪽 끝에 +n. 주문이 출발하면 카드가 사라지고 나머지가 오른쪽으로 당겨진다.
    /// </summary>
    public sealed class OrderQueueView : MonoBehaviour
    {
        [SerializeField, Tooltip("오른쪽 위 기준점 (카드 pivot도 오른쪽 위)")] RectTransform container;
        [SerializeField] OrderCardView cardTemplate;
        [SerializeField] TMP_Text overflowLabel;
        [SerializeField, Min(1)] int visibleSlots = 5;
        [SerializeField, Min(0f)] float spacing = 12f;
        [SerializeField, Min(0.05f)] float slideDuration = 0.25f;

        readonly List<OrderCardView> cards = new(); // 오래된 순 = 오른쪽부터

        float Step => cardTemplate.Rect.sizeDelta.x + spacing;

        void Start()
        {
            cardTemplate.gameObject.SetActive(false);
            overflowLabel.gameObject.SetActive(false);

            var service = OrderService.Instance;
            foreach (var order in service.Orders) Add(order);
            service.OnAdded.Subscribe(Add).AddTo(this);
            service.OnRemoved.Subscribe(Remove).AddTo(this);
        }

        void Add(Order order)
        {
            var card = Instantiate(cardTemplate, container);
            card.Bind(order);
            card.Rect.anchoredPosition = SlotPosition(cards.Count);
            cards.Add(card);

            var visible = cards.Count <= visibleSlots;
            card.gameObject.SetActive(visible);
            if (visible) card.PlayEnter();
            RefreshOverflow();
        }

        void Remove(Order order)
        {
            var index = cards.FindIndex(c => c.Order == order);
            if (index < 0) return;
            var card = cards[index];
            cards.RemoveAt(index);

            // 사라지는 연출이 끝난 뒤 나머지를 당긴다.
            var wait = card.PlayExit();
            DOVirtual.DelayedCall(wait, Relayout).SetLink(gameObject);
        }

        void Relayout()
        {
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var visible = i < visibleSlots;
                var target = SlotPosition(i);

                if (visible && !card.gameObject.activeSelf)
                {
                    // 넘쳐서 숨어 있던 카드가 자리로 들어온다.
                    card.gameObject.SetActive(true);
                    card.Rect.anchoredPosition = target;
                    card.PlayEnter();
                    continue;
                }

                card.gameObject.SetActive(visible);
                if (!visible) continue;
                card.Rect.DOKill(true); // 등장 연출(OutBack)을 중간에 끊으면 크기가 1보다 큰 채로 남으므로 끝까지 완료시킨다
                card.Rect.DOAnchorPos(target, slideDuration).SetEase(Ease.OutCubic).SetLink(card.gameObject);
            }
            RefreshOverflow();
        }

        void RefreshOverflow()
        {
            var hidden = cards.Count - visibleSlots;
            overflowLabel.gameObject.SetActive(hidden > 0);
            if (hidden <= 0) return;
            overflowLabel.text = $"+{hidden}";
            var rect = overflowLabel.rectTransform;
            rect.anchoredPosition = SlotPosition(visibleSlots) + new Vector2(0f, -cardTemplate.Rect.sizeDelta.y * 0.5f + rect.sizeDelta.y * 0.5f);
        }

        Vector2 SlotPosition(int index) => new(-index * Step, 0f);
    }
}
