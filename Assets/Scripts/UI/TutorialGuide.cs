using System;
using System.Collections;
using System.Collections.Generic;
using APC.Cameras;
using APC.Contracts;
using APC.Core;
using APC.Data;
using APC.Economy;
using APC.Interaction;
using APC.Logistics;
using APC.Site;
using APC.Staff;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 튜토리얼 (GDD 13, M13-17). 하차 → 선반 → 상차 → 돈 줍기 → 사무실(정산 → 고용 → B보관) → Lv.2 반품 구역.
    /// 단계가 시작할 때 게임을 멈추고 카메라가 대상으로 날아가 확대 → `holdTime`초(또는 탭) 뒤 돌아와 재개, 그 뒤로는 가이드 화살표가 대상을 가리킨다.
    /// 안내 글은 화면 위 한 줄 배너 (단계가 끝날 때까지). 첫 고용은 무료 · 픽업 3 · 이동속도 1 고정 보관 알바 (기본 옷).
    /// 저장이 없으므로 새 게임마다 처음부터. 씬 `Site/Logistics`에 하나. UI는 실행 중 만든다.
    /// </summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        sealed class Step
        {
            public string Message;
            public Func<Transform> Target; // null이면 강조 · 화살표 없음 (대상이 아직 없으면 생길 때까지 기다림)
            public Func<bool> Done;
        }

        public static TutorialGuide Instance { get; private set; }

        /// <summary>디버그 메뉴: 시작할 때 튜토리얼을 건너뛴다.</summary>
        public static bool Disabled;

        [SerializeField, Tooltip("끄면 튜토리얼 없이 시작")] bool runOnStart = true;
        [SerializeField, Min(0.05f), Tooltip("카메라가 대상으로 날아가는 시간 (초, 실제 시간)")] float flyDuration = 0.6f;
        [SerializeField, Min(0f), Tooltip("대상을 비추는 시간 (초, GDD 13 임시 1.5~2). 화면을 누르면 바로 돌아온다")] float holdTime = 1.8f;
        [SerializeField, Range(0.2f, 1.5f), Tooltip("확대 = 기본 거리 × 이 값")] float zoom = 0.7f;
        [SerializeField, Tooltip("첫 고용 무료 알바 스탯 (GDD 13: 픽업 3 · 이동속도 1)")] StaffStats freeHireStats = new(1, 3);
        [SerializeField, Min(0f), Tooltip("끝 안내를 띄워 두는 시간 (초)")] float finishDuration = 3f;

        TMP_Text banner;
        GameObject bannerRoot;
        Interactor player;
        int stepIndex = -1;
        bool skipStep;
        bool bannerOn;
        bool spotlighting;

        public bool Running => stepIndex >= 0;
        public int StepIndex => stepIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GuideArrow.TutorialTarget = null;
        }

        // 배너는 팝업(정산 · 책상)이 열려 있으면 숨긴다 — 팝업 위를 가리지 않게. 강조 중(게임 멈춤)에는 보인다
        void Update()
        {
            if (bannerRoot == null) return;
            var show = bannerOn && (spotlighting || GameManager.Instance.State.CurrentValue != GameState.Popup);
            if (bannerRoot.activeSelf != show) bannerRoot.SetActive(show);
        }

        IEnumerator Start()
        {
            if (!runOnStart || Disabled) yield break;
            yield return null; // 모듈 · 선반 · 알바 서비스가 Start를 마칠 때까지
            BuildBanner();
            foreach (var who in Interactor.All)
            {
                if (who.GetComponent<StaffWorker>() == null) player = who;
            }
            yield return Run();
        }

        /// <summary>디버그: 지금 단계를 끝난 것으로 친다.</summary>
        public void SkipStep() => skipStep = true;

        /// <summary>디버그: 튜토리얼을 끝낸다.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            Finish(false);
        }

        IEnumerator Run()
        {
            var steps = CreateSteps();
            for (stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                var step = steps[stepIndex];
                skipStep = false;
                ShowBanner(step.Message);

                // 대상이 생길 때까지 (레벨 · 건설 타일 등) 기다렸다가, 화면이 비어 있을 때(팝업 · 연출 없음) 한 번 강조한다
                Transform target = null;
                var spotlit = step.Target == null;
                while (!skipStep && !step.Done())
                {
                    if (target == null && step.Target != null) target = step.Target();
                    GuideArrow.TutorialTarget = target;
                    if (!spotlit && target != null && GameManager.Instance.State.CurrentValue == GameState.Playing)
                    {
                        spotlit = true;
                        yield return Spotlight(target.position);
                        continue;
                    }
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }
            Finish(true);
        }

        void Finish(bool completed)
        {
            stepIndex = -1;
            GuideArrow.TutorialTarget = null;
            if (bannerRoot == null) return;
            if (!completed)
            {
                bannerOn = false;
                return;
            }
            ShowBanner("튜토리얼 끝! 물류센터를 키워 보세요");
            StartCoroutine(HideLater());
        }

        IEnumerator HideLater()
        {
            yield return new WaitForSecondsRealtime(finishDuration);
            bannerOn = false;
        }

        /// <summary>게임 멈춤 → 카메라 대상으로 이동 · 확대 → `holdTime`초 또는 탭 → 돌아와 재개.</summary>
        IEnumerator Spotlight(Vector3 point)
        {
            var rig = Camera.main != null ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig == null) yield break;
            var gm = GameManager.Instance;
            gm.OpenPopup();
            spotlighting = true;
            yield return rig.Spotlight(point, zoom, flyDuration).WaitForCompletion();
            for (var t = 0f; t < holdTime && !Tapped(); t += Time.unscaledDeltaTime) yield return null;
            rig.EndSpotlight();
            spotlighting = false;
            gm.ClosePopup();
        }

        static bool Tapped()
        {
            var pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        List<Step> CreateSteps()
        {
            var staff = StaffService.Instance;
            var wallet = GameManager.Instance.Wallet;
            return new List<Step>
            {
                new()
                {
                    Message = "트럭 뒷문에서 상자를 꺼내세요",
                    Target = () => Find<TruckPickupZone>(_ => true),
                    Done = () => player != null && player.Stack != null && player.Stack.Items.Count > 0,
                },
                new()
                {
                    Message = "팔레트에 내려놓고, 같은 색 선반으로 옮기세요", // 트럭 → 하차 칸 공간 → 선반 (GDD 7.2)
                    Target = () => Find<Shelf>(s => s.Item != null),
                    Done = () => Find<Shelf>(s => s.Count.CurrentValue > 0) != null,
                },
                new()
                {
                    Message = "상차 트럭에 주문한 상자를 실으세요",
                    Target = () => Find<TruckLoadZone>(_ => true),
                    Done = () => wallet.Piggy.CurrentValue > 0 || Find<CashZone>(c => c.StoredCash > 0) != null,
                },
                new()
                {
                    Message = "돈을 주워 저금통에 모으세요",
                    Target = () => Find<CashZone>(c => c.StoredCash > 0) ?? Find<CashZone>(_ => true),
                    Done = () => wallet.Piggy.CurrentValue > 0,
                },
                new()
                {
                    Message = "파트가 끝나면 정산을 확인하세요",
                    Target = null,
                    Done = () => PartService.Instance == null || PartService.Instance.Part.CurrentValue >= 2,
                },
                new()
                {
                    Message = "클립보드 책상에서 알바를 고용하세요 (무료)",
                    Target = () => Find<ContractPC>(pc => !pc.IsDeskB),
                    Done = () =>
                    {
                        if (staff == null) return true;
                        if (staff.Workers.Count > 0) return true;
                        staff.OfferFreeCandidate(StaffRole.Sort, freeHireStats); // 파트가 바뀌어 후보가 새로 뽑혀도 다시 넣는다
                        return false;
                    },
                },
                new()
                {
                    Message = "B보관 건설 타일에 돈을 채워 지으세요",
                    Target = () => Find<ExpansionPad>(p => p.PlacementId == "StoreB"),
                    Done = () => SiteGrid.Instance == null || SiteGrid.Instance.IsUnlocked("StoreB"),
                },
                new()
                {
                    Message = "Lv.2가 되면 반품 구역을 지으세요",
                    Target = () => Find<ExpansionPad>(p => p.PlacementId == "Ret1"),
                    Done = () => SiteGrid.Instance == null || SiteGrid.Instance.IsUnlocked("Ret1"),
                },
            };
        }

        static Transform Find<T>(Predicate<T> match) where T : Component
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (match(c)) return c.transform;
            }
            return null;
        }

        void ShowBanner(string text)
        {
            if (banner == null) return;
            banner.text = text;
            bannerOn = true;
        }

        /// <summary>화면 위 한 줄 배너 (레이캐스트 안 막음).</summary>
        void BuildBanner()
        {
            var canvas = new GameObject("TutorialCanvas").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 49; // HUD 위, 팝업은 State로 숨김
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var back = new GameObject("Banner").AddComponent<Image>();
            back.transform.SetParent(canvas.transform, false);
            back.color = new Color(0f, 0f, 0f, 0.55f);
            back.raycastTarget = false;
            var r = back.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -110f); // HUD 돈 · 저금통 줄 아래
            r.sizeDelta = new Vector2(1100f, 84f);
            bannerRoot = back.gameObject;

            var anyText = FindAnyObjectByType<TextMeshProUGUI>();
            var text = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(back.transform, false);
            text.font = anyText != null ? anyText.font : TMP_Settings.defaultFontAsset;
            text.fontSize = 44f;
            text.enableAutoSizing = true; // 긴 안내도 한 줄
            text.fontSizeMin = 26f;
            text.fontSizeMax = 44f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var t = text.rectTransform;
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = t.offsetMax = Vector2.zero;
            banner = text;
            bannerRoot.SetActive(false);
        }
    }
}
