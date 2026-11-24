using System;
using System.Collections.Generic;
using APC.Navigation;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace APC.Debugging
{
    /// <summary>마커 하나(또는 묶음)의 단계 통계. 시간은 ms/프레임, GC는 KB/프레임.</summary>
    public readonly struct MarkerStats
    {
        public readonly bool Valid;
        public readonly double Mean, Median, P95, Max, Sum;

        public MarkerStats(List<double> values)
        {
            Valid = values.Count > 0;
            Mean = Median = P95 = Max = Sum = 0;
            if (!Valid) return;
            var sorted = new List<double>(values);
            sorted.Sort();
            foreach (var v in sorted) Sum += v;
            Mean = Sum / sorted.Count;
            Median = sorted[sorted.Count / 2];
            P95 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
            Max = sorted[^1];
        }
    }

    /// <summary>단계 하나의 기록 (측정 도구가 길 요청 수 등을 채운다).</summary>
    public sealed class PhaseStats
    {
        public string Name;
        public string Kind; // "기준" · "NavMesh" · "A*" · "실제 알바"
        public int Frames;
        public double Seconds;
        public int PathRequests, PathFailures, Repaths;
        public readonly Dictionary<string, MarkerStats> Tracks = new();

        public double Median(string track) => Tracks.TryGetValue(track, out var s) && s.Valid ? s.Median : double.NaN;
        public double Mean(string track) => Tracks.TryGetValue(track, out var s) && s.Valid ? s.Mean : double.NaN;
    }

    /// <summary>
    /// 길찾기 비교용 Profiler 기록기 (2026-11-24). 고정 마커 목록을 ProfilerRecorder로 열고 매 프레임 LastValue(직전 프레임 값)를 모은다.
    /// Unity 내부 마커는 실행 중 이름으로 찾아 그 분류로 연다 (없으면 결과 표에 "없음"). 여러 스레드 값은 합쳐진다 (기본 옵션).
    /// </summary>
    public sealed class NavBenchRecorder : IDisposable
    {
        public static readonly ProfilerMarker Driver = new("Bench.Driver");
        public static readonly ProfilerMarker SetDestination = new("Bench.SetDestination");

        public const string PlayerLoop = "PlayerLoop";
        public const string AiMain = "AI 메인";
        public const string CrowdPathfinding = "Crowd.Pathfinding";
        public const string NavMeshManager = "NavMeshManager";
        public const string Carving = "Carving";
        public const string NavMeshBuild = "NavMesh 굽기";
        public const string Path = "Nav.Path";
        public const string Move = "Nav.Move";
        public const string Bake = "Nav.Bake";
        public const string DriverTrack = "Bench.Driver";
        public const string SetDestinationTrack = "Bench.SetDestination";
        public const string Gc = "GC KB";

        static readonly (string label, string[] names, bool bytes)[] UnityTracks =
        {
            (PlayerLoop, new[] { "PlayerLoop" }, false),
            (AiMain, new[] { "PreUpdate.AIUpdate", "PreLateUpdate.AIUpdatePostScript" }, false),
            (CrowdPathfinding, new[] { "Crowd.Pathfinding" }, false),
            (NavMeshManager, new[] { "NavMeshManager" }, false),
            (Carving, new[] { "Carving.Prepare", "Carving.BuildBVTree", "Carving.ProjectVertices", "Carving.ConvexHulls", "Carving.Clipping", "Carving.ApplyResults" }, false),
            (NavMeshBuild, new[] { "NavMesh.BuildNavMeshData", "NavMesh.UpdateNavMeshData" }, false),
            (Gc, new[] { "GC Allocated In Frame" }, true),
        };

        sealed class Track
        {
            public string Label;
            public bool Bytes;
            public readonly List<ProfilerRecorder> Recorders = new();
            public readonly List<double> Values = new();
        }

        readonly List<Track> tracks = new();
        readonly List<string> missing = new();
        PhaseStats current;

        /// <summary>찾지 못한 Unity 마커 이름 (결과 표에 적는다).</summary>
        public IReadOnlyList<string> Missing => missing;

        public NavBenchRecorder()
        {
            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            var byName = new Dictionary<string, ProfilerRecorderDescription>();
            foreach (var handle in available)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                byName.TryAdd(description.Name, description);
            }

            foreach (var (label, names, bytes) in UnityTracks)
            {
                var track = new Track { Label = label, Bytes = bytes };
                foreach (var name in names)
                {
                    if (byName.TryGetValue(name, out var d)) track.Recorders.Add(ProfilerRecorder.StartNew(d.Category, name));
                    else missing.Add(name);
                }
                tracks.Add(track);
            }
            AddMarker(Path, NavMarkers.Path);
            AddMarker(Move, NavMarkers.Move);
            AddMarker(Bake, NavMarkers.Bake);
            AddMarker(DriverTrack, Driver);
            AddMarker(SetDestinationTrack, SetDestination);
        }

        void AddMarker(string label, ProfilerMarker marker)
        {
            var track = new Track { Label = label };
            track.Recorders.Add(ProfilerRecorder.StartNew(marker));
            tracks.Add(track);
        }

        public void BeginPhase(string name, string kind)
        {
            current = new PhaseStats { Name = name, Kind = kind };
            foreach (var t in tracks) t.Values.Clear();
        }

        /// <summary>매 프레임 한 번. 직전 프레임 값을 모은다.</summary>
        public void Sample()
        {
            if (current == null) return;
            current.Frames++;
            foreach (var t in tracks)
            {
                if (t.Recorders.Count == 0) continue;
                long sum = 0;
                foreach (var r in t.Recorders) sum += r.LastValue;
                t.Values.Add(t.Bytes ? sum / 1024.0 : sum / 1_000_000.0);
            }
        }

        public PhaseStats EndPhase()
        {
            var stats = current;
            current = null;
            if (stats == null) return null;
            foreach (var t in tracks) stats.Tracks[t.Label] = new MarkerStats(t.Values);
            return stats;
        }

        public void Dispose()
        {
            foreach (var t in tracks)
            {
                foreach (var r in t.Recorders) r.Dispose();
            }
            tracks.Clear();
        }
    }
}
