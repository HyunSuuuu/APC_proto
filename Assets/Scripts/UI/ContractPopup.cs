using System.Collections.Generic;
using APC.Contracts;
using APC.Core;
using APC.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 계약 팝업 (GDD 6.3, 12.3). 열려 있는 동안 게임이 멈춘다. 계약하면 바로 닫혀서 선반이 생기는 모습을 보여준다.
    /// 이 오브젝트는 항상 켜 두고, 자식 body만 켜고 끈다.
    /// </summary>
    public sealed class ContractPopup : MonoBehaviour
    {
        [SerializeField] GameObject body;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform panel;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform cardContainer;
        [SerializeField] ContractCard cardTemplate;

        readonly List<ContractCard> cards = new();
        Sequence transition;

        /// <summary>계약 PC(사무실 모듈 프리팹 안)가 씬의 팝업을 찾을 때 쓴다.</summary>
        public static ContractPopup Instance { get; private set; }

        public bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Awake()
        {
            Instance = this;
            closeButton.onClick.AddListener(Close);
            cardTemplate.gameObject.SetActive(false);
            body.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Refresh();
            body.SetActive(true);
            GameManager.Instance.OpenPopup();

            transition?.Kill();
            group.alpha = 0f;
            panel.localScale = Vector3.one * 0.85f;
            transition = DOTween.Sequence()
                .Append(group.DOFade(1f, 0.15f))
                .Join(panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            transition?.Kill();
            transition = DOTween.Sequence()
                .Append(group.DOFade(0f, 0.12f))
                .Join(panel.DOScale(0.9f, 0.12f))
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    body.SetActive(false);
                    GameManager.Instance.ClosePopup();
                });
        }

        void Refresh()
        {
            var companies = GameManager.Instance.Database.Companies;
            var service = ContractService.Instance;

            while (cards.Count < companies.Count)
            {
                var card = Instantiate(cardTemplate, cardContainer);
                card.gameObject.SetActive(true);
                cards.Add(card);
            }

            for (var i = 0; i < cards.Count; i++)
            {
                var visible = i < companies.Count;
                cards[i].gameObject.SetActive(visible);
                if (!visible) continue;

                var company = companies[i];
                var state = service.IsContracted(company) ? ContractCard.CardState.Contracted
                    : service.IsUnlocked(company) ? ContractCard.CardState.Available
                    : ContractCard.CardState.Locked;
                cards[i].Bind(company, state, () => Contract(company));
            }
        }

        void Contract(CompanyDefinition company)
        {
            if (ContractService.Instance.TryContract(company)) Close();
        }
    }
}
