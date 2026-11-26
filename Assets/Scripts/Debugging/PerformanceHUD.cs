using System.Text;
using TMPro;
using Unity.Profiling;
using APC.Staff;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace APC.Debugging
{
    /// <summary>
    /// 성능 측정 패널 (2026-11-20, 사용자 제공 코드). 왼쪽 아래 [PERF] 버튼 · F2로 열고 닫는다.
    /// 2026-11-26 알바 최적화 측정용으로 줄임: 프레임 · CPU/GPU · GC · 메모리 + 알바 상태 분포 · 길찾기. 세로 화면에 맞게 왼쪽 가운데 456폭 패널.
    /// 닫혀 있으면 글자를 만들지 않는다. UI는 TycoonTestBuilder가 TycoonTest 씬에 만든다 (Game 씬에도 있음).
    /// </summary>
    public sealed class PerformanceHUD : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] TMP_Text performanceText;
        [SerializeField, Tooltip("열고 닫는 패널 (글자 · 배경)")] GameObject panel;
        [SerializeField] Button toggleButton;
        [SerializeField] TMP_Text toggleLabel;

        [Header("Settings")]
        [SerializeField] float updateInterval = 0.5f;
        [SerializeField] int targetFPS = 120;
        [SerializeField] Key toggleKey = Key.F2;
        [SerializeField] bool startOpen = true;

        // 세로 화면(1080 높이 기준 캔버스 폭 약 480)에 들어가는 크기
        static readonly Vector2 PanelSize = new(456f, 530f);
        const float PanelMargin = 12f;
        const float TextPadding = 12f;
        const float FontSize = 21f;
        const float Mb = 1024f * 1024f;

        ProfilerRecorder cpuMainRecorder;
        ProfilerRecorder gpuFrameRecorder;
        ProfilerRecorder gcAllocRecorder; // 개발 빌드에서만 값이 있다 — 일반 빌드는 N/A

        float elapsedTime;
        int frameCount;
        float maxFrameMs;   // 구간 안 가장 긴 프레임
        long maxGcAlloc;    // 구간 안 프레임당 GC 할당 최대 (바이트)
        long sumGcAlloc;    // 구간 GC 할당 합 (바이트)

        // 누적 평균: 패널을 연 때부터 (닫았다 열면 처음부터) — 실기기 비교용
        const float HitchMs = 33f; // 이보다 긴 프레임 = 끊김 (30fps 아래)
        float runTime;
        int runFrames;
        float runMaxFrameMs;
        int runHitches;
        long runGcAlloc;
        int runGcCount0;
        int runPaths0;
        int runNoPath0;
        readonly StringBuilder sb = new(512);

        bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            Application.targetFrameRate = targetFPS;
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            FitPortrait();
            SetOpen(startOpen);
        }

        /// <summary>씬마다 다른 패널 배치를 무시하고 왼쪽 가운데에 세로 화면 폭으로 붙인다 (위 HUD · 아래 버튼을 피함).</summary>
        void FitPortrait()
        {
            if (panel == null) return;
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(PanelMargin, 0f);
            rect.sizeDelta = PanelSize;
            if (panel.TryGetComponent<Graphic>(out var background)) background.raycastTarget = false; // 게임 조작을 막지 않게
            if (performanceText == null) return;
            var textRect = performanceText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TextPadding, TextPadding);
            textRect.offsetMax = new Vector2(-TextPadding, -TextPadding);
            performanceText.fontSize = FontSize;
            performanceText.enableAutoSizing = false;
            performanceText.alignment = TextAlignmentOptions.TopLeft;
            performanceText.raycastTarget = false;
        }

        void OnEnable()
        {
            cpuMainRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            gpuFrameRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time");
            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        void OnDisable()
        {
            cpuMainRecorder.Dispose();
            gpuFrameRecorder.Dispose();
            gcAllocRecorder.Dispose();
        }

        public void Toggle() => SetOpen(!IsOpen);

        void SetOpen(bool open)
        {
            if (panel != null) panel.SetActive(open);
            if (toggleLabel != null) toggleLabel.text = open ? "PERF off" : "PERF";
            ResetInterval();
            ResetRun();
        }

        void ResetRun()
        {
            runTime = 0f;
            runFrames = 0;
            runMaxFrameMs = 0f;
            runHitches = 0;
            runGcAlloc = 0;
            runGcCount0 = System.GC.CollectionCount(0);
            runPaths0 = StaffWorker.PathRequests;
            runNoPath0 = StaffWorker.NoPathCount;
        }

        void ResetInterval()
        {
            elapsedTime = 0f;
            frameCount = 0;
            maxFrameMs = 0f;
            maxGcAlloc = 0;
            sumGcAlloc = 0;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) Toggle();
            if (!IsOpen) return;

            var frameMs = Time.unscaledDeltaTime * 1000f;
            elapsedTime += Time.unscaledDeltaTime;
            frameCount++;
            maxFrameMs = Mathf.Max(maxFrameMs, frameMs);
            runTime += Time.unscaledDeltaTime;
            runFrames++;
            runMaxFrameMs = Mathf.Max(runMaxFrameMs, frameMs);
            if (frameMs > HitchMs) runHitches++;
            var gcAlloc = ReadRecorder(gcAllocRecorder);
            if (gcAlloc >= 0)
            {
                maxGcAlloc = System.Math.Max(maxGcAlloc, gcAlloc);
                sumGcAlloc += gcAlloc;
                runGcAlloc += gcAlloc;
            }
            if (elapsedTime < updateInterval) return;

            sb.Clear();
            // 지금 (0.5초 구간)
            sb.AppendLine($"FPS {frameCount / elapsedTime:F1}  {elapsedTime / frameCount * 1000f:F1}ms (max {maxFrameMs:F1})");
            sb.AppendLine($"CPU {FormatTime(ReadTimeMs(cpuMainRecorder))}  GPU {FormatTime(ReadGpuMs())}");
            sb.AppendLine(gcAllocRecorder.Valid
                ? $"GC {sumGcAlloc / elapsedTime / 1024f:F1}KB/s (frame max {maxGcAlloc / 1024f:F1}KB)"
                : "GC alloc N/A (dev build only)");
            sb.AppendLine();
            // 누적 평균 (패널을 연 뒤 runTime초)
            sb.AppendLine($"[Avg {runTime:F0}s] FPS {runFrames / runTime:F1}  {runTime / runFrames * 1000f:F1}ms");
            sb.AppendLine($"max {runMaxFrameMs:F1}ms, >{HitchMs:0}ms x{runHitches:N0}");
            sb.AppendLine(gcAllocRecorder.Valid
                ? $"GC {runGcAlloc / runTime / 1024f:F1}KB/s, +{System.GC.CollectionCount(0) - runGcCount0:N0} GC"
                : $"GC +{System.GC.CollectionCount(0) - runGcCount0:N0}");
            // 메모리 (2026-11-26 다시 넣음): Unity 할당 / 예약 · 관리 힙 — 기기 RAM 8GB 판단용 (에디터 값은 에디터 몫이 섞여 큼)
            sb.AppendLine($"Mem {Profiler.GetTotalAllocatedMemoryLong() / Mb:F0}/{Profiler.GetTotalReservedMemoryLong() / Mb:F0}MB  Heap {Profiler.GetMonoUsedSizeLong() / Mb:F0}MB");
            sb.AppendLine();
            AppendStaff();

            if (performanceText != null) performanceText.text = sb.ToString();
            ResetInterval();
        }

        /// <summary>
        /// 알바 상태 분포 (2026-11-26, 저사양 기기에서 알바가 서 있는 원인 구분): 일하러 가는 중 ·
        /// 대기 위치로 가는 중 · 일하는 중 · 그 자리 대기(그중 짐 든 채 = 배달할 곳 없음) · 대기 위치 · 판단 전, 길 요청 · 구간 안 한 프레임 Think 최대 수.
        /// </summary>
        void AppendStaff()
        {
            int toWork = 0, toPark = 0, working = 0, waiting = 0, waitCarry = 0, parked = 0, idle = 0;
            var all = StaffWorker.All;
            for (var i = 0; i < all.Count; i++) // IReadOnlyList foreach = 열거자 박싱
            {
                var worker = all[i];
                switch (worker.State)
                {
                    case StaffWorker.Phase.Moving:
                        if (worker.Parking) toPark++;
                        else toWork++;
                        break;
                    case StaffWorker.Phase.Working: working++; break;
                    case StaffWorker.Phase.Waiting:
                        waiting++;
                        if (worker.Stack.Count.CurrentValue > 0) waitCarry++;
                        break;
                    case StaffWorker.Phase.Parked: parked++; break;
                    default: idle++; break;
                }
            }
            sb.AppendLine($"Staff {all.Count} (A*)");
            sb.AppendLine($"Go {toWork}  Work {working}  Idle {idle}");
            sb.AppendLine($"Hold {waiting} (cargo {waitCarry})");
            sb.AppendLine($"ToPark {toPark}  Parked {parked}");
            sb.AppendLine($"Think max {StaffWorker.TakeMaxThinksPerFrame()}/f");
            sb.Append($"Paths {(StaffWorker.PathRequests - runPaths0) / runTime:F1}/s  NoPath +{StaffWorker.NoPathCount - runNoPath0:N0}");
            if (StaffWorker.ThinkScale > 1f) sb.Append("\nA/B: think x4"); // DEBUG 토글이 켜졌는지 스크린샷으로 알게
        }

        readonly FrameTiming[] frameTimings = new FrameTiming[1];

        /// <summary>
        /// GPU 시간 (ms). 프로파일러 카운터가 없으면 FrameTimingManager로 (Player Settings `Frame Timing Stats` 켬, 2026-11-26).
        /// 기기 · 그래픽 API가 지원하지 않으면 -1 (N/A).
        /// </summary>
        double ReadGpuMs()
        {
            var recorded = ReadTimeMs(gpuFrameRecorder);
            if (recorded > 0) return recorded;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTimings) == 0) return -1;
            var gpu = frameTimings[0].gpuFrameTime;
            return gpu > 0 ? gpu : -1;
        }

        static long ReadRecorder(ProfilerRecorder recorder) => !recorder.Valid || recorder.Count == 0 ? -1 : recorder.LastValue;

        // 나노초 → 밀리초
        static double ReadTimeMs(ProfilerRecorder recorder)
        {
            var value = ReadRecorder(recorder);
            return value <= 0 ? -1 : value / 1_000_000.0;
        }

        static string FormatTime(double value) => value < 0 ? "N/A" : $"{value:F1}ms";
    }
}
