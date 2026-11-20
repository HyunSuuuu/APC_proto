using System.Text;
using TMPro;
using Unity.Profiling;
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

        float elapsedTime;
        int frameCount;

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
        }

        public void Toggle() => SetOpen(!IsOpen);

        void SetOpen(bool open)
        {
            if (panel != null) panel.SetActive(open);
            if (toggleLabel != null) toggleLabel.text = open ? "PERF off" : "PERF";
            elapsedTime = 0f;
            frameCount = 0;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) Toggle();
            if (!IsOpen) return;

            elapsedTime += Time.unscaledDeltaTime;
            frameCount++;
            if (elapsedTime < updateInterval) return;

            var fps = frameCount / elapsedTime;
            var frameTimeMs = elapsedTime / frameCount * 1000f;
            const float Mb = 1024f * 1024f;

            sb.Clear();
            sb.AppendLine($"FPS: {fps:F1}");
            sb.AppendLine($"Frame Time: {frameTimeMs:F2} ms");
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

            elapsedTime = 0f;
            frameCount = 0;
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
