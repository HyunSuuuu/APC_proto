using System.Collections.Generic;
using APC.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 계약 PC 팝업 오른쪽 아래의 도장 (GDD 6.3 · 11.1). 계약 탭 · 고용 탭이 같이 쓴다.
    /// 평소엔 앞모습(Front), 들면 윗모습(Top). 카드(StampCard) 위에 놓으면 그 카드의 TryStamp를 부르고,
    /// 찍혔으면 꾹 누르듯 조금 커졌다 돌아온 뒤 제자리로 간다. 못 찍었으면 바로 돌아간다.
    /// 게임이 멈춘 동안 쓰므로 연출은 SetUpdate(true).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class Stamp : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Image image;
        [SerializeField, Tooltip("놓여 있을 때 (앞모습)")] Sprite frontSprite;
        [SerializeField, Tooltip("들고 있을 때 (윗모습)")] Sprite topSprite;
        [SerializeField, Tooltip("연출 값 (Assets/Data/StampConfig)")] StampConfig config;

        static readonly List<RaycastResult> Hits = new();

        RectTransform rect;
        CanvasGroup group;
        Vector2 home;
        Vector2 grabOffset;
        Sequence motion;

        void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            home = rect.anchoredPosition;
        }

        void OnDisable()
        {
            motion?.Kill();
            rect.anchoredPosition = home;
            rect.localScale = Vector3.one;
            group.blocksRaycasts = true;
            image.sprite = frontSprite;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            motion?.Kill();
            rect.localScale = Vector3.one;
            group.blocksRaycasts = false; // 아래 카드가 레이캐스트에 잡히게
            image.sprite = topSprite;
            grabOffset = (Vector2)rect.position - eventData.position;
        }

        public void OnDrag(PointerEventData eventData) => rect.position = eventData.position + grabOffset; // Screen Space Overlay: 월드 = 화면 좌표

        public void OnEndDrag(PointerEventData eventData)
        {
            group.blocksRaycasts = true;
            var card = FindCard(eventData);
            var stamped = card != null && card.TryStamp();

            var c = StampConfig.OrDefault(config);
            motion?.Kill();
            motion = DOTween.Sequence();
            if (stamped)
            {
                motion.Append(rect.DOScale(c.PressScale, c.PressInDuration).SetEase(Ease.OutQuad))
                    .Append(rect.DOScale(1f, c.PressOutDuration).SetEase(Ease.InQuad))
                    .AppendInterval(c.HoldDuration);
            }
            motion.AppendCallback(() => image.sprite = frontSprite)
                .Append(rect.DOAnchorPos(home, c.ReturnDuration).SetEase(c.ReturnEase))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        static StampCard FindCard(PointerEventData eventData)
        {
            Hits.Clear();
            EventSystem.current.RaycastAll(eventData, Hits);
            foreach (var hit in Hits)
            {
                var card = hit.gameObject.GetComponentInParent<StampCard>();
                if (card != null && card.isActiveAndEnabled) return card;
            }
            return null;
        }
    }
}
