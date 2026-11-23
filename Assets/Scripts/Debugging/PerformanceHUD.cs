using System.Text;
using TMPro;
using Unity.Profiling;
using APC.Site;
using APC.Staff;
using APC.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace APC.Debugging
{
    /// <summary>
    /// 성능 측정 패널 (2026-11-20, 사용자 제공 코드). 왼쪽 아래 [PERF] 버튼 · F2로 열고 닫는다.
    /// FPS · 프레임 시간, CPU 메인 · 렌더 · GPU 시간, 정점 · 삼각형 · 드로우콜 · 배치 · SetPass, 메모리.
    /// 닫혀 있으면 글자를 만들지 않는다. UI는 TycoonTestBuilder가 TycoonTest 씬에 만든다.
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

        // Rendering
        ProfilerRecorder vertexRecorder;
        ProfilerRecorder triangleRecorder;
        ProfilerRecorder drawCallRecorder;
        ProfilerRecorder batchRecorder;
        ProfilerRecorder setPassRecorder;

        // CPU / GPU Frame Timing
        ProfilerRecorder cpuMainRecorder;
        ProfilerRecorder cpuRenderRecorder;
        ProfilerRecorder gpuFrameRecorder;

        // GC (개발 빌드에서만 값이 있다 — 일반 빌드는 N/A)
        ProfilerRecorder gcAllocRecorder;

        float elapsedTime;
        int frameCount;
        float maxFrameMs;   // 구간 안 가장 긴 프레임 (0.5초 주기 끊김 확인용)
        long maxGcAlloc;    // 구간 안 프레임당 GC 할당 최대 (바이트)
        long sumGcAlloc;    // 구간 GC 할당 합 (바이트)

        // 누적 평균: 패널을 연 때부터 (닫았다 열면 처음부터) — 실기기 비교용
        const float HitchMs = 33f; // 이보다 긴 프레임 = 끊김 (30fps 아래). 20ms는 실기기 평균이 그 근처라 거의 전부 걸렸다
        float runTime;
        int runFrames;
        float runMaxFrameMs;
        int runHitches;
        long runGcAlloc;
        int runGcCount0;
        int runScanCount0;
        double runScanMs0;
        int runPaths0;
        int runNoPath0;
        readonly StringBuilder sb = new(512);

        bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            Application.targetFrameRate = targetFPS;
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            SetOpen(startOpen);
        }

        void OnEnable()
        {
            vertexRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            triangleRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            drawCallRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batchRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");

            cpuMainRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            cpuRenderRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Render Thread Frame Time");
            gpuFrameRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time");

            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        void OnDisable()
        {
            vertexRecorder.Dispose();
            triangleRecorder.Dispose();
            drawCallRecorder.Dispose();
            batchRecorder.Dispose();
            setPassRecorder.Dispose();

            cpuMainRecorder.Dispose();
            cpuRenderRecorder.Dispose();
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
            runScanCount0 = WallCutout.ScanCount;
            runScanMs0 = WallCutout.TotalScanMs;
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

            var fps = frameCount / elapsedTime;
            var frameTimeMs = elapsedTime / frameCount * 1000f;
            const float Mb = 1024f * 1024f;

            sb.Clear();
            sb.AppendLine($"FPS: {fps:F1}");
            sb.AppendLine($"Frame Time: {frameTimeMs:F2} ms (max {maxFrameMs:F2})");
            sb.AppendLine();
            sb.AppendLine($"Cutout Scan: {WallCutout.LastScanMs:F2} ms (max {WallCutout.MaxScanMs:F2}) / Walls {WallCutout.SwappedCount:N0}");
            sb.AppendLine($"NavMesh Build: {SiteNavMesh.LastBuildMs:F2} ms (max {SiteNavMesh.MaxBuildMs:F2}) x {SiteNavMesh.BuildCount:N0}");
            sb.AppendLine(gcAllocRecorder.Valid
                ? $"GC Alloc: {sumGcAlloc / elapsedTime / 1024f:F1} KB/s (frame max {maxGcAlloc / 1024f:F1} KB)"
                : "GC Alloc: N/A (dev build only)");
            sb.AppendLine();
            // 누적 평균 (패널을 연 뒤 runTime초)
            var scans = WallCutout.ScanCount - runScanCount0;
            sb.AppendLine($"[Avg {runTime:F0}s] FPS {runFrames / runTime:F1}");
            sb.AppendLine($"Frame: {runTime / runFrames * 1000f:F2} ms (max {runMaxFrameMs:F1}, >{HitchMs:0}ms {runHitches:N0})");
            sb.AppendLine(scans > 0
                ? $"Scan: {(WallCutout.TotalScanMs - runScanMs0) / scans:F2} ms x {scans:N0}"
                : "Scan: 0");
            sb.AppendLine($"Paths: {(StaffWorker.PathRequests - runPaths0) / runTime:F1}/s, NoPath +{StaffWorker.NoPathCount - runNoPath0:N0} ({(StaffService.Instance != null ? StaffService.Instance.Workers.Count : 0)} staff)");
            sb.AppendLine(gcAllocRecorder.Valid
                ? $"GC: {runGcAlloc / runTime / 1024f:F1} KB/s, +{System.GC.CollectionCount(0) - runGcCount0:N0} GC"
                : $"GC: +{System.GC.CollectionCount(0) - runGcCount0:N0} GC");
            sb.AppendLine();
            sb.AppendLine($"CPU Main: {FormatTime(ReadTimeMs(cpuMainRecorder))}");
            sb.AppendLine($"CPU Render: {FormatTime(ReadTimeMs(cpuRenderRecorder))}");
            sb.AppendLine($"GPU Frame: {FormatTime(ReadTimeMs(gpuFrameRecorder))}");
            sb.AppendLine();
            sb.AppendLine($"Vertices: {FormatValue(ReadRecorder(vertexRecorder))}");
            sb.AppendLine($"Triangles: {FormatValue(ReadRecorder(triangleRecorder))}");
            sb.AppendLine($"Draw Calls: {FormatValue(ReadRecorder(drawCallRecorder))}");
            sb.AppendLine($"Batches: {FormatValue(ReadRecorder(batchRecorder))}");
            sb.AppendLine($"SetPass: {FormatValue(ReadRecorder(setPassRecorder))}");
            sb.AppendLine();
            sb.AppendLine($"Unity Allocated: {Profiler.GetTotalAllocatedMemoryLong() / Mb:F1} MB");
            sb.AppendLine($"Unity Reserved: {Profiler.GetTotalReservedMemoryLong() / Mb:F1} MB");
            sb.AppendLine($"Managed Heap: {Profiler.GetMonoUsedSizeLong() / Mb:F1} MB");
            sb.Append($"System RAM: {SystemInfo.systemMemorySize:N0} MB (Total)");

            if (performanceText != null) performanceText.text = sb.ToString();

            ResetInterval();
        }

        static long ReadRecorder(ProfilerRecorder recorder) => !recorder.Valid || recorder.Count == 0 ? -1 : recorder.LastValue;

        // 나노초 → 밀리초
        static double ReadTimeMs(ProfilerRecorder recorder)
        {
            var value = ReadRecorder(recorder);
            return value <= 0 ? -1 : value / 1_000_000.0;
        }

        static string FormatValue(long value) => value < 0 ? "N/A" : value.ToString("N0");

        static string FormatTime(double value) => value < 0 ? "N/A" : $"{value:F2} ms";
    }
}
