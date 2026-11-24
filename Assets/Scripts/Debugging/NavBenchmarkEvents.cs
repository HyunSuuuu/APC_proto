using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using APC.Data;
using APC.Navigation;
using APC.Site;
using APC.Staff;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

namespace APC.Debugging
{
    /// <summary>측정 도구의 0 보정 · 2 길 1개 · 3 해금 · 4 레일 단계 (2026-11-24).</summary>
    public sealed partial class NavBenchmark
    {
        readonly struct RailSpot
        {
            public readonly int Room;
            public readonly Vector2 Center;
            public readonly float Yaw;
            public readonly NavBox Box;

            public RailSpot(int room, Vector2 center, float yaw, NavBox box)
            {
                Room = room;
                Center = center;
                Yaw = yaw;
                Box = box;
            }
        }

        PhaseStats window;
        readonly List<GameObject> rails = new();

        // ── 0. 보정 ──

        IEnumerator Calibrate()
        {
            var service = StaffService.Instance;
            if (service == null)
            {
                sections.Add("## 0. 보정 (실제 알바)\n\n- StaffService가 없어 건너뜀 (기본 길 요청 간격)");
                yield break;
            }
            var roles = new[] { StaffRole.Unload, StaffRole.Sort, StaffRole.Pick, StaffRole.Forklift };
            var counts = config.CalibrateRoles;
            var total = 0;
            var wait = new WaitForSecondsRealtime(config.CalibrateSpawnInterval);
            for (var i = 0; i < roles.Length && i < counts.Length; i++)
            {
                for (var n = 0; n < counts[i]; n++)
                {
                    if (service.Spawn(roles[i]) != null) total++;
                    yield return wait; // 한 프레임에 몰아 뽑지 않는다
                }
            }
            yield return Drive(null, config.WarmupSeconds, false);

            int requests0 = StaffWorker.PathRequests, noPath0 = StaffWorker.NoPathCount;
            BeginProfilerLog("실제 알바");
            recorder.BeginPhase($"실제 알바 {total}명", "실제 알바");
            var start = Time.realtimeSinceStartupAsDouble;
            yield return Drive(null, config.CalibrateSeconds, true, config.ProfilerLogSeconds);
            var stats = recorder.EndPhase();
            EndProfilerLog();
            stats.Seconds = Time.realtimeSinceStartupAsDouble - start;
            stats.PathRequests = StaffWorker.PathRequests - requests0;
            stats.PathFailures = StaffWorker.NoPathCount - noPath0;
            phases.Add(stats);

            var rate = stats.PathRequests / stats.Seconds;
            if (rate > 0) repathInterval = (float)(config.AgentCount / rate);
            sections.Add($"## 0. 보정 (실제 알바)\n\n- 실제 알바 {total}명(하차 · 보관 · 상차 · 지게차 {string.Join(" · ", counts)}) {stats.Seconds:F0}초: 초당 길 요청 **{rate:F1}** (\"길이 없습니다\" {stats.PathFailures}) → 측정 알바 {config.AgentCount}명의 길 요청 간격 **{repathInterval:F2}초**\n" +
                         $"- 같은 구간 PlayerLoop 중앙 {NavBenchReport.F(stats.Median(NavBenchRecorder.PlayerLoop))}ms · AI 메인 평균 {NavBenchReport.F(stats.Mean(NavBenchRecorder.AiMain))}ms (실제 알바는 일감 찾기 비용이 섞임 — 1장 표 '실제 알바' 행)");
            Debug.Log($"[NavBench] 보정: 초당 길 요청 {rate:F1} → 간격 {repathInterval:F2}초");

            // 끄기만 한다 (지우면 들고 있던 화물이 같이 사라져 풀이 깨질 수 있음). 꺼지면 명단 · 손 목록에서 빠진다.
            foreach (var worker in service.Workers.ToList())
            {
                if (worker != null) worker.gameObject.SetActive(false);
            }
            yield return null;
        }

        // ── 2. 길 1개 ──

