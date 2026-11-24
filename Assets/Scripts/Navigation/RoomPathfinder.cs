using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Navigation
{
    public enum PathStatus { Ok, NoStart, NoGoal, NoPath }

    /// <summary>
    /// 방 단위 2층 길찾기 (2026-11-24). 같은 방이면 방 안 A*, 아니면 문 그래프 A* + 문 지도를 따라 칸 길을 잇는다.
    /// 끝에 시야 검사로 꺾이는 점만 남겨 웨이포인트로 돌려준다. 비교 · 자기 검사용으로 맵 한 장 A*(<see cref="FindPathFlat"/>)도 있다.
    /// 버퍼는 재사용한다 — 호출마다 할당 없음(결과 리스트는 호출자가 준다). 스레드 하나에서만 쓴다.
    /// </summary>
    public sealed class RoomPathfinder
    {
        readonly NavGrid grid;
        readonly RoomGates gates;
        readonly GridAStar astar;
        readonly List<int> cells = new();
        readonly List<int> scratch = new();
        readonly List<int> chain = new();

        readonly int maxGates;
        readonly int startNode;
        readonly int goalNode;
        readonly int[] cost;
        readonly int[] parent;
        readonly int[] seen;
        readonly int[] closed;
        readonly IntMinHeap open = new();
        int generation;
        int startCell;
        int goalCell;

        /// <summary>마지막 길의 웨이포인트 길이 (m, XZ).</summary>
        public float LastLength { get; private set; }
        /// <summary>마지막 호출에서 펼친 노드 수 (칸 A* + 문 그래프).</summary>
        public int LastExpanded { get; private set; }

        public RoomPathfinder(NavGrid grid, RoomGates gates)
        {
            this.grid = grid;
            this.gates = gates;
            astar = new GridAStar(grid);
            maxGates = gates.MaxGatesPerRoom;
            var nodes = grid.RoomCount * maxGates + 2;
            startNode = nodes - 2;
            goalNode = nodes - 1;
            cost = new int[nodes];
            parent = new int[nodes];
            seen = new int[nodes];
            closed = new int[nodes];
        }

        /// <summary>방 단위 2층 길찾기. snapCells = 출발 · 도착이 막힌 칸일 때 붙일 반경(칸).</summary>
        public PathStatus FindPath(Vector3 from, Vector3 to, int snapCells, List<Vector3> waypoints)
        {
            using (NavMarkers.Path.Auto())
            {
                var status = Begin(from, to, snapCells, waypoints);
                if (status != PathStatus.Ok) return status;
                int startRoom = grid.RoomOfCell(startCell), goalRoom = grid.RoomOfCell(goalCell);
                var found = false;
                if (startRoom == goalRoom)
                {
                    found = astar.FindPath(grid.RoomRect(startRoom), startCell, goalCell, cells);
                    LastExpanded += astar.LastExpanded;
                }
                if (!found) found = SearchGates(startRoom, goalRoom);
                if (!found) return PathStatus.NoPath;
                Smooth(from, to, waypoints);
                return PathStatus.Ok;
            }
        }

        /// <summary>맵 전체 한 장 격자 A* (비교 · 자기 검사용).</summary>
        public PathStatus FindPathFlat(Vector3 from, Vector3 to, int snapCells, List<Vector3> waypoints)
        {
            using (NavMarkers.Path.Auto())
            {
                var status = Begin(from, to, snapCells, waypoints);
                if (status != PathStatus.Ok) return status;
                var found = astar.FindPath(new RectInt(0, 0, grid.Width, grid.Height), startCell, goalCell, cells);
                LastExpanded += astar.LastExpanded;
                if (!found) return PathStatus.NoPath;
                Smooth(from, to, waypoints);
                return PathStatus.Ok;
            }
        }

        PathStatus Begin(Vector3 from, Vector3 to, int snapCells, List<Vector3> waypoints)
        {
            waypoints.Clear();
            cells.Clear();
            LastLength = 0f;
            LastExpanded = 0;
            var a = grid.WorldToCell(from);
            if (!grid.TryNearestWalkable(a.x, a.y, snapCells, out startCell)) return PathStatus.NoStart;
            var b = grid.WorldToCell(to);
            if (!grid.TryNearestWalkable(b.x, b.y, snapCells, out goalCell)) return PathStatus.NoGoal;
            return PathStatus.Ok;
        }

        // ── 문 그래프 A* ──

        bool SearchGates(int startRoom, int goalRoom)
        {
            if (++generation == int.MaxValue)
            {
                Array.Clear(seen, 0, seen.Length);
                Array.Clear(closed, 0, closed.Length);
                generation = 1;
            }
            open.Clear();
            cost[startNode] = 0;
            seen[startNode] = generation;
            open.Push(Heuristic(startCell), startNode);

            while (open.Count > 0)
            {
                var node = open.Pop();
                if (closed[node] == generation) continue;
                closed[node] = generation;
                LastExpanded++;
                if (node == goalNode) return BuildCells(goalRoom);

                if (node == startNode)
                {
                    for (var i = 0; i < gates.GateCount(startRoom); i++)
                    {
                        var d = gates.Distance(startRoom, i, startCell);
                        if (d != ushort.MaxValue) Relax(startRoom * maxGates + i, d, node);
                    }
                    continue;
                }

                int room = node / maxGates, gate = node % maxGates;
                var here = cost[node];
                var cell = gates.GateCell(room, gate);
                var peer = gates.GatePeer(room, gate);
                if (peer >= 0) Relax(peer, here + GridAStar.Straight, node);
                for (var j = 0; j < gates.GateCount(room); j++)
                {
                    if (j == gate) continue;
                    var d = gates.Distance(room, j, cell);
                    if (d != ushort.MaxValue) Relax(room * maxGates + j, here + d, node);
                }
                if (room == goalRoom)
                {
                    var d = gates.Distance(room, gate, goalCell);
                    if (d != ushort.MaxValue) Relax(goalNode, here + d, node);
                }
            }
            return false;
        }

        void Relax(int node, int g, int from)
        {
            if (closed[node] == generation) return;
            if (seen[node] == generation && g >= cost[node]) return;
            seen[node] = generation;
            cost[node] = g;
            parent[node] = from;
            open.Push(g + Heuristic(NodeCell(node)), node);
        }

        int NodeCell(int node) => node == startNode ? startCell : node == goalNode ? goalCell : gates.GateCell(node / maxGates, node % maxGates);

        int Heuristic(int cell)
        {
            var width = grid.Width;
            return GridAStar.Heuristic(cell % width, cell / width, goalCell % width, goalCell / width);
        }

        /// <summary>노드 사슬(출발 → 문들 → 도착)을 칸 길로 바꾼다. 문 지도의 방향만 따라가므로 추가 탐색이 없다.</summary>
        bool BuildCells(int goalRoom)
        {
            chain.Clear();
            for (var n = goalNode; n != startNode; n = parent[n]) chain.Add(n);
            chain.Add(startNode);
            chain.Reverse();

            cells.Clear();
            cells.Add(startCell);
            var limit = grid.CellsPerRoom * grid.CellsPerRoom;
            for (var k = 1; k < chain.Count; k++)
            {
                int previous = chain[k - 1], node = chain[k];
                if (node == goalNode)
                {
                    // 마지막 문 → 도착: 도착에서 문까지 방향을 따라간 목록을 뒤집어 붙인다
                    int room = previous / maxGates, gate = previous % maxGates;
                    var gateCell = gates.GateCell(room, gate);
                    scratch.Clear();
                    for (var c = goalCell; c != gateCell; c = gates.StepTowardGate(room, gate, c))
                    {
                        if (c < 0 || scratch.Count > limit) return false;
                        scratch.Add(c);
                    }
                    for (var s = scratch.Count - 1; s >= 0; s--) cells.Add(scratch[s]);
                    continue;
                }

                int nodeRoom = node / maxGates, nodeGate = node % maxGates;
                var target = gates.GateCell(nodeRoom, nodeGate);
                var previousRoom = previous == startNode ? grid.RoomOfCell(startCell) : previous / maxGates;
                if (previousRoom != nodeRoom)
                {
                    cells.Add(target); // 변 건너기: 맞붙은 칸
                    continue;
                }
                var steps = 0;
                for (var c = cells[^1]; c != target;)
                {
                    c = gates.StepTowardGate(nodeRoom, nodeGate, c);
                    if (c < 0 || ++steps > limit) return false;
                    cells.Add(c);
                }
            }
            return true;
        }

        // ── 직선 펴기 ──

        /// <summary>
        /// 기준 칸에서 보이는 가장 먼 칸으로 건너뛴다 (끝 칸 먼저, 그다음 앞쪽 방 한 변 길이 × 2칸 안을 먼 쪽부터).
        /// 문(변 가운데)까지 갔다가 되돌아오는 꺾임을 없앤다. 첫 점 = from, 끝 점 = to(도착 칸이 to의 칸이면) 아니면 붙인 칸 중심.
        /// </summary>
        void Smooth(Vector3 from, Vector3 to, List<Vector3> waypoints)
        {
            waypoints.Add(from);
            var last = cells.Count - 1;
            var lookAhead = grid.CellsPerRoom * 2;
            var anchor = 0;
            while (anchor < last)
            {
                var next = anchor + 1;
                if (grid.LineOfSight(cells[anchor], cells[last])) next = last;
                else
                {
                    for (var j = Math.Min(last - 1, anchor + lookAhead); j > anchor + 1; j--)
                    {
                        if (!grid.LineOfSight(cells[anchor], cells[j])) continue;
                        next = j;
                        break;
                    }
                }
                if (next == last) break;
                anchor = next;
                var corner = grid.CellCenter(cells[anchor]);
                corner.y = from.y;
                waypoints.Add(corner);
            }
            var toCell = grid.WorldToCell(to);
            var end = grid.InBounds(toCell.x, toCell.y) && grid.Index(toCell.x, toCell.y) == goalCell ? to : grid.CellCenter(goalCell);
            if (end != to) end.y = from.y;
            waypoints.Add(end);

            var length = 0f;
            for (var i = 1; i < waypoints.Count; i++)
            {
                length += Vector2.Distance(new Vector2(waypoints[i - 1].x, waypoints[i - 1].z), new Vector2(waypoints[i].x, waypoints[i].z));
            }
            LastLength = length;
        }
    }
}
