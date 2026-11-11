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
    /// 계약 PC 팝업 (GDD 6.3, 11.1, 11.3, 12.3): `계약 | 고용 | 배치` 탭. 열려 있는 동안 게임이 멈춘다.
    /// 계약 · 고용 탭은 오른쪽 아래 도장(Stamp)을 카드에 찍어 실행한다. 배치 탭(RosterPanel)은 알바 카드를 끌어 역할 변경 · 해고.
    /// 계약 탭: 계약하면 `[ 계약 ]` 자국을 잠깐 보여 준 뒤 닫혀서 선반이 생기는 모습을 보여준다. 고용 탭(HirePanel): 고용해도 열린 채.
    /// 열 때 탭: 아직 계약이 없으면 계약, 있으면 고용.
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

        [Header("탭")]
        [SerializeField] Button contractTab;
        [SerializeField] Button hireTab;
        [SerializeField] Button rosterTab;
        [SerializeField] GameObject contractPage;
        [SerializeField] GameObject hirePage;
        [SerializeField] GameObject rosterPage;
        [SerializeField, Tooltip("도장 + 안내 글자 (배치 탭에서는 숨김)")] GameObject stampArea;
        [SerializeField] Color tabOnColor = new(0.95f, 0.6f, 0.25f);
        [SerializeField] Color tabOffColor = new(0.75f, 0.72f, 0.68f);
        [SerializeField, Tooltip("계약 도장을 찍은 뒤 닫히기까지 (초, 실시간)")] float contractCloseDelay = 0.6f;

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
            contractTab.onClick.AddListener(() => SelectTab(Tab.Contract));
            hireTab.onClick.AddListener(() => SelectTab(Tab.Hire));
            rosterTab.onClick.AddListener(() => SelectTab(Tab.Roster));
            cardTemplate.gameObject.SetActive(false);
            body.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Refresh();
            body.SetActive(true);
            SelectTab(ContractService.Instance != null && ContractService.Instance.HasAnyContract ? Tab.Hire : Tab.Contract);
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

        enum Tab { Contract, Hire, Roster }

        void SelectTab(Tab tab)
        {
            contractPage.SetActive(tab == Tab.Contract);
            hirePage.SetActive(tab == Tab.Hire);
            rosterPage.SetActive(tab == Tab.Roster);
            stampArea.SetActive(tab != Tab.Roster);
            contractTab.image.color = tab == Tab.Contract ? tabOnColor : tabOffColor;
            hireTab.image.color = tab == Tab.Hire ? tabOnColor : tabOffColor;
            rosterTab.image.color = tab == Tab.Roster ? tabOnColor : tabOffColor;
            if (tab == Tab.Contract) Refresh();
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
                var areaName = GameManager.Instance.Database.Site.GetDisplayName(company.ShelfAreaId);
                cards[i].Bind(company, service.GetBlock(company), areaName, () => Contract(company));
            }
        }

        /// <summary>계약 카드에 도장이 찍혔다. 성공하면 도장 자국을 잠깐 보여 준 뒤 닫혀서 선반이 생기는 모습을 보여 준다.</summary>
        bool Contract(CompanyDefinition company)
        {
            if (!ContractService.Instance.TryContract(company)) return false;
            DOVirtual.DelayedCall(contractCloseDelay, Close, ignoreTimeScale: true).SetLink(gameObject);
            return true;
        }
    }
}
