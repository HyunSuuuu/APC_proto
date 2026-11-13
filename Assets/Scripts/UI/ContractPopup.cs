using APC.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 계약 PC 팝업 (GDD 4.3, 11.1, 11.3, 12.3): `고용 | 배치` 탭. 열려 있는 동안 게임이 멈춘다.
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
        [SerializeField] GameObject hirePage;
        [SerializeField] GameObject rosterPage;
        [SerializeField, Tooltip("도장 + 안내 글자 (배치 탭에서는 숨김)")] GameObject stampArea;
        [SerializeField] Color tabOnColor = new(0.95f, 0.6f, 0.25f);
        [SerializeField] Color tabOffColor = new(0.75f, 0.72f, 0.68f);

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
            hireTab.onClick.AddListener(() => SelectTab(Tab.Hire));
            rosterTab.onClick.AddListener(() => SelectTab(Tab.Roster));
            body.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            body.SetActive(true);
            SelectTab(Tab.Hire);
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

        enum Tab { Hire, Roster }

        void SelectTab(Tab tab)
        {
            hirePage.SetActive(tab == Tab.Hire);
            rosterPage.SetActive(tab == Tab.Roster);
            stampArea.SetActive(tab != Tab.Roster);
            hireTab.image.color = tab == Tab.Hire ? tabOnColor : tabOffColor;
            rosterTab.image.color = tab == Tab.Roster ? tabOnColor : tabOffColor;
        }
    }
}
