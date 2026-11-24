using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using APC.Core;
using APC.Data;
using APC.Interaction;
using APC.Site;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace APC.Debugging
{
    /// <summary>
    /// 길찾기 비교 측정 도구 (NavMesh vs 방 단위 A*, 2026-11-24). TycoonTest Play 중에 메뉴 `APC > Test > Nav Benchmark`가 붙인다
    /// (씬에는 두지 않는다). 단계를 자동으로 진행하며 Profiler 마커를 모아 `Logs/NavBench/<시각>/result.md`와 콘솔에 결과 표를 남긴다.
    /// 단계마다 Profiler 기록(.raw)도 남긴다 — Profiler 창에서 Load. 끝나면 콘솔에 "[NavBench] 끝".
    /// </summary>
    public sealed partial class NavBenchmark : MonoBehaviour
    {
        NavBenchmarkConfig config;
        NavBenchRecorder recorder;
        SiteNavGrid siteNav;
        NavMeshSurface surface;
        Transform siteRoot;
        string folder;
        int logIndex;
        bool profilerWasEnabled;
        float repathInterval;
        float speed;
        readonly List<Vector3> targets = new();
        readonly List<PhaseStats> phases = new();
        readonly List<string> sections = new();
        readonly List<string> headerLines = new();

        public bool Done { get; private set; }
        public string ResultPath { get; private set; }

        public void Init(NavBenchmarkConfig benchConfig)
        {
            config = benchConfig;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Application.runInBackground = true;
            profilerWasEnabled = Profiler.enabled;
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NavBench", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(folder);
            Debug.Log($"[NavBench] 시작 — 알바 {config.AgentCount}명, 측정 {config.MeasureSeconds}초 × {config.Repeats}회, 결과 {folder}");
            yield return new WaitForSecondsRealtime(3f); // 선반 생성 · 첫 NavMesh 굽기가 끝나도록

            recorder = new NavBenchRecorder();
            foreach (var slot in Object.FindObjectsByType<WorkSlot>(FindObjectsSortMode.InstanceID)) targets.Add(slot.transform.position);
            var staff = GameManager.Instance.Database.Staff;
            speed = staff.GetMoveSpeed(staff.StatMin);
            repathInterval = config.DefaultRepathInterval;
            surface = Object.FindFirstObjectByType<NavMeshSurface>();
            siteRoot = surface != null ? surface.transform : SiteGrid.Instance.transform; // NavMesh가 굽는 것과 같은 충돌체 묶음
            siteNav = new SiteNavGrid(SiteGrid.Instance, siteRoot, config.Grid);

            if (config.Calibrate) yield return Calibrate();
            yield return Steady();
            PathCost();
            yield return AStarEvents();
            if (surface != null) yield return NavMeshEvents();
            else sections.Add("## 3 · 4. NavMesh 해금 · 레일\n\n- NavMeshSurface가 없어 건너뜀");

            Finish();
        }

        /// <summary>1 기준 · 2 NavMesh 100명 · 3 A* 100명 × 반복.</summary>
        IEnumerator Steady()
        {
            for (var r = 1; r <= config.Repeats; r++)
            {
                var log = r == 1; // Profiler 기록은 첫 반복만 (크기)
                yield return Phase($"기준 #{r}", "기준", null, log);
                yield return Phase($"NavMesh {config.AgentCount}명 #{r}", "NavMesh", NavBenchAgents.Mode.NavMesh, log);
                yield return Phase($"A* {config.AgentCount}명 #{r}", "A*", NavBenchAgents.Mode.AStar, log);
            }
        }

        IEnumerator Phase(string name, string kind, NavBenchAgents.Mode? mode, bool log)
        {
            var agents = mode == null ? null : new NavBenchAgents(mode.Value, config, targets, speed, repathInterval, siteNav.World);
            yield return Drive(agents, config.WarmupSeconds, false);
            int requests0 = agents?.PathRequests ?? 0, failures0 = agents?.PathFailures ?? 0;
            if (log) BeginProfilerLog(kind);
            recorder.BeginPhase(name, kind);
            var start = Time.realtimeSinceStartupAsDouble;
            yield return Drive(agents, config.MeasureSeconds, true, log ? config.ProfilerLogSeconds : 0f);
            var stats = recorder.EndPhase();
            stats.Seconds = Time.realtimeSinceStartupAsDouble - start;
            EndProfilerLog();
            if (agents != null)
            {
                stats.PathRequests = agents.PathRequests - requests0;
                stats.PathFailures = agents.PathFailures - failures0;
                agents.Dispose();
            }
            phases.Add(stats);
            Debug.Log($"[NavBench] {name}: PlayerLoop 중앙 {NavBenchReport.F(stats.Median(NavBenchRecorder.PlayerLoop))}ms, 길찾기 합 {NavBenchReport.F(NavBenchReport.NavCost(stats))}ms");
            yield return null; // 지운 알바가 사라진 뒤 다음 단계
        }

        /// <param name="logSeconds">0보다 크면 그 초가 지나면 Profiler 기록을 멈춘다 (측정은 계속).</param>
        IEnumerator Drive(NavBenchAgents agents, float seconds, bool record, float logSeconds = 0f)
        {
            var start = Time.realtimeSinceStartupAsDouble;
            var end = start + seconds;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                agents?.Tick(Time.deltaTime);
                if (record) recorder.Sample();
                if (logSeconds > 0f && Time.realtimeSinceStartupAsDouble - start > logSeconds) EndProfilerLog();
                yield return null;
            }
        }

        void BeginProfilerLog(string kind)
        {
            if (!config.RecordProfilerLog) return;
            var tag = kind switch { "기준" => "baseline", "NavMesh" => "navmesh", "A*" => "astar", _ => "real" };
            Profiler.logFile = Path.Combine(folder, $"{++logIndex:00}-{tag}.raw");
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
        }

        void EndProfilerLog()
        {
            if (!Profiler.enableBinaryLog) return;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = string.Empty;
        }

        void Finish()
        {
            headerLines.Add($"- {DateTime.Now:yyyy-MM-dd HH:mm} · 씬 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} · {(Application.isEditor ? "에디터" : "빌드")} {Application.unityVersion}");
            headerLines.Add($"- 측정 알바 {config.AgentCount}명 · 속도 {speed:F2}m/s · 길 요청 간격 {repathInterval:F2}초(알바마다) · 목적지 {targets.Count}곳(WorkSlot) · 예열 {config.WarmupSeconds}초 · 측정 {config.MeasureSeconds}초 × {config.Repeats}회");
            var grid = siteNav.World.Grid;
            headerLines.Add($"- A* 격자: 방 {grid.RoomsX}×{grid.RoomsZ} · 칸 {grid.CellSize}m ({grid.Width}×{grid.Height}) · 문 {siteNav.World.Gates.TotalGates}개 · 메모리 {(grid.MemoryBytes + siteNav.World.Gates.MemoryBytes) / 1024}KB · 처음 전체 굽기 {siteNav.BuildMs:F1}ms");
            var triangles = UnityEngine.AI.NavMesh.CalculateTriangulation().indices.Length / 3;
            headerLines.Add($"- NavMesh: 삼각형 {triangles}개 (지금 게임 방식: 해금 · 재배치 때 부지 전체 동기 굽기, 선반은 carving)");

            var report = NavBenchReport.Build(string.Join("\n", headerLines), phases, sections, recorder.Missing);
            ResultPath = Path.Combine(folder, "result.md");
            File.WriteAllText(ResultPath, report);
            recorder.Dispose();
            Profiler.enabled = profilerWasEnabled; // 기록 때 켠 Profiler를 원래대로
            Debug.Log("[NavBench] 결과\n" + report);
            Debug.Log($"[NavBench] 끝 — {ResultPath}");
            Done = true;
        }

        void OnDestroy() => recorder?.Dispose();
    }
}
