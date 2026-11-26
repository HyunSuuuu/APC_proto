using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 길찾기 코어 자기 검사 (테스트 프레임워크 대신, 2026-11-24). Unity MCP `execute_code`나 측정 도구가 <see cref="Run"/>을 부른다.
    /// 모두 통과면 "OK n", 아니면 "FAIL" 다음 줄마다 실패한 케이스. 케이스는 작은 가짜 격자에서 돈다 (씬 · 게임 코드 없음).
    /// </summary>
    public static class NavSelfTest
    {
        static readonly List<string> failures = new();
        static int count;

        public static string Run()
        {
            failures.Clear();
            count = 0;
            GridCases();
            AStarCases();
            GateCases();
            PathCases();
            WorldCases();
            CrowdCases();
            return failures.Count == 0 ? $"OK {count}" : "FAIL\n" + string.Join("\n", failures);
        }

        static void Case(string name, Func<bool> body)
        {
            count++;
            try
            {
                if (!body()) failures.Add(name);
            }
            catch (Exception e)
            {
                failures.Add($"{name}: {e.GetType().Name} {e.Message}");
            }
        }

        // ── 공용 ──

        /// <summary>칸 0.5m · 방 20칸 · 타일 4칸, 원점 (0, 0). roomsX × roomsZ 방, open이 비면 전부 열림.</summary>
        static NavGrid Grid(int roomsX, int roomsZ, params int[] closedRooms)
        {
            var grid = new NavGrid(Vector2.zero, roomsX, roomsZ, 0.5f, 20, 4);
            for (var r = 0; r < grid.RoomCount; r++) grid.SetRoomOpen(r, Array.IndexOf(closedRooms, r) < 0);
            return grid;
        }

        static RectInt All(NavGrid grid) => new(0, 0, grid.Width, grid.Height);

        static int Paint(NavGrid grid, float inflate, params NavBox[] boxes) => grid.Rasterize(All(grid), boxes, inflate, out _);

        /// <summary>칸 하나만 덮는 상자 (칸 중심, 반크기 0.2).</summary>
        static NavBox CellBox(NavGrid grid, int x, int z)
        {
            var c = grid.CellCenter(grid.Index(x, z));
            return new NavBox(new Vector2(c.x, c.z), new Vector2(0.2f, 0.2f), Vector2.right);
        }

        sealed class ListSource : INavObstacleSource
        {
            public readonly List<NavBox> Boxes = new();

            public void Collect(Rect worldXZ, List<NavBox> output)
            {
                foreach (var box in Boxes)
                {
                    if (box.Bounds(0f).Overlaps(worldXZ)) output.Add(box);
                }
            }
        }

        // ── Task 1: 격자 · 상자 ──

        static void GridCases()
        {
            Case("WorldToCell", () => Grid(2, 1).WorldToCell(new Vector3(0.74f, 0f, 9.9f)) == new Vector2Int(1, 19));

            Case("ClosedRoomBlocked", () =>
            {
                var g = Grid(2, 1, 1);
                Paint(g, 0.5f);
                return !g.IsWalkable(g.Index(25, 5)) && g.IsWalkable(g.Index(5, 5));
            });

            Case("BoxInflate", () =>
            {
                // 중심 (5, 5) 반크기 0.8 + 키우기 0.5 → |d| ≤ 1.3 막힘. 칸 중심 3.75 · 6.25는 안, 3.25 · 6.75는 밖
                var g = Grid(2, 1);
                Paint(g, 0.5f, new NavBox(new Vector2(5f, 5f), new Vector2(0.8f, 0.8f), Vector2.right));
                return !g.IsWalkable(g.Index(7, 7)) && !g.IsWalkable(g.Index(12, 10))
                       && g.IsWalkable(g.Index(13, 10)) && g.IsWalkable(g.Index(6, 10));
            });

            Case("RotatedBoxFootprint", () =>
            {
                // 45° 막대 (3 × 0.5): 축 방향 칸 (11, 11)은 막히고, 축에 수직으로 1m 떨어진 칸 (8, 11)은 열림 (경계 상자였다면 막힘)
                var g = Grid(2, 1);
                Paint(g, 0f, new NavBox(new Vector2(5f, 5f), new Vector2(1.5f, 0.25f), new Vector2(1f, 1f)));
                return !g.IsWalkable(g.Index(11, 11)) && g.IsWalkable(g.Index(8, 11));
            });

            Case("EdgeChangedFlag", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f);
                var border = g.Rasterize(All(g), new[] { new NavBox(new Vector2(9.75f, 5f), new Vector2(0.2f, 1f), Vector2.right) }, 0f, out var borderEdge);
                Paint(g, 0f);
                var middle = g.Rasterize(All(g), new[] { CellBox(g, 10, 10) }, 0f, out var middleEdge);
                return border > 0 && borderEdge && middle == 1 && !middleEdge;
            });

            Case("NearestWalkable", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, new NavBox(new Vector2(5.25f, 5.25f), new Vector2(0.6f, 0.6f), Vector2.right)); // 칸 9~11 × 9~11 막힘
                if (!g.TryNearestWalkable(10, 10, 4, out var index) || !g.IsWalkable(index)) return false;
                var x = index % g.Width;
                var z = index / g.Width;
                return Mathf.Max(Mathf.Abs(x - 10), Mathf.Abs(z - 10)) == 2;
            });

            Case("LineOfSightBlocked", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, new NavBox(new Vector2(5f, 5f), new Vector2(0.3f, 5f), Vector2.right)); // 칸 x 9 · 10 세로 벽
                return !g.LineOfSight(g.Index(2, 10), g.Index(17, 10)) && g.LineOfSight(g.Index(2, 2), g.Index(7, 8));
            });

            Case("LineOfSightCorner", () =>
            {
                // 대각선으로 맞닿은 두 막힌 칸 (5, 6) · (6, 5) 사이 꼭짓점을 지나는 시선은 막힘
                var g = Grid(2, 1);
                Paint(g, 0f, CellBox(g, 5, 6), CellBox(g, 6, 5));
                return !g.LineOfSight(g.Index(5, 5), g.Index(6, 6)) && g.LineOfSight(g.Index(5, 5), g.Index(4, 4));
            });
        }

        // ── Task 2: 사각형 범위 A* ──

        static bool AllWalkable(NavGrid g, List<int> cells)
        {
            foreach (var c in cells)
            {
                if (!g.IsWalkable(c)) return false;
            }
            return true;
        }

        static void AStarCases()
        {
            var cells = new List<int>();

            Case("AStarStraight", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f);
                int a = g.Index(5, 10), b = g.Index(25, 10);
                return new GridAStar(g).FindPath(All(g), a, b, cells) && cells.Count == 21 && cells[0] == a && cells[^1] == b;
            });

            Case("AStarWallGap", () =>
            {
                // 칸 x 9 · 10 세로 벽, z 14 · 15만 틈
                var g = Grid(2, 1);
                Paint(g, 0f, new NavBox(new Vector2(5f, 3.5f), new Vector2(0.3f, 3.5f), Vector2.right),
                    new NavBox(new Vector2(5f, 9f), new Vector2(0.3f, 1f), Vector2.right));
                if (!new GridAStar(g).FindPath(All(g), g.Index(2, 5), g.Index(17, 5), cells)) return false;
                var throughGap = false;
                foreach (var c in cells)
                {
                    int x = c % g.Width, z = c / g.Width;
                    if (x is 9 or 10 && z is 14 or 15) throughGap = true;
                }
                return throughGap && AllWalkable(g, cells);
            });

            Case("AStarNoPath", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, new NavBox(new Vector2(5f, 5f), new Vector2(0.3f, 5f), Vector2.right));
                return !new GridAStar(g).FindPath(All(g), g.Index(2, 10), g.Index(17, 10), cells);
            });

            Case("AStarNoCornerCut", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, CellBox(g, 5, 6), CellBox(g, 6, 5));
                int a = g.Index(5, 5), b = g.Index(6, 6);
                if (!new GridAStar(g).FindPath(All(g), a, b, cells)) return false;
                for (var i = 1; i < cells.Count; i++)
                {
                    if ((cells[i - 1] == a && cells[i] == b) || (cells[i - 1] == b && cells[i] == a)) return false;
                }
                return cells.Count > 2 && AllWalkable(g, cells);
            });

            Case("AStarBounds", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f);
                return !new GridAStar(g).FindPath(g.RoomRect(0), g.Index(5, 5), g.Index(25, 5), cells);
            });
        }

        // ── Task 3: 방 문 · 문 지도 ──

        static RoomGates Gates(NavGrid g)
        {
            var gates = new RoomGates(g);
            gates.BuildAll();
            return gates;
        }

        static List<int> GateCells(RoomGates gates, int room)
        {
            var list = new List<int>();
            for (var i = 0; i < gates.GateCount(room); i++) list.Add(gates.GateCell(room, i));
            list.Sort();
            return list;
        }

        static void GateCases()
        {
            Case("GateMiddle", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f);
                var gates = Gates(g);
                var peer = gates.GatePeer(0, 0);
                return gates.GateCount(0) == 1 && gates.GateCell(0, 0) == g.Index(19, 10)
                       && peer / gates.MaxGatesPerRoom == 1 && gates.GateCell(1, peer % gates.MaxGatesPerRoom) == g.Index(20, 10);
            });

            Case("EdgeWithTwoRuns", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, CellBox(g, 19, 10)); // 변 가운데 칸 하나 막힘 → 구간 z 0~9 · 11~19
                var gates = Gates(g);
                return gates.GateCount(0) == 2 && g.IsWalkable(gates.GateCell(0, 0)) && g.IsWalkable(gates.GateCell(0, 1))
                       && gates.GateCount(1) == 2;
            });

            Case("ClosedNeighborNoGate", () =>
            {
                var g = Grid(2, 1, 1);
                Paint(g, 0f);
                return Gates(g).GateCount(0) == 0;
            });

            Case("GateMapZeroAtGate", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, CellBox(g, 5, 5));
                var gates = Gates(g);
                var gate = gates.GateCell(0, 0);
                return gates.Distance(0, 0, gate) == 0 && gates.Distance(0, 0, g.Index(0, 0)) > 0
                       && gates.Distance(0, 0, g.Index(5, 5)) == ushort.MaxValue
                       && gates.Distance(0, 0, g.Index(25, 5)) == ushort.MaxValue; // 다른 방 칸
            });

            Case("StepLeadsToGate", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, CellBox(g, 10, 10), CellBox(g, 11, 10), CellBox(g, 12, 10));
                var gates = Gates(g);
                var cell = g.Index(0, 0);
                for (var i = 0; i < 400; i++)
                {
                    var next = gates.StepTowardGate(0, 0, cell);
                    if (next < 0) break;
                    if (!g.IsWalkable(next)) return false;
                    cell = next;
                }
                return cell == gates.GateCell(0, 0);
            });

            Case("RebuildRoomMatchesAll", () =>
            {
                var g = Grid(3, 1);
                Paint(g, 0f);
                var gates = Gates(g);
                Paint(g, 0f, CellBox(g, 20, 10), CellBox(g, 30, 5)); // 방1 서쪽 변 칸 + 방1 안쪽 칸
                gates.RebuildRoom(1);
                var fresh = Gates(g);
                for (var room = 0; room < 3; room++)
                {
                    var a = GateCells(gates, room);
                    var b = GateCells(fresh, room);
                    if (a.Count != b.Count) return false;
                    for (var i = 0; i < a.Count; i++)
                    {
                        if (a[i] != b[i]) return false;
                    }
                }
                // 이웃(방0) 문 지도도 새로 만든 것과 같아야 한다
                for (var i = 0; i < gates.GateCount(0); i++)
                {
                    if (gates.Distance(0, i, g.Index(0, 0)) != fresh.Distance(0, i, g.Index(0, 0))) return false;
                }
                return gates.GateCount(0) == 2;
            });

            Case("PeersStayValidTwoRoomsAway", () =>
            {
                // 방0을 다시 구울 때 이웃 방1의 문 목록이 바뀌면(서쪽 문 1 → 2개) 방2가 가리키는 방1 문 번호도 맞아야 한다
                var g = Grid(3, 1);
                Paint(g, 0f);
                var gates = Gates(g);
                Paint(g, 0f, CellBox(g, 20, 10));
                gates.RebuildRoom(0);
                for (var room = 0; room < 3; room++)
                {
                    for (var i = 0; i < gates.GateCount(room); i++)
                    {
                        var peer = gates.GatePeer(room, i);
                        if (peer < 0) return false;
                        var peerCell = gates.GateCell(peer / gates.MaxGatesPerRoom, peer % gates.MaxGatesPerRoom);
                        var cell = gates.GateCell(room, i);
                        if (Mathf.Abs(peerCell % g.Width - cell % g.Width) + Mathf.Abs(peerCell / g.Width - cell / g.Width) != 1) return false;
                    }
                }
                return true;
            });
        }

        // ── Task 4: 방 2층 길찾기 ──

        const int Snap = 4; // 2m

        static RoomPathfinder Finder(NavGrid g) => new(g, Gates(g));

        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static bool SegmentsClear(NavGrid g, List<Vector3> waypoints)
        {
            for (var i = 1; i < waypoints.Count; i++)
            {
                var a = g.WorldToCell(waypoints[i - 1]);
                var b = g.WorldToCell(waypoints[i]);
                if (!g.LineOfSight(g.Index(a.x, a.y), g.Index(b.x, b.y))) return false;
            }
            return true;
        }

        static void PathCases()
        {
            var points = new List<Vector3>();

            Case("PathAcrossRooms", () =>
            {
                var g = Grid(3, 1);
                Paint(g, 0f);
                return Finder(g).FindPath(new Vector3(1f, 0f, 5f), new Vector3(29f, 0f, 5f), Snap, points) == PathStatus.Ok && points.Count == 2;
            });

            Case("SnapOutOfObstacle", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0.5f, new NavBox(new Vector2(15f, 5f), new Vector2(0.8f, 0.8f), Vector2.right));
                var to = new Vector3(15f, 0f, 5f);
                if (Finder(g).FindPath(new Vector3(2f, 0f, 5f), to, Snap, points) != PathStatus.Ok) return false;
                var last = g.WorldToCell(points[^1]);
                return g.IsWalkable(last.x, last.y) && FlatDistance(points[^1], to) <= 2.1f;
            });

            Case("SplitRoomDetour", () =>
            {
                // 3 × 2 방, 방1(칸 x 20~39, z 0~19)을 z 9 · 10 벽이 가로로 가름 → 방1 안 남쪽 → 북쪽은 이웃 방으로 돌아야 함
                var g = Grid(3, 2);
                Paint(g, 0f, new NavBox(new Vector2(15f, 5f), new Vector2(5f, 0.3f), Vector2.right));
                var finder = Finder(g);
                var from = new Vector3(15.25f, 0f, 1.75f);
                var to = new Vector3(15.25f, 0f, 8.25f);
                return finder.FindPath(from, to, Snap, points) == PathStatus.Ok && finder.LastLength > FlatDistance(from, to) + 1f
                       && SegmentsClear(g, points);
            });

            Case("ClosedRoomUnreachable", () =>
            {
                var g = Grid(2, 1, 1);
                Paint(g, 0f);
                return Finder(g).FindPath(new Vector3(2f, 0f, 5f), new Vector3(15f, 0f, 5f), Snap, points) == PathStatus.NoGoal;
            });

            Case("NoPathWhenWalled", () =>
            {
                var g = Grid(2, 1);
                Paint(g, 0f, new NavBox(new Vector2(9.75f, 5f), new Vector2(0.2f, 5f), Vector2.right)); // 방0 동쪽 변 전체
                return Finder(g).FindPath(new Vector3(2f, 0f, 5f), new Vector3(15f, 0f, 5f), Snap, points) == PathStatus.NoPath;
            });

            Case("HierarchicalMatchesFlat", () =>
            {
                // 4 × 4 방(40m), 시드 1234 무작위 상자 40개 · 무작위 쌍 200개
                var g = Grid(4, 4);
                var random = new System.Random(1234);
                var boxes = new NavBox[40];
                for (var i = 0; i < boxes.Length; i++)
                {
                    var angle = (float)(random.NextDouble() * Math.PI);
                    boxes[i] = new NavBox(new Vector2((float)random.NextDouble() * 40f, (float)random.NextDouble() * 40f),
                        new Vector2(0.3f + (float)random.NextDouble() * 1.2f, 0.3f + (float)random.NextDouble() * 1.2f),
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                }
                Paint(g, 0.5f, boxes);
                var finder = Finder(g);
                var flat = new List<Vector3>();
                for (var pair = 0; pair < 200; pair++)
                {
                    var a = g.CellCenter(random.Next(g.Width * g.Height));
                    var b = g.CellCenter(random.Next(g.Width * g.Height));
                    if (!g.IsWalkable(g.Index(g.WorldToCell(a).x, g.WorldToCell(a).y)) || !g.IsWalkable(g.Index(g.WorldToCell(b).x, g.WorldToCell(b).y))) continue;
                    var flatStatus = finder.FindPathFlat(a, b, 0, flat);
                    var flatLength = finder.LastLength;
                    var status = finder.FindPath(a, b, 0, points);
                    if ((flatStatus == PathStatus.Ok) != (status == PathStatus.Ok)) return false;
                    if (status != PathStatus.Ok) continue;
                    // 1.4 임시: 문이 변 가운데 하나라 대각선 길은 두 변 가운데를 거친다 (2026-11-24 이 케이스 최대 1.33 · 평균 1.02)
                    if (flatLength > 1f ? finder.LastLength > flatLength * 1.4f : finder.LastLength > flatLength + 1f) return false;
                    if (!SegmentsClear(g, points) || !SegmentsClear(g, flat)) return false;
                }
                return true;
            });
        }

        // ── Task 5: 다시 굽기 범위 ──

        static NavWorld World(ListSource source, int roomsX, int roomsZ, params int[] closedRooms)
        {
            var world = new NavWorld(Grid(roomsX, roomsZ, closedRooms), source, 0.5f);
            world.RebuildAll();
            return world;
        }

        /// <summary>걸을 수 있는 칸 · 방마다 문 칸이 모두 같은가.</summary>
        static bool SameWorld(NavWorld a, NavWorld b)
        {
            var n = a.Grid.Width * a.Grid.Height;
            for (var i = 0; i < n; i++)
            {
                if (a.Grid.IsWalkable(i) != b.Grid.IsWalkable(i)) return false;
            }
            for (var room = 0; room < a.Grid.RoomCount; room++)
            {
                var x = GateCells(a.Gates, room);
                var y = GateCells(b.Gates, room);
                if (x.Count != y.Count) return false;
                for (var i = 0; i < x.Count; i++)
                {
                    if (x[i] != y[i]) return false;
                }
            }
            return true;
        }

        static void WorldCases()
        {
            var points = new List<Vector3>();
            var rail = new NavBox(new Vector2(15f, 5f), new Vector2(0.5f, 3f), Vector2.right); // 방1 안 세로 막대

            Case("TilesBlockNewBox", () =>
            {
                var source = new ListSource();
                var world = World(source, 2, 1);
                Vector3 from = new(12f, 0f, 5f), to = new(18f, 0f, 5f);
                if (world.Pathfinder.FindPath(from, to, Snap, points) != PathStatus.Ok) return false;
                var before = world.Pathfinder.LastLength;
                source.Boxes.Add(rail);
                world.RebuildTiles(rail.Bounds(0.5f));
                return world.ChangedRooms.Count == 1 && world.ChangedRooms[0] == 1
                       && world.Pathfinder.FindPath(from, to, Snap, points) == PathStatus.Ok && world.Pathfinder.LastLength > before + 0.5f;
            });

            Case("RoomEqualsAll", () =>
            {
                var source = new ListSource();
                var world = World(source, 3, 1);
                source.Boxes.Add(rail);
                source.Boxes.Add(new NavBox(new Vector2(10f, 5f), new Vector2(0.3f, 1f), Vector2.right)); // 방0 · 방1 경계에 걸침
                world.RebuildRoom(1);
                world.RebuildRoom(0);
                return SameWorld(world, World(source, 3, 1));
            });

            Case("TilesEqualsRoom", () =>
            {
                var source = new ListSource();
                var byTiles = World(source, 3, 1);
                var byRoom = World(source, 3, 1);
                // 방1 동쪽 테두리 칸(x 39)을 막되, 키운 모양(± 0.55)이 방1(X 10~20) 밖으로 나가지 않게
                var edgeBox = new NavBox(new Vector2(19.4f, 5f), new Vector2(0.05f, 1f), Vector2.right);
                source.Boxes.Add(rail);
                source.Boxes.Add(edgeBox);
                byTiles.RebuildTiles(rail.Bounds(0.5f));
                byTiles.RebuildTiles(edgeBox.Bounds(0.5f));
                byRoom.RebuildRoom(1);
                return SameWorld(byTiles, byRoom) && SameWorld(byRoom, World(source, 3, 1));
            });

            Case("OpenRoomCreatesGates", () =>
            {
                var world = World(new ListSource(), 2, 1, 1);
                if (world.Gates.GateCount(0) != 0) return false;
                world.SetRoomOpen(1, true);
                return world.Gates.GateCount(0) > 0
                       && world.Pathfinder.FindPath(new Vector3(2f, 0f, 5f), new Vector3(15f, 0f, 5f), Snap, points) == PathStatus.Ok;
            });
        }

        // ── Task 6: 이동 ──

        static Transform TempBody(Vector3 position)
        {
            var go = new GameObject("NavSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = position;
            return go.transform;
        }

        static void CrowdCases()
        {
            Case("CrowdArrives", () =>
            {
                var body = TempBody(Vector3.zero);
                try
                {
                    var crowd = new NavCrowd();
                    var agent = crowd.Add(body, 2f);
                    crowd.SetPath(agent, new List<Vector3> { Vector3.zero, new(4f, 0f, 0f), new(4f, 0f, 4f) });
                    for (var i = 0; i < 45; i++) crowd.Tick(0.1f);
                    return Vector3.Distance(crowd.Position(agent), new Vector3(4f, 0f, 4f)) < 0.01f && !crowd.IsMoving(agent)
                           && Vector3.Distance(body.position, new Vector3(4f, 0f, 4f)) < 0.01f;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(body.gameObject);
                }
            });

            Case("CrowdPauseRemainingRemove", () =>
            {
                var body = TempBody(Vector3.zero);
                try
                {
                    var crowd = new NavCrowd();
                    var agent = crowd.Add(body, 2f);
                    crowd.SetPath(agent, new List<Vector3> { Vector3.zero, new(4f, 0f, 0f), new(4f, 0f, 4f) });
                    var full = crowd.RemainingDistance(agent); // 8m
                    crowd.SetPaused(agent, true);
                    crowd.Tick(1f);
                    var paused = crowd.Position(agent) == Vector3.zero;
                    crowd.SetPaused(agent, false);
                    crowd.SetSpeed(agent, 4f);
                    crowd.Tick(0.5f); // 2m
                    var left = crowd.RemainingDistance(agent); // 6m
                    crowd.Remove(agent);
                    crowd.Tick(1f);
                    return Mathf.Abs(full - 8f) < 0.01f && paused && Mathf.Abs(left - 6f) < 0.01f && !crowd.IsMoving(agent) && crowd.RemainingDistance(agent) == 0f;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(body.gameObject);
                }
            });

            Case("RepathOnlyTouching", () =>
            {
                var g = Grid(3, 1);
                var a = TempBody(new Vector3(2f, 0f, 5f));
                var b = TempBody(new Vector3(1f, 0f, 1f));
                try
                {
                    var crowd = new NavCrowd();
                    var crossing = crowd.Add(a, 2f);
                    var staying = crowd.Add(b, 2f);
                    crowd.SetPath(crossing, new List<Vector3> { a.position, new(28f, 0f, 5f) }); // 방1(X 10~20)을 지남
                    crowd.SetPath(staying, new List<Vector3> { b.position, new(8f, 0f, 8f) });   // 방0 안
                    var rooms = new List<int> { 1 };
                    return crowd.PathTouchesRooms(crossing, g, rooms) && !crowd.PathTouchesRooms(staying, g, rooms);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(a.gameObject);
                    UnityEngine.Object.DestroyImmediate(b.gameObject);
                }
            });
        }
    }
}
