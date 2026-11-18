using APC.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 사무실 책상 팝업 (GDD 4.3, 4.4, 5.1, 11.1, 11.3, 12.3). 두 책상이 같이 쓰고 연 책상에 따라 탭이 다르다:
    /// A책상 = `고용 | 배치 | 스탯`(스탯 = 옛 업그레이드 탭), B책상 = `설비 | 트럭 | 모듈` (Open(deskB), 2026-11-18). 열려 있는 동안 게임이 멈춘다.
    /// 모듈 탭(ModulePanel)의 재배치는 닫을 때 부지에 적용한다.
    /// 고용 탭(HirePanel)은 오른쪽 아래 도장(Stamp)을 후보 카드에 찍어 고용한다 (고용해도 열린 채). 배치 탭(RosterPanel)은 알바 카드를 끌어 역할 변경 · 해고.
    /// 계약 탭은 계약 시스템 삭제로 없앴다 (2026-11-13, GDD 6.3). 열면 고용 탭.
    /// 이 오브젝트는 항상 켜 두고, 자식 body만 켜고 끈다.
    /// </summary>
    public sealed class ContractPopup : MonoBehaviour
    {
        [SerializeField] GameObject body;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform panel;
        [SerializeField] Button closeButton;

        [Header("탭")]
        [SerializeField] Button hireTab;
        [SerializeField] Button rosterTab;
        [SerializeField, Tooltip("A책상 `스탯` (적재 도구 · 이동 속도)")] Button upgradeTab;
        [SerializeField] Button moduleTab;
        [SerializeField, Tooltip("B책상 `설비` (선반 단계, M16-3)")] Button facilityTab;
        [SerializeField, Tooltip("B책상 `트럭` (트럭 단계, M16-4)")] Button truckTab;
        [SerializeField] GameObject hirePage;
        [SerializeField] GameObject rosterPage;
        [SerializeField] GameObject upgradePage;
        [SerializeField] GameObject facilityPage;
        [SerializeField] GameObject truckPage;
        [SerializeField, Tooltip("모듈 재배치 미니맵 (GDD 4.4). 열 때 Begin, 닫을 때 Apply")] ModulePanel modulePage;
        [SerializeField, Tooltip("도장 + 안내 글자 (고용 탭에서만 보임)")] GameObject stampArea;
        [SerializeField] Color tabOnColor = new(0.95f, 0.6f, 0.25f);
        [SerializeField] Color tabOffColor = new(0.75f, 0.72f, 0.68f);

        Sequence transition;
        Vector2[] tabPositions; // 왼쪽부터 탭 자리 (처음 배치의 hire · roster · upgrade 자리)

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
            hireTab.onClick.AddListener(() => SelectTab(Tab.Hire));
            rosterTab.onClick.AddListener(() => SelectTab(Tab.Roster));
            upgradeTab.onClick.AddListener(() => SelectTab(Tab.Upgrade));
            moduleTab.onClick.AddListener(() => SelectTab(Tab.Module));
            facilityTab.onClick.AddListener(() => SelectTab(Tab.Facility));
            truckTab.onClick.AddListener(() => SelectTab(Tab.Truck));
            tabPositions = new[] { Rect(hireTab).anchoredPosition, Rect(rosterTab).anchoredPosition, Rect(upgradeTab).anchoredPosition };
            body.SetActive(false);
        }

        static RectTransform Rect(Button button) => (RectTransform)button.transform;

        /// <param name="deskB">true = B책상 (`설비 | 트럭 | 모듈`), false = A책상 (`고용 | 배치 | 스탯`).</param>
        public void Open(bool deskB)
        {
            if (IsOpen) return;
            IsOpen = true;
            body.SetActive(true);
            modulePage.Begin();
            var tabs = deskB ? new[] { facilityTab, truckTab, moduleTab } : new[] { hireTab, rosterTab, upgradeTab };
            foreach (var tab in new[] { hireTab, rosterTab, upgradeTab, facilityTab, truckTab, moduleTab }) tab.gameObject.SetActive(System.Array.IndexOf(tabs, tab) >= 0);
            for (var i = 0; i < tabs.Length; i++) Rect(tabs[i]).anchoredPosition = tabPositions[i];
            SelectTab(deskB ? Tab.Facility : Tab.Hire);
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
            modulePage.Apply(); // 모듈 재배치는 닫을 때 한꺼번에 (GDD 4.4)

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

        enum Tab { Hire, Roster, Upgrade, Module, Facility, Truck }

        void SelectTab(Tab tab)
        {
            hirePage.SetActive(tab == Tab.Hire);
            rosterPage.SetActive(tab == Tab.Roster);
            upgradePage.SetActive(tab == Tab.Upgrade);
            modulePage.gameObject.SetActive(tab == Tab.Module);
            facilityPage.SetActive(tab == Tab.Facility);
            truckPage.SetActive(tab == Tab.Truck);
            facilityTab.image.color = tab == Tab.Facility ? tabOnColor : tabOffColor;
            truckTab.image.color = tab == Tab.Truck ? tabOnColor : tabOffColor;
            stampArea.SetActive(tab == Tab.Hire);
            hireTab.image.color = tab == Tab.Hire ? tabOnColor : tabOffColor;
            rosterTab.image.color = tab == Tab.Roster ? tabOnColor : tabOffColor;
            upgradeTab.image.color = tab == Tab.Upgrade ? tabOnColor : tabOffColor;
            moduleTab.image.color = tab == Tab.Module ? tabOnColor : tabOffColor;
        }
    }
}
