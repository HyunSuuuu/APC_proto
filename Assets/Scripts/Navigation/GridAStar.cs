using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 사각형 범위 안 격자 A* (8방향, 곧은 10 · 대각선 14, 대각선은 옆 두 칸이 모두 걸을 수 있을 때만).
    /// 범위 = 방 하나(방 안 길) 또는 맵 전체(한 장 격자 길). 버퍼는 격자 크기로 한 번 만들고 세대 번호로 재사용한다 (호출마다 할당 없음).
    /// </summary>
    public sealed class GridAStar
    {
        internal const int Straight = 10;
        internal const int Diagonal = 14;
        internal static readonly int[] DirX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        internal static readonly int[] DirZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        readonly NavGrid grid;
        readonly int[] cost;
        readonly int[] parent;
        readonly int[] seen;   // 이번 세대에 cost가 정해진 칸
        readonly int[] closed; // 이번 세대에 닫힌 칸
        readonly IntMinHeap open = new();
        int generation;

        public int LastExpanded { get; private set; }

        public GridAStar(NavGrid grid)
        {
            this.grid = grid;
            var n = grid.Width * grid.Height;
            cost = new int[n];
            parent = new int[n];
            seen = new int[n];
            closed = new int[n];
        }

        /// <summary>start → goal 칸 목록(둘 다 포함)을 cells에 채운다. 둘 중 하나가 막혔거나 범위 밖이면 false.</summary>
        public bool FindPath(RectInt bounds, int start, int goal, List<int> cells)
        {
            cells.Clear();
            LastExpanded = 0;
            var width = grid.Width;
            if (!Inside(bounds, start, width) || !Inside(bounds, goal, width) || !grid.IsWalkable(start) || !grid.IsWalkable(goal)) return false;

            NextGeneration();
            int gx = goal % width, gz = goal / width;
            open.Clear();
            cost[start] = 0;
            seen[start] = generation;
            open.Push(Heuristic(start % width, start / width, gx, gz), start);

            while (open.Count > 0)
            {
                var current = open.Pop();
                if (closed[current] == generation) continue;
                closed[current] = generation;
                LastExpanded++;
                if (current == goal)
                {
                    for (var c = goal; c != start; c = parent[c]) cells.Add(c);
                    cells.Add(start);
                    cells.Reverse();
                    return true;
                }

                int cx = current % width, cz = current / width;
                for (var d = 0; d < 8; d++)
                {
                    int nx = cx + DirX[d], nz = cz + DirZ[d];
                    if (nx < bounds.xMin || nx >= bounds.xMax || nz < bounds.yMin || nz >= bounds.yMax) continue;
                    var next = nz * width + nx;
                    if (closed[next] == generation || !grid.IsWalkable(next)) continue;
                    if (d >= 4 && (!grid.IsWalkable(nx, cz) || !grid.IsWalkable(cx, nz))) continue; // 모서리 자르기 금지
                    var g = cost[current] + (d >= 4 ? Diagonal : Straight);
                    if (seen[next] == generation && g >= cost[next]) continue;
                    seen[next] = generation;
                    cost[next] = g;
                    parent[next] = current;
                    open.Push(g + Heuristic(nx, nz, gx, gz), next);
                }
            }
            return false;
        }

        /// <summary>옥타일 거리 (곧은 10 · 대각선 14).</summary>
        internal static int Heuristic(int x, int z, int gx, int gz)
        {
            int dx = Math.Abs(x - gx), dz = Math.Abs(z - gz);
            return Straight * Math.Max(dx, dz) + (Diagonal - Straight) * Math.Min(dx, dz);
        }

        static bool Inside(RectInt r, int index, int width)
        {
            int x = index % width, z = index / width;
            return x >= r.xMin && x < r.xMax && z >= r.yMin && z < r.yMax;
        }

        void NextGeneration()
        {
            if (++generation != int.MaxValue) return;
            Array.Clear(seen, 0, seen.Length);
            Array.Clear(closed, 0, closed.Length);
            generation = 1;
        }
    }
}
