using System.Collections.Generic;
using APC.Core;
using APC.Economy;
using APC.Staff;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.Events
{
    /// <summary>
    /// 정전 이벤트 (GDD 14.3, M13-15). Lv.4부터 파트가 시작할 때 `chance` 확률로 그 파트 `minTime`~`maxTime`초 사이 무작위 시점에 정전 1번
    /// (연달아 `maxInARow` 파트까지). 정전 중에는 알바 전원이 제자리에 멈추고(StaffWorker가 `Active`를 본다) 머리 위에 느낌표,
    /// 컴퓨터 책상이 꺼진다(ContractPC). 연출 = 해 어둡게 + 붉은 빛 깜빡임 + HUD 한 줄 + 분전반 위 느낌표. 분전반(BreakerPanel) 게이지를 채우면 끝난다.
    /// 씬 `Site/Logistics`에 하나. UI · 느낌표는 실행 중 만든다.
    /// </summary>
    public sealed class Outage : MonoBehaviour
    {
        public static Outage Instance { get; private set; }

        /// <summary>지금 정전 중인가 (알바 멈춤 · 컴퓨터 책상 꺼짐).</summary>
        public static bool Active => Instance != null && Instance.active.Value;

        [SerializeField, Min(1)] int unlockLevel = 4;
        [SerializeField, Range(0f, 1f), Tooltip("파트가 시작할 때 그 파트에 정전이 일어날 확률 (GDD 14.3 임시)")] float chance = 0.5f;
        [SerializeField, Min(0f), Tooltip("정전 시점 = 파트 시작 뒤 이 범위 무작위 (초)")] float minTime = 30f;
        [SerializeField, Min(0f)] float maxTime = 150f;
        [SerializeField, Min(1), Tooltip("연달아 정전이 나는 파트 수 최대 (GDD '파트 3번당 최대 2회')")] int maxInARow = 2;

        [Header("연출")]
        [SerializeField, Range(0f, 1f), Tooltip("정전 중 해 밝기 배율")] float dimFactor = 0.25f;
        [SerializeField] Color alarmColor = new(0.9f, 0.05f, 0.05f, 0.28f);
        [SerializeField, Min(0.1f), Tooltip("붉은 빛 깜빡임 한 주기 (초)")] float alarmPeriod = 1.4f;
        [SerializeField] string message = "정전! 분전반을 고치세요";
        [SerializeField, Min(1f), Tooltip("분전반 위 느낌표 크기 (알바 위는 8)")] float breakerMarkSize = 20f;

        readonly ReactiveProperty<bool> active = new(false);
        readonly List<TextMeshPro> marks = new();
        float triggerAt = -1f; // 이번 파트 정전 시점 (파트 시작 뒤 초), 없으면 −1
        int inARow;
        Light sun;
        float sunIntensity;
        Image alarm;
        TMP_Text banner;
        TMP_FontAsset font;

        public ReadOnlyReactiveProperty<bool> IsActive => active;

        /// <summary>분전반 위치 (느낌표). BreakerPanel이 등록한다.</summary>
        public Transform Breaker { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            active.Dispose();
        }

        void Start()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) sun = l;
            }
            if (sun != null) sunIntensity = sun.intensity;
            var anyText = FindAnyObjectByType<TextMeshProUGUI>();
            font = anyText != null ? anyText.font : TMP_Settings.defaultFontAsset;
            BuildHud();
            if (PartService.Instance != null) PartService.Instance.Part.Subscribe(_ => RollPart()).AddTo(this);
            active.Subscribe(Apply).AddTo(this);
        }

        /// <summary>파트가 시작할 때: 이번 파트에 정전이 날지 정한다.</summary>
        void RollPart()
        {
            triggerAt = -1f;
            if (GameManager.Instance.Level.Level.CurrentValue < unlockLevel) return;
            if (inARow >= maxInARow || Random.value >= chance)
            {
                inARow = 0;
                return;
            }
            inARow++;
            triggerAt = Random.Range(minTime, maxTime);
        }

        void Update()
        {
            var parts = PartService.Instance;
            if (triggerAt >= 0f && parts != null && parts.IsRunning && parts.CurrentPartLength - parts.Remaining.CurrentValue >= triggerAt)
            {
                triggerAt = -1f;
                Begin();
            }
            if (!active.Value) return;

            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / alarmPeriod);
            alarm.color = new Color(alarmColor.r, alarmColor.g, alarmColor.b, alarmColor.a * pulse);
            UpdateMarks();
        }

        /// <summary>정전 시작 (디버그 메뉴도 부른다).</summary>
        public void Begin() => active.Value = true;

        /// <summary>정전 끝 (분전반 게이지 · 디버그).</summary>
        public void End() => active.Value = false;

        void Apply(bool on)
        {
            if (sun != null) sun.intensity = on ? sunIntensity * dimFactor : sunIntensity;
            alarm.gameObject.SetActive(on);
            banner.gameObject.SetActive(on);
            if (!on)
            {
                foreach (var mark in marks) mark.gameObject.SetActive(false);
            }
        }

        /// <summary>알바 머리 위 · 분전반 위 느낌표 (통통 튐, 카메라를 봄).</summary>
        void UpdateMarks()
        {
            var targets = new List<(Transform t, float height, float size)>();
            if (Breaker != null) targets.Add((Breaker, 2.4f, breakerMarkSize)); // 분전반은 크게 (2026-11-18)
            if (StaffService.Instance != null)
            {
                foreach (var worker in StaffService.Instance.Workers) targets.Add((worker.transform, 2.6f, 8f));
            }
            while (marks.Count < targets.Count) marks.Add(CreateMark());
            var cam = Camera.main;
            var bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 0.25f;
            for (var i = 0; i < marks.Count; i++)
            {
                var on = i < targets.Count && targets[i].t != null;
                marks[i].gameObject.SetActive(on);
                if (!on) continue;
                marks[i].fontSize = targets[i].size;
                marks[i].rectTransform.sizeDelta = Vector2.one * (targets[i].size * 0.25f);
                marks[i].transform.position = targets[i].t.position + Vector3.up * (targets[i].height + bounce * targets[i].size / 8f);
                if (cam != null) marks[i].transform.rotation = cam.transform.rotation;
            }
        }

        TextMeshPro CreateMark()
        {
            var mark = new GameObject("OutageMark").AddComponent<TextMeshPro>();
            mark.transform.SetParent(transform, false);
            mark.font = font;
            mark.text = "!";
            mark.fontSize = 8f;
            mark.fontStyle = FontStyles.Bold;
            mark.color = new Color(1f, 0.85f, 0.1f);
            mark.outlineWidth = 0.25f;
            mark.outlineColor = new Color32(60, 20, 0, 255);
            mark.alignment = TextAlignmentOptions.Center;
            mark.rectTransform.sizeDelta = new Vector2(2f, 2f);
            return mark;
        }

        /// <summary>화면 전체 붉은 빛 + HUD 위 한 줄 (자체 캔버스, 레이캐스트 안 막음).</summary>
        void BuildHud()
        {
            var canvas = new GameObject("OutageCanvas").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // HUD 위, 팝업 아래쯤
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            alarm = new GameObject("Alarm").AddComponent<Image>();
            alarm.transform.SetParent(canvas.transform, false);
            alarm.raycastTarget = false;
            var rect = alarm.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var text = new GameObject("Banner").AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(canvas.transform, false);
            text.font = font;
            text.text = message;
            text.fontSize = 56f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(120, 0, 0, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var t = text.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 1f);
            t.pivot = new Vector2(0.5f, 1f);
            t.anchoredPosition = new Vector2(0f, -170f);
            t.sizeDelta = new Vector2(1200f, 90f);
            banner = text;

            alarm.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
        }
    }
}
