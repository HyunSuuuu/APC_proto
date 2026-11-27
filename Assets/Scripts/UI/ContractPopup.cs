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
    /// 모듈 탭(ModulePanel)의 재배치는 [적용]으로 부지에 반영하고, 적용하지 않고 닫으면 확인창을 띄운다.
    /// 고용 탭(HirePanel)은 오른쪽 아래 도장(Stamp)을 후보 카드에 찍어 고용한다 (고용해도 열린 채). 배치 탭(RosterPanel)은 알바 카드를 끌어 역할 변경 · 해고.
    /// 계약 탭은 계약 시스템 삭제로 없앴다 (2026-11-13, GDD 6.3). 열면 고용 탭.
    /// 이 오브젝트는 항상 켜 두고, 자식 body만 켜고 끈다.
    /// 화면은 가로(`Body`) · 세로(`BodyPortrait`) 두 벌(ContractPopupView)을 씬에 따로 만들고, 방향에 맞는 한 벌만 켠다 (ContractPopup.Portrait.cs, 2026-11-27).
    /// </summary>
    public sealed partial class ContractPopup : MonoBehaviour
    {
        [SerializeField, Tooltip("가로 화면 한 벌 (판 · 탭 · 페이지, 2026-11-27). 화면 참조는 모두 여기에")] ContractPopupView landscapeView;
        [SerializeField, Tooltip("세로 화면 한 벌 (모양은 씬에서 따로). 비우면 세로에서도 가로 화면을 그대로 쓴다")] ContractPopupView portraitView;
        [SerializeField, Min(0.5f), Tooltip("닫기(X) 버튼 배율 (씬 크기 × 이 값, 2026-11-26 1.5배 → 다시 × 1.25)")] float closeButtonScale = 1.875f;

        // 지금 쓰는 화면 한 벌. 아래 이름들은 예전 필드 이름 그대로 (세로 배치 코드가 같이 씀)
        ContractPopupView view;
        GameObject body => view.gameObject;
        CanvasGroup group => view.Group;
        RectTransform panel => view.Panel;
        Button closeButton => view.CloseButton;
        Button hireTab => view.HireTab;
        Button rosterTab => view.RosterTab;
        Button upgradeTab => view.UpgradeTab;
        Button moduleTab => view.ModuleTab;
        Button facilityTab => view.FacilityTab;
        Button truckTab => view.TruckTab;
        Button beltTab => view.BeltTab;
        GameObject hirePage => view.HirePage;
        GameObject rosterPage => view.RosterPage;
        GameObject upgradePage => view.UpgradePage;
        GameObject facilityPage => view.FacilityPage;
        GameObject truckPage => view.TruckPage;
        GameObject beltPage => view.BeltPage;
        Button beltInstallButton => view.BeltInstallButton;
        ModulePanel modulePage => view.ModulePage;
        GameObject stampArea => view.StampArea;
        RectTransform paperFront => view.PaperFront;
        RectTransform paperBack1 => view.PaperBack1;
        RectTransform paperBack2 => view.PaperBack2;
        RectTransform tabsBehind => view.TabsBehind;
        RectTransform tabsFront => view.TabsFront;
        TMP_Text sheetTitle => view.SheetTitle;
        TMP_Text sheetDesc => view.SheetDesc;

        // 순서 = Tab
        static readonly string[] Titles = { "알바 고용", "알바 명부", "스탯 강화", "모듈 배치", "설비 강화", "트럭 강화", "벨트 설치" };
        static readonly string[] Descs =
        {
            "도장을 끌어 이력서 카드에 찍으면 고용됩니다.",
            "직군별로 알바 상태를 확인하고, 필요하면 해고합니다.",
            "적재 도구와 이동 속도를 올립니다.",
            "칸을 끌어 자리를 바꾸고 [적용]을 누르면 반영됩니다.",
            "보관 모듈 선반 단계를 올립니다.",
            "하차 트럭 단계를 올립니다.",
            "물건을 옮길 곳을 벨트로 잇습니다. 1m당 ₩{0}, 철거하면 절반을 돌려받습니다.",
        };

        Sequence transition;
        RectMask2D[] shuffleMasks; // 셔플 동안 끈 마스크 (명부 목록 등)
        Tween maskRestore;
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
            if (portraitView != null)
            {
                view = portraitView;
                Wire();
                body.SetActive(false);
            }
            view = landscapeView;
            Wire();
            body.SetActive(false);
        }

        /// <summary>지금 화면(view) 한 벌의 버튼을 연결하고 탭 색 · 자리를 기억한다 (Awake에서 화면마다 한 번).</summary>
        void Wire()
        {
            SetViewArrays();
            closeButton.onClick.AddListener(Close);
            beltInstallButton.onClick.AddListener(() => CloseThen(() =>
            {
                if (BeltPlacer.Instance != null) BeltPlacer.Instance.Begin();
            }));
            view.TabColors = new Color[tabs.Length];
            for (var i = 0; i < tabs.Length; i++)
            {
                var index = (Tab)i;
                tabs[i].onClick.AddListener(() => SelectTab(index, false));
                view.TabColors[i] = tabs[i].image.color;
            }
            var y0 = Rect(hireTab).anchoredPosition.y;
            var step = Rect(rosterTab).anchoredPosition.y - y0;
            view.TabY = new[] { y0, y0 + step, y0 + step * 2f, y0 + step * 3f }; // 4번째 = B책상 `벨트`
            view.TabX = Rect(hireTab).anchoredPosition.x;
            modulePage.Build(); // 모듈 탭이 만드는 버튼 · 세로 UI
            Rect(closeButton).localScale *= closeButtonScale;
            SetViewArrays();
        }

        void SetViewArrays()
        {
            tabs = new[] { hireTab, rosterTab, upgradeTab, moduleTab, facilityTab, truckTab, beltTab };
            pages = new[] { hirePage, rosterPage, upgradePage, modulePage.gameObject, facilityPage, truckPage, beltPage };
            tabColors = view.TabColors;
            tabY = view.TabY;
            tabX = view.TabX;
        }

        int lastScreenW, lastScreenH;

        // 열려 있는 동안 화면이 돌거나 해상도가 바뀌면 가로 · 세로 화면을 바꿔 켜거나(같은 책상 · 같은 탭) 탭 · 모듈 배치를 다시 잡는다.
        // LateUpdate = HUD CanvasScaler(OrientationCanvasScaler가 기준 해상도를 바꿈)가 Update에서 크기를 다시 잡은 뒤
        void LateUpdate()
        {
            if (!IsOpen) return;
            if (portraitView != null && IsPortrait() != portrait)
            {
                lastScreenW = Screen.width;
                lastScreenH = Screen.height;
                var previous = view;
                SetView(IsPortrait());
                previous.gameObject.SetActive(false);
                ShowView(current);
                return;
            }
            if (Screen.width != lastScreenW || Screen.height != lastScreenH)
            {
                lastScreenW = Screen.width;
                lastScreenH = Screen.height;
                Canvas.ForceUpdateCanvases();
                LayoutModules();
                PlaceTabs();
            }
        }

        static RectTransform Rect(Component c) => (RectTransform)c.transform;

        /// <param name="deskB">true = B책상 (`설비 | 트럭 | 모듈 | 벨트`), false = A책상 (`고용 | 배치 | 스탯`).</param>
        public void Open(bool deskB)
        {
            if (IsOpen) return;
            IsOpen = true;
            SetView(IsPortrait());
            openDeskB = deskB;
            ShowView((int)(deskB ? Tab.Facility : Tab.Hire));
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

        bool openDeskB;

        /// <summary>지금 화면을 켜고 연 책상의 탭만 놓은 뒤 tab 페이지를 바로 보인다 (열 때 · 열린 채 화면을 바꿀 때).</summary>
        void ShowView(int tab)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            body.SetActive(true);
            group.alpha = 1f;
            panel.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases(); // 모듈 탭은 아직 코드 배치 — 켠 뒤에 새 캔버스 크기로 (세로 UI 분리 5단계에서 없앰)
            LayoutModules();
            modulePage.Begin();
            shownTabs = openDeskB ? new[] { facilityTab, truckTab, moduleTab, beltTab } : new[] { hireTab, rosterTab, upgradeTab };
            foreach (var t in tabs) t.gameObject.SetActive(System.Array.IndexOf(shownTabs, t) >= 0);
            PlaceTabs();
            current = -1;
            SelectTab((Tab)tab, true);
        }

        public void Close() => CloseThen(null);

        /// <summary>닫고 나서 then. 모듈 탭에 [적용]하지 않은 배치가 있으면 확인창 → [확인]이면 버리고 닫음, [취소]면 열린 채 (GDD 4.4, 2026-11-26).</summary>
        void CloseThen(System.Action then)
        {
            if (!IsOpen) return;
            if (modulePage.HasChanges)
            {
                modulePage.ConfirmDiscard(() => CloseThen(then));
                return;
            }
            IsOpen = false;
            then?.Invoke();

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

        /// <summary>
        /// 선택 탭은 앞 종이 위로 나오고 커지며 밝아짐, 나머지는 종이 밑으로 7 들어가고 어두워짐.
        /// 가로 = 오른쪽 탭이 왼쪽(종이 쪽)으로, 세로 = 아래 탭이 위쪽(종이 쪽)으로 들어간다.
        /// </summary>
        void SetTabLook(int i, bool active, bool instant)
        {
            var rt = Rect(tabs[i]);
            rt.SetParent(active ? tabsFront : tabsBehind, true);
            var x = active ? tabX : tabX - 7f;
            var y = active ? portraitTabTuck : portraitTabTuck + 7f;
            var scale = active ? 1.03f : 0.97f;
            var bright = active ? 1.03f : 0.84f;
            var color = new Color(tabColors[i].r * bright, tabColors[i].g * bright, tabColors[i].b * bright, 1f);
            var image = tabs[i].image;

            rt.DOKill();
            image.DOKill();
            if (instant)
            {
                rt.anchoredPosition = portrait ? new Vector2(0f, y) : new Vector2(x, rt.anchoredPosition.y);
                rt.localScale = Vector3.one * scale;
                image.color = color;
                return;
            }
            if (portrait) rt.DOAnchorPosY(y, 0.25f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true);
            else rt.DOAnchorPosX(x, 0.25f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true);
            rt.DOScale(scale, 0.25f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true);
            image.DOColor(color, 0.2f).SetUpdate(true);
        }

        /// <summary>
        /// 앞 종이가 들렸다 흔들리며 내려앉고, 뒤 종이 두 장이 따라 흔들림.
        /// RectMask2D는 회전을 지원하지 않아 종이가 기운 동안 목록 가장자리가 잘리므로, 셔플 동안만 앞 종이 안의 마스크를 스텐실 Mask로 바꾼다 (2026-11-26).
        /// </summary>
        void ShufflePaper(float duration)
        {
            maskRestore?.Kill(); // 연달아 바꾸면 앞 셔플의 마스크부터 되살린다
            RestoreShuffleMasks();
            shuffleMasks = paperFront.GetComponentsInChildren<RectMask2D>(false);
            foreach (var mask in shuffleMasks) SwapToStencil(mask, true);
            maskRestore = DOVirtual.DelayedCall(duration, RestoreShuffleMasks).SetUpdate(true).SetLink(gameObject);

            Shuffle(paperFront, duration);
            if (portrait) Shuffle((RectTransform)stampArea.transform, duration); // 세로: 도장 받침도 앞 종이와 같이 흔들림
            paperBack1.DOKill(true);
            paperBack1.DOPunchRotation(new Vector3(0, 0, 0.9f), duration, 4).SetUpdate(true);
            paperBack2.DOKill(true);
            paperBack2.DOPunchRotation(new Vector3(0, 0, -1.1f), duration, 4).SetUpdate(true);
        }

        void RestoreShuffleMasks()
        {
            maskRestore = null;
            if (shuffleMasks == null) return;
            foreach (var mask in shuffleMasks)
            {
                if (mask != null) SwapToStencil(mask, false);
            }
            shuffleMasks = null;
        }

        /// <summary>
        /// 셔플 동안 RectMask2D(회전 미지원) 대신 스텐실 Mask(회전 지원)로 자른다 — 끄기만 하면 스크롤 영역 바깥 내용이 잠깐 보였다 (2026-11-26).
        /// 그래픽이 없는 마스크는 보이지 않는 Image(StencilShape)를 붙여 모양으로 쓴다 (셔플 때만 켬).
        /// </summary>
        void SwapToStencil(RectMask2D mask, bool on)
        {
            var go = mask.gameObject;
            if (!go.TryGetComponent(out Mask stencil))
            {
                if (!on) return;
                if (!go.TryGetComponent(out Graphic _))
                {
                    go.AddComponent<Image>(); // 모양용 (Mask가 그리지 않음)
                    stencilShapes.Add(go);
                }
                stencil = go.AddComponent<Mask>();
                stencil.showMaskGraphic = !stencilShapes.Contains(go); // 원래 있던 그래픽(도면 판 등)은 그대로 보임
            }
            if (stencilShapes.Contains(go)) go.GetComponent<Image>().enabled = on; // 붙인 모양은 셔플 때만
            stencil.enabled = on;
            mask.enabled = !on;
        }

        readonly System.Collections.Generic.HashSet<GameObject> stencilShapes = new(); // 마스크 모양용으로 붙인 Image가 있는 오브젝트

        /// <summary>들림(-1.2° · 위 8) → 흔들림(0.8° · 아래 3) → OutBack 착지. 위치는 (·, 0) 기준.</summary>
        static void Shuffle(RectTransform target, float duration)
        {
            target.DOKill(true);
            DOTween.Sequence().SetTarget(target).SetUpdate(true)
                .Append(target.DOLocalRotate(new Vector3(0, 0, -1.2f), duration * 0.3f).SetEase(Ease.OutQuad))
                .Join(target.DOAnchorPosY(8f, duration * 0.3f).SetEase(Ease.OutQuad))
                .Append(target.DOLocalRotate(new Vector3(0, 0, 0.8f), duration * 0.35f).SetEase(Ease.InOutQuad))
                .Join(target.DOAnchorPosY(-3f, duration * 0.35f).SetEase(Ease.InOutQuad))
                .Append(target.DOLocalRotate(Vector3.zero, duration * 0.35f).SetEase(Ease.OutBack, 2f))
                .Join(target.DOAnchorPosY(0f, duration * 0.35f).SetEase(Ease.OutBack, 2f));
        }
    }
}