        void PathCost()
        {
            var random = new System.Random(config.Seed + 1);
            var pathfinder = siteNav.World.Pathfinder;
            var grid = siteNav.World.Grid;
            var snap = config.Grid.SnapCells;
            var navPath = new NavMeshPath();
            var watch = new Stopwatch();
            var points = new List<Vector3>();
            var flatPoints = new List<Vector3>();
            List<double> navTimes = new(), roomTimes = new(), flatTimes = new();
            int navOk = 0, roomOk = 0, flatOk = 0, mismatch = 0, overRatio = 0, blocked = 0, ratioCount = 0;
            double ratioSum = 0, ratioMax = 0;

            for (var i = 0; i < config.PathQueries; i++)
            {
                var a = targets[random.Next(targets.Count)];
                var b = targets[random.Next(targets.Count)];

                watch.Restart();
                var navFound = NavMesh.SamplePosition(a, out var ha, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(b, out var hb, 2f, NavMesh.AllAreas)
                               && NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, navPath) && navPath.status == NavMeshPathStatus.PathComplete;
                navTimes.Add(watch.Elapsed.TotalMilliseconds * 1000.0);
                if (navFound) navOk++;

                watch.Restart();
                var flatStatus = pathfinder.FindPathFlat(a, b, snap, flatPoints);
                flatTimes.Add(watch.Elapsed.TotalMilliseconds * 1000.0);
                var flatLength = pathfinder.LastLength;
                if (flatStatus == PathStatus.Ok) flatOk++;

                watch.Restart();
                var status = pathfinder.FindPath(a, b, snap, points);
                roomTimes.Add(watch.Elapsed.TotalMilliseconds * 1000.0);
                if (status == PathStatus.Ok) roomOk++;

                if ((flatStatus == PathStatus.Ok) != (status == PathStatus.Ok)) mismatch++;
                if (status == PathStatus.Ok && flatStatus == PathStatus.Ok && flatLength > 1f)
                {
                    var ratio = pathfinder.LastLength / flatLength;
                    ratioSum += ratio;
                    ratioCount++;
                    ratioMax = System.Math.Max(ratioMax, ratio);
                    if (ratio > config.LengthRatioLimit) overRatio++;
                }
                if (status == PathStatus.Ok && !SegmentsClear(grid, points)) blocked++;
            }

            var sb = new StringBuilder();
            sb.AppendLine("## 2. 길 1개 계산 (µs, 같은 목적지 쌍)");
            sb.AppendLine();
            sb.AppendLine($"목적지 쌍 {config.PathQueries}개 (WorkSlot 위치, 시드 {config.Seed + 1}). NavMesh는 `SamplePosition` 2번 + `CalculatePath`(동기), A*는 붙이기 포함.");
            sb.AppendLine();
            sb.AppendLine("| 방식 | 찾음 | 평균 | 중앙 | p95 | 최대 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            Row(sb, "NavMesh.CalculatePath", navOk, navTimes);
            Row(sb, "방 단위 2층 A*", roomOk, roomTimes);
            Row(sb, "맵 한 장 격자 A*", flatOk, flatTimes);
            sb.AppendLine();
            var verdict = mismatch == 0 && overRatio == 0 && blocked == 0 ? "통과" : "**실패**";
            sb.AppendLine($"- 자기 검사 {verdict}: 도달 불일치(한 장 격자 ↔ 방 2층) {mismatch} · 길이 비율 평균 {(ratioCount > 0 ? ratioSum / ratioCount : 0):F3} 최대 {ratioMax:F3} (한도 {config.LengthRatioLimit} 넘음 {overRatio}) · 막힌 칸을 지나는 구간 {blocked}");
            sections.Add(sb.ToString());
            Debug.Log($"[NavBench] 길 1개: 자기 검사 {verdict} (불일치 {mismatch}, 비율 초과 {overRatio}, 막힌 구간 {blocked})");
        }

        static void Row(StringBuilder sb, string name, int ok, List<double> times)
        {
            var sorted = times.OrderBy(t => t).ToList();
            sb.AppendLine($"| {name} | {ok}/{times.Count} | {sorted.Average():F1} | {sorted[sorted.Count / 2]:F1} | {sorted[(int)(sorted.Count * 0.95)]:F1} | {sorted[^1]:F1} |");
        }

        /// <summary>양 끝 칸이 걸을 수 있는 구간만 본다 (출발 · 도착이 막힌 칸에 붙여진 구간은 빼고).</summary>
        static bool SegmentsClear(NavGrid grid, List<Vector3> points)
        {
            for (var i = 1; i < points.Count; i++)
            {
                var a = grid.WorldToCell(points[i - 1]);
                var b = grid.WorldToCell(points[i]);
                if (!grid.IsWalkable(a.x, a.y) || !grid.IsWalkable(b.x, b.y)) continue;
                if (!grid.LineOfSight(grid.Index(a.x, a.y), grid.Index(b.x, b.y))) return false;
            }
            return true;
        }

        // ── 3 · 4. A* 해금 · 레일 ──

        IEnumerator AStarEvents()
        {
            var world = siteNav.World;
            var agents = new NavBenchAgents(NavBenchAgents.Mode.AStar, config, targets, speed, repathInterval, world);
            yield return Drive(agents, config.WarmupSeconds, false);
            var steadyPath = NavBenchReport.MedianOf(phases.Where(p => p.Kind == "A*").Select(p => p.Mean(NavBenchRecorder.Path)));
            var unlock = new List<string>();
            var rail = new List<string>();
            var room = FarRoom();
            var watch = new Stopwatch();

            // 해금 = 방 열기. 닫은 뒤(잼 안 함) 다시 열면서 잰다. 0회차는 예열(첫 호출 비용 빼기), 1 ~ Repeats회 중앙값
            foreach (var full in new[] { false, true })
            {
                var times = new List<double>();
                int maps = 0, repaths = 0;
                for (var rep = 0; rep <= config.Repeats; rep++)
                {
                    world.SetRoomOpen(room, false);
                    agents.RepathTouching(world.ChangedRooms);
                    yield return Drive(agents, 0.5f, false);
                    watch.Restart();
                    if (full)
                    {
                        world.Grid.SetRoomOpen(room, true);
                        world.RebuildAll();
                    }
                    else world.SetRoomOpen(room, true);
                    if (rep > 0) times.Add(watch.Elapsed.TotalMilliseconds);
                    maps = world.LastMapsBuilt;
                    repaths = agents.RepathTouching(world.ChangedRooms);
                    if (rep == config.Repeats) yield return Window(agents, "해금 A*");
                    else yield return Drive(agents, 0.3f, false);
                }
                unlock.Add($"| A* {(full ? "전체" : "방별")} | **{Median(times):F2}** ({Spread(times)}) | 방 {(full ? world.Grid.RoomCount : 1)} · 문 지도 {maps} | {repaths} | {Extra(window, NavBenchRecorder.Path, steadyPath)} |");
            }

            // 레일 — 범위 · 개수마다 0회차 예열 + Repeats회 중앙값
            var spots = ChooseRails(world);
            var changed = new List<int>();
            foreach (var scope in new[] { "타일별", "방별", "전체" })
            {
                foreach (var count in RailCounts(spots.Count))
                {
                    var times = new List<double>();
                    int maps = 0, repaths = 0, rooms = 0;
                    for (var rep = 0; rep <= config.Repeats; rep++)
                    {
                        PlaceRails(spots, count, false);
                        changed.Clear();
                        maps = 0;
                        watch.Restart();
                        if (scope == "전체")
                        {
                            world.RebuildAll();
                            changed.AddRange(world.ChangedRooms);
                            maps = world.LastMapsBuilt;
                        }
                        else
                        {
                            for (var i = 0; i < count; i++)
                            {
                                var bounds = spots[i].Box.Bounds(config.Grid.AgentRadius);
                                if (scope == "타일별")
                                {
                                    world.RebuildTiles(bounds);
                                    changed.AddRange(world.ChangedRooms);
                                    maps += world.LastMapsBuilt;
                                    continue;
                                }
                                foreach (var r in RoomsTouched(world.Grid, bounds))
                                {
                                    world.RebuildRoom(r);
                                    changed.Add(r);
                                    maps += world.LastMapsBuilt;
                                }
                            }
                        }
                        if (rep > 0) times.Add(watch.Elapsed.TotalMilliseconds);
                        var distinct = changed.Distinct().ToList();
                        rooms = distinct.Count;
                        repaths = agents.RepathTouching(distinct);
                        if (rep == config.Repeats) yield return Window(agents, "레일 A*");

                        RemoveRails();
                        yield return null; // 지운 충돌체가 물리에서 빠지도록
                        world.RebuildAll();
                        agents.RepathTouching(distinct);
                        yield return Drive(agents, 0.3f, false);
                    }
                    rail.Add($"| A* {scope} | {count} | **{Median(times):F2}** ({Spread(times)}) | 방 {rooms} · 문 지도 {maps} | {repaths} | {Extra(window, NavBenchRecorder.Path, steadyPath)} |");
                }
            }
            agents.Dispose();
            AddEventSections(unlock, rail, spots.Count, room);
            yield return null;
        }

        // ── 3 · 4. NavMesh 해금 · 레일 ──

        IEnumerator NavMeshEvents()
        {
            var agents = new NavBenchAgents(NavBenchAgents.Mode.NavMesh, config, targets, speed, repathInterval, siteNav.World);
            yield return Drive(agents, config.WarmupSeconds, false);
            var steadyAi = NavBenchReport.MedianOf(phases.Where(p => p.Kind == "NavMesh").Select(p => p.Mean(NavBenchRecorder.AiMain)));
            var room = FarRoom();
            var floors = FloorsOf(room);
            var watch = new Stopwatch();

            // 해금 (동기 전체 = 지금 게임): 0회차 예열 + Repeats회 중앙값 · 비동기 UpdateNavMesh는 참고로 한 번
            if (floors.Count > 0)
            {
                var times = new List<double>();
                for (var rep = 0; rep <= config.Repeats; rep++)
                {
                    SetEnabled(floors, false);
                    surface.BuildNavMesh();
                    yield return Drive(agents, 0.5f, false);
                    SetEnabled(floors, true);
                    watch.Restart();
                    surface.BuildNavMesh();
                    if (rep > 0) times.Add(watch.Elapsed.TotalMilliseconds);
                    if (rep == config.Repeats) yield return Window(agents, "해금 NavMesh");
                    else yield return Drive(agents, 0.3f, false);
                }
                navUnlock.Add($"| NavMesh 전체 동기 `BuildNavMesh` (지금 게임) | **{Median(times):F2}** ({Spread(times)}) | 부지 전체 | 자동 | {Extra(window, NavBenchRecorder.AiMain, steadyAi)} |");

                SetEnabled(floors, false);
                surface.BuildNavMesh();
                yield return Drive(agents, 1f, false);
                SetEnabled(floors, true);
                recorder.BeginPhase("해금 NavMesh 비동기", "이벤트");
                var start = Time.realtimeSinceStartupAsDouble;
                var operation = surface.UpdateNavMesh(surface.navMeshData);
                var frames = 0;
                while (!operation.isDone && frames < 600)
                {
                    yield return null;
                    recorder.Sample();
                    agents.Tick(Time.deltaTime);
                    frames++;
                }
                yield return null;
                recorder.Sample();
                window = recorder.EndPhase();
                var wall = (Time.realtimeSinceStartupAsDouble - start) * 1000.0;
                window.Tracks.TryGetValue(NavBenchRecorder.PlayerLoop, out var loop);
                window.Tracks.TryGetValue(NavBenchRecorder.NavMeshBuild, out var build);
                navUnlock.Add($"| NavMesh 비동기 `UpdateNavMesh` (참고, 1회) | 가장 긴 프레임 {loop.Max:F2} · 끝날 때까지 {frames}프레임 · {wall:F0}ms | 바뀐 타일 | 자동 | 굽기 마커 합 {build.Sum:F2}ms(모든 스레드) |");
            }
            else navUnlock.Add("| NavMesh | 방 바닥 충돌체를 못 찾아 건너뜀 | | | |");

            // 레일: carving(지금 선반 방식) · 전체 굽기 — 0회차 예열 + Repeats회 중앙값
            var spots = ChooseRails(siteNav.World);
            foreach (var count in RailCounts(spots.Count))
            {
                List<double> carveAi = new(), carveSum = new(), carveMax = new();
                for (var rep = 0; rep <= config.Repeats; rep++)
                {
                    PlaceRails(spots, count, true);
                    yield return Window(agents, "레일 carving");
                    if (rep > 0)
                    {
                        window.Tracks.TryGetValue(NavBenchRecorder.Carving, out var carving);
                        window.Tracks.TryGetValue(NavBenchRecorder.PlayerLoop, out var carveLoop);
                        carveAi.Add(ExtraValue(window, NavBenchRecorder.AiMain, steadyAi));
                        carveSum.Add(carving.Sum);
                        carveMax.Add(carveLoop.Max);
                    }
                    RemoveRails();
                    yield return Drive(agents, 0.5f, false);
                }
                navRail.Add($"| NavMesh carving (지금 선반 방식) | {count} | 다음 프레임들에 나눠서 (가장 긴 프레임 {Median(carveMax):F2}) | 바뀐 타일 | 자동 | AI 메인 {Median(carveAi):F2} · Carving 합 {Median(carveSum):F2}(모든 스레드) |");

                var times = new List<double>();
                for (var rep = 0; rep <= config.Repeats; rep++)
                {
                    PlaceRails(spots, count, false);
                    watch.Restart();
                    surface.BuildNavMesh();
                    if (rep > 0) times.Add(watch.Elapsed.TotalMilliseconds);
                    if (rep == config.Repeats) yield return Window(agents, "레일 NavMesh 전체");
                    RemoveRails();
                    yield return null;
                    surface.BuildNavMesh();
                    yield return Drive(agents, 0.3f, false);
                }
                navRail.Add($"| NavMesh 전체 동기 `BuildNavMesh` | {count} | **{Median(times):F2}** ({Spread(times)}) | 부지 전체 | 자동 | {Extra(window, NavBenchRecorder.AiMain, steadyAi)} |");
            }
            agents.Dispose();
            surface.BuildNavMesh(); // 원래 상태로
            AddNavMeshSections(spots.Count);
            yield return null;
        }

        readonly List<string> navUnlock = new();
        readonly List<string> navRail = new();
        readonly List<string> astarUnlock = new();
        readonly List<string> astarRail = new();
        int railSpots;
        int unlockRoom;

        void AddEventSections(List<string> unlock, List<string> rail, int spotCount, int room)
        {
            astarUnlock.AddRange(unlock);
            astarRail.AddRange(rail);
            railSpots = spotCount;
            unlockRoom = room;
        }

        /// <summary>A* · NavMesh 해금 · 레일 표를 합쳐 보고서 절로 넣는다 (NavMesh 단계가 끝난 뒤, 또는 Finish 전에 한 번).</summary>
        void AddNavMeshSections(int spotCount)
        {
            var cell = siteNav.SiteCellOf(unlockRoom);
            var sb = new StringBuilder();
            sb.AppendLine("## 3. 해금 (방 하나 열기)");
            sb.AppendLine();
            sb.AppendLine($"플레이어에서 가장 먼 방 (칸 열 {cell.x} · 행 {cell.y})을 닫았다가 여는 비용. 굽기 = 메인 스레드가 멈춘 시간(스톱워치), 예열 1회 뒤 {config.Repeats}회 중앙값 (괄호 = 최소~최대). 이후 {config.EventWindowFrames}프레임 추가 = 마지막 회의 그 프레임들 길찾기 마커 합 − 평소 평균 × 프레임 수.");
            sb.AppendLine();
            sb.AppendLine($"| 방식 | 굽기 ms | 다시 구운 범위 | 다시 길 찾은 알바 | 이후 {config.EventWindowFrames}프레임 추가 ms |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var line in navUnlock.Concat(astarUnlock)) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine("## 4. 레일 설치");
            sb.AppendLine();
            sb.AppendLine($"레일 = 지나갈 수 없는 {config.RailSize.x}×{config.RailSize.z}m 상자(높이 {config.RailSize.y}m), 방 안 빈 자리(장애물 · 서는 위치와 안 겹침), 1개 = 방 1개, {spotCount}개 = 서로 다른 방 {spotCount}개에 하나씩 한 번에.");
            sb.AppendLine();
            sb.AppendLine($"| 방식 | 레일 | 굽기 ms | 다시 구운 범위 | 다시 길 찾은 알바 | 이후 {config.EventWindowFrames}프레임 추가 ms |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var line in navRail.Concat(astarRail)) sb.AppendLine(line);
            sections.Add(sb.ToString());
        }

        // ── 도움 ──

        IEnumerator Window(NavBenchAgents agents, string name)
        {
            recorder.BeginPhase(name, "이벤트");
            for (var i = 0; i < config.EventWindowFrames; i++)
            {
                yield return null;
                recorder.Sample(); // 직전 프레임 값 — 첫 번째가 사건이 일어난 프레임
                agents?.Tick(Time.deltaTime);
            }
            window = recorder.EndPhase();
        }

        static string Extra(PhaseStats stats, string track, double steadyMean) => NavBenchReport.F(ExtraValue(stats, track, steadyMean), 2);

        /// <summary>창 프레임들의 마커 합 − 평소 평균 × 프레임 수 (ms). 마커가 없으면 NaN.</summary>
        static double ExtraValue(PhaseStats stats, string track, double steadyMean)
        {
            if (!stats.Tracks.TryGetValue(track, out var s) || !s.Valid) return double.NaN;
            return s.Sum - (double.IsNaN(steadyMean) ? 0 : steadyMean) * stats.Frames;
        }

        static double Median(List<double> values) => values.Count == 0 ? double.NaN : values.OrderBy(v => v).ElementAt(values.Count / 2);

        static string Spread(List<double> values) => values.Count == 0 ? "-" : $"{values.Min():F2}~{values.Max():F2}";

        static IEnumerable<int> RailCounts(int spots) => spots <= 1 ? new[] { spots } : new[] { 1, spots };

        /// <summary>플레이어에서 가장 먼 열린 방 (플레이어가 없으면 격자 가운데에서).</summary>
        int FarRoom()
        {
            var grid = siteNav.World.Grid;
            var player = GameObject.Find("Player");
            var from = player != null ? player.transform.position : grid.CellCenter(grid.Index(grid.Width / 2, grid.Height / 2));
            int best = 0;
            var bestDistance = -1f;
            for (var room = 0; room < grid.RoomCount; room++)
            {
                if (!grid.IsRoomOpen(room)) continue;
                var r = grid.RoomRect(room);
                var center = grid.CellCenter(grid.Index(r.xMin + r.width / 2, r.yMin + r.height / 2));
                var distance = (center - from).sqrMagnitude;
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                best = room;
            }
            return best;
        }

        /// <summary>그 방 모듈의 바닥 충돌체 (경계 위 0.3m 아래). NavMesh 해금 흉내에 끄고 켠다.</summary>
        List<Collider> FloorsOf(int room)
        {
            var list = new List<Collider>();
            var module = SiteGrid.Instance.ModuleAt(siteNav.SiteCellOf(room));
            if (module == null) return list;
            foreach (var c in module.GetComponentsInChildren<Collider>())
            {
                if (c.enabled && !c.isTrigger && c.bounds.max.y < 0.3f) list.Add(c);
            }
            return list;
        }

        static void SetEnabled(List<Collider> colliders, bool enabled)
        {
            foreach (var c in colliders) c.enabled = enabled;
        }

        static IEnumerable<int> RoomsTouched(NavGrid grid, Rect bounds)
        {
            var a = grid.WorldToCell(new Vector3(bounds.xMin, 0f, bounds.yMin));
            var b = grid.WorldToCell(new Vector3(bounds.xMax, 0f, bounds.yMax));
            var cpr = grid.CellsPerRoom;
            for (var rz = Mathf.Max(0, a.y / cpr); rz <= Mathf.Min(grid.RoomsZ - 1, b.y / cpr); rz++)
            for (var rx = Mathf.Max(0, a.x / cpr); rx <= Mathf.Min(grid.RoomsX - 1, b.x / cpr); rx++)
                yield return grid.RoomIndex(rx, rz);
        }

        /// <summary>
        /// 시드로 서로 다른 열린 방 RailBurst개에서 레일 자리를 고른다: 키운 모양이 그 방 안에 들고, 기존 장애물 · 서는 위치와 안 겹침 (방마다 RailTries번 시도).
        /// 같은 시드 · 같은 장애물이면 A* · NavMesh 단계가 같은 자리를 쓴다.
        /// </summary>
        List<RailSpot> ChooseRails(NavWorld world)
        {
            var random = new System.Random(config.Seed + 2);
            var grid = world.Grid;
            var inflate = config.Grid.AgentRadius;
            var size = config.RailSize;
            var half = new Vector2(size.x * 0.5f, size.z * 0.5f);
            var rooms = Enumerable.Range(0, grid.RoomCount).Where(grid.IsRoomOpen).OrderBy(_ => random.Next()).ToList();
            var spots = new List<RailSpot>();
            var boxes = new List<NavBox>();
            var roomSize = grid.CellsPerRoom * grid.CellSize;
            foreach (var room in rooms)
            {
                if (spots.Count >= config.RailBurst) break;
                var rect = grid.RoomRect(room);
                var origin = new Vector2(grid.Origin.x + rect.xMin * grid.CellSize, grid.Origin.y + rect.yMin * grid.CellSize);
                boxes.Clear();
                siteNav.Collect(new Rect(origin, new Vector2(roomSize, roomSize)), boxes);
                for (var t = 0; t < config.RailTries; t++)
                {
                    var yaw = random.Next(2) * 90f;
                    var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    var extent = (yaw == 0f ? half : new Vector2(half.y, half.x)) + Vector2.one * inflate;
                    if (extent.x * 2f >= roomSize || extent.y * 2f >= roomSize) break;
                    var center = new Vector2(origin.x + extent.x + (float)random.NextDouble() * (roomSize - extent.x * 2f),
                        origin.y + extent.y + (float)random.NextDouble() * (roomSize - extent.y * 2f));
                    var box = new NavBox(center, half, new Vector2(right.x, right.z));
                    var area = box.Bounds(inflate);
                    if (boxes.Any(b => b.Bounds(0f).Overlaps(area))) continue;
                    if (targets.Any(p => box.Contains(new Vector2(p.x, p.z), inflate))) continue;
                    spots.Add(new RailSpot(room, center, yaw, box));
                    break;
                }
            }
            if (spots.Count < config.RailBurst) Debug.LogWarning($"[NavBench] 레일 자리를 {spots.Count}곳만 찾았습니다 (목표 {config.RailBurst})");
            return spots;
        }

        /// <summary>레일 count개를 Site 아래에 놓는다. carve = 선반처럼 NavMeshObstacle(carving) + 굽기 제외, 아니면 일반 충돌체(굽기에 들어감).</summary>
        void PlaceRails(List<RailSpot> spots, int count, bool carve)
        {
            var size = config.RailSize;
            for (var i = 0; i < count && i < spots.Count; i++)
            {
                var go = new GameObject($"BenchRail{i:00}");
                go.transform.SetPositionAndRotation(new Vector3(spots[i].Center.x, size.y * 0.5f, spots[i].Center.y), Quaternion.Euler(0f, spots[i].Yaw, 0f));
                go.transform.SetParent(siteRoot, true);
                go.AddComponent<BoxCollider>().size = size;
                if (carve)
                {
                    var obstacle = go.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = size;
                    obstacle.carving = true;
                    obstacle.carvingTimeToStationary = 0f; // 놓자마자 구멍 (기본 0.5초 기다림)
                    go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                }
                rails.Add(go);
            }
        }

        void RemoveRails()
        {
            foreach (var go in rails)
            {
                if (go != null) Destroy(go);
            }
            rails.Clear();
        }
    }
}
