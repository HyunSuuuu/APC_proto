using APC.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 사무실 책상 팝업 (GDD 4.3, 4.4, 5.1, 11.1, 11.3, 12.3). 두 책상이 같이 쓰고 연 책상에 따라 탭이 다르다:
    /// A책상 = `고용 | 배치 | 스탯`(스탯 = 옛 업그레이드 탭), B책상 = `설비 | 트럭 | 모듈 | 벨트` (Open(deskB), 2026-11-18 · 벨트 2026-11-24). 열려 있는 동안 게임이 멈춘다.
    /// 모양은 클립보드 (UI 샘플 ClipboardScene 방식, 2026-11-19): 나무 판 위 종이 더미, 오른쪽 인덱스 탭(선택 탭은 앞 종이 위로),
    /// 탭을 바꾸면 종이가 흔들리고(셔플) 새 페이지가 아래에서 떠오르며 제목이 튄다.
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
        [SerializeField, Tooltip("B책상 `벨트` (컨베이어 벨트 설치, GDD 7.10)")] Button beltTab;
        [SerializeField] GameObject hirePage;
        [SerializeField] GameObject rosterPage;
        [SerializeField] GameObject upgradePage;
        [SerializeField] GameObject facilityPage;
        [SerializeField] GameObject truckPage;
        [SerializeField] GameObject beltPage;
        [SerializeField, Tooltip("벨트 탭 [설치] → 팝업 닫고 설치 상태 (BeltPlacer)")] Button beltInstallButton;
        [SerializeField, Tooltip("모듈 재배치 미니맵 (GDD 4.4). 열 때 Begin, 닫을 때 Apply")] ModulePanel modulePage;
        [SerializeField, Tooltip("도장 + 안내 글자 (고용 탭에서만 보임)")] GameObject stampArea;

        [Header("클립보드")]
        [SerializeField] RectTransform paperFront;
        [SerializeField] RectTransform paperBack1;
        [SerializeField] RectTransform paperBack2;
        [SerializeField, Tooltip("선택 안 된 탭이 들어가는 층 (앞 종이 아래)")] RectTransform tabsBehind;
        [SerializeField, Tooltip("선택된 탭이 들어가는 층 (앞 종이 위)")] RectTransform tabsFront;
        [SerializeField] TMP_Text sheetTitle;
        [SerializeField] TMP_Text sheetDesc;

        // 순서 = Tab
        static readonly string[] Titles = { "알바 고용", "알바 명부", "스탯 강화", "모듈 배치", "설비 강화", "트럭 강화", "벨트 설치" };
        static readonly string[] Descs =
        {
            "도장을 끌어 이력서 카드에 찍으면 고용됩니다.",
            "직군별로 알바 상태를 확인하고, 필요하면 해고합니다.",
            "적재 도구와 이동 속도를 올립니다.",
            "보관 · 반품 칸을 끌어 자리를 바꿉니다. 닫으면 적용됩니다.",
            "보관 모듈 선반 단계를 올립니다.",
            "하차 트럭 단계를 올립니다.",
            "물건을 옮길 곳을 벨트로 잇습니다. 1m당 ₩{0}, 철거하면 절반을 돌려받습니다.",
        };

        Sequence transition;
        Button[] tabs;
        GameObject[] pages;
        Color[] tabColors; // 씬에 칠해 둔 탭 색 = 기준 색
        float[] tabY;      // 위부터 탭 자리 (처음 배치의 hire · roster · upgrade 자리)
        float tabX;
        int current = -1;

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
            tabs = new[] { hireTab, rosterTab, upgradeTab, moduleTab, facilityTab, truckTab, beltTab };
            pages = new[] { hirePage, rosterPage, upgradePage, modulePage.gameObject, facilityPage, truckPage, beltPage };
            beltInstallButton.onClick.AddListener(() =>
            {
                Close();
                if (BeltPlacer.Instance != null) BeltPlacer.Instance.Begin();
            });
            tabColors = new Color[tabs.Length];
            for (var i = 0; i < tabs.Length; i++)
            {
                var index = (Tab)i;
                tabs[i].onClick.AddListener(() => SelectTab(index, false));
                tabColors[i] = tabs[i].image.color;
            }
            var y0 = Rect(hireTab).anchoredPosition.y;
            var step = Rect(rosterTab).anchoredPosition.y - y0;
            tabY = new[] { y0, y0 + step, y0 + step * 2f, y0 + step * 3f }; // 4번째 = B책상 `벨트`
            tabX = Rect(hireTab).anchoredPosition.x;
            body.SetActive(false);
        }

        static RectTransform Rect(Component c) => (RectTransform)c.transform;

        /// <param name="deskB">true = B책상 (`설비 | 트럭 | 모듈 | 벨트`), false = A책상 (`고용 | 배치 | 스탯`).</param>
        public void Open(bool deskB)
        {
            if (IsOpen) return;
            IsOpen = true;
            body.SetActive(true);
            modulePage.Begin();
            var shown = deskB ? new[] { facilityTab, truckTab, moduleTab, beltTab } : new[] { hireTab, rosterTab, upgradeTab };
            foreach (var tab in tabs) tab.gameObject.SetActive(System.Array.IndexOf(shown, tab) >= 0);
            for (var i = 0; i < shown.Length; i++) Rect(shown[i]).anchoredPosition = new Vector2(tabX, tabY[i]);
            current = -1;
            SelectTab(deskB ? Tab.Facility : Tab.Hire, true);
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

        enum Tab { Hire, Roster, Upgrade, Module, Facility, Truck, Belt }

        void SelectTab(Tab tab, bool instant)
        {
            var index = (int)tab;
            if (index == current)
            {
                Rect(tabs[index]).DOPunchScale(Vector3.one * 0.05f, 0.25f, 8).SetUpdate(true);
                return;
            }
            current = index;

            for (var i = 0; i < tabs.Length; i++)
            {
                pages[i].SetActive(i == index);
                SetTabLook(i, i == index, instant);
            }
            stampArea.SetActive(tab == Tab.Hire);
            sheetTitle.text = Titles[index];
            sheetDesc.text = string.Format(Descs[index], GameManager.Instance.Database.Logistics.BeltCostPerMeter);

            // 새 페이지: 아래 16에서 떠오르며 나타남
            var page = Rect(pages[index].transform);
            var fade = pages[index].TryGetComponent(out CanvasGroup g) ? g : pages[index].AddComponent<CanvasGroup>();
            page.DOKill();
            fade.DOKill();
            page.anchoredPosition = instant ? Vector2.zero : new Vector2(0f, -16f);
            fade.alpha = instant ? 1f : 0f;
            if (instant) return;
            fade.DOFade(1f, 0.22f).SetDelay(0.08f).SetUpdate(true);
            page.DOAnchorPos(Vector2.zero, 0.3f).SetDelay(0.08f).SetEase(Ease.OutCubic).SetUpdate(true);

            ShufflePaper(0.42f);
            sheetTitle.rectTransform.DOKill(true);
            sheetTitle.rectTransform.DOPunchAnchorPos(new Vector2(6f, 0f), 0.3f, 6).SetUpdate(true);
        }

        /// <summary>선택 탭은 앞 종이 위로 나오고 커지며 밝아짐, 나머지는 종이 밑으로 7 들어가고 어두워짐.</summary>
        void SetTabLook(int i, bool active, bool instant)
        {
            var rt = Rect(tabs[i]);
            rt.SetParent(active ? tabsFront : tabsBehind, true);
            var x = active ? tabX : tabX - 7f;
            var scale = active ? 1.03f : 0.97f;
            var bright = active ? 1.03f : 0.84f;
            var color = new Color(tabColors[i].r * bright, tabColors[i].g * bright, tabColors[i].b * bright, 1f);
            var image = tabs[i].image;

            rt.DOKill();
            image.DOKill();
            if (instant)
            {
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                rt.localScale = Vector3.one * scale;
                image.color = color;
                return;
            }
            rt.DOAnchorPosX(x, 0.25f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true);
            rt.DOScale(scale, 0.25f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true);
            image.DOColor(color, 0.2f).SetUpdate(true);
        }

        /// <summary>앞 종이가 들렸다 흔들리며 내려앉고, 뒤 종이 두 장이 따라 흔들림.</summary>
        void ShufflePaper(float duration)
        {
            paperFront.DOKill(true);
            DOTween.Sequence().SetTarget(paperFront).SetUpdate(true)
                .Append(paperFront.DOLocalRotate(new Vector3(0, 0, -1.2f), duration * 0.3f).SetEase(Ease.OutQuad))
                .Join(paperFront.DOAnchorPosY(8f, duration * 0.3f).SetEase(Ease.OutQuad))
                .Append(paperFront.DOLocalRotate(new Vector3(0, 0, 0.8f), duration * 0.35f).SetEase(Ease.InOutQuad))
                .Join(paperFront.DOAnchorPosY(-3f, duration * 0.35f).SetEase(Ease.InOutQuad))
                .Append(paperFront.DOLocalRotate(Vector3.zero, duration * 0.35f).SetEase(Ease.OutBack, 2f))
                .Join(paperFront.DOAnchorPosY(0f, duration * 0.35f).SetEase(Ease.OutBack, 2f));
            paperBack1.DOKill(true);
            paperBack1.DOPunchRotation(new Vector3(0, 0, 0.9f), duration, 4).SetUpdate(true);
            paperBack2.DOKill(true);
            paperBack2.DOPunchRotation(new Vector3(0, 0, -1.1f), duration, 4).SetUpdate(true);
        }
    }
}
