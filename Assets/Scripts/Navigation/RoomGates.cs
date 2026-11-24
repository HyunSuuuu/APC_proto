using System;
using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 방 문 · 문 지도 (방 단위 2층 길찾기의 윗층, 2026-11-24).
    /// 문: 방의 서 · 동 · 남 · 북 변마다, 두 방이 모두 열려 있고 마주 보는 두 칸이 모두 걸을 수 있는 칸 쌍의 이어진 구간마다 가운데 한 쌍.
    /// 문 지도: 문마다 방 안 다익스트라 — 방 안 모든 칸에서 그 문까지 비용(곧은 10 · 대각선 14)과 문 쪽 다음 방향.
    /// 방 안 문↔문 비용은 따로 저장하지 않고 문 지도에서 읽는다. 노드 번호 = 방 × MaxGatesPerRoom + 문 번호.
    /// </summary>
    public sealed class RoomGates
    {
        const byte NoStep = 255;
        static readonly int[] Opposite = { 1, 0, 3, 2, 7, 6, 5, 4 };

        readonly NavGrid grid;
        readonly int cellsPerRoom;
        readonly int roomCells;
        readonly int[] gateCount;
        readonly int[][] gateCell;
        readonly int[][] gateSide;
        readonly int[][] gatePeer;
        readonly ushort[][][] dist;
        readonly byte[][][] step;
        readonly int[] scratchCell;
        readonly int[] scratchSide;
        readonly bool[] settled;
        readonly IntMinHeap heap = new();

        /// <summary>방 하나가 가질 수 있는 문 최대 수 (변 4개 × 구간 최대 수).</summary>
        public int MaxGatesPerRoom { get; }
        public int TotalGates { get; private set; }
        /// <summary>마지막 공개 호출에서 만든 문 지도 수 (다익스트라 횟수).</summary>
        public int LastMapsBuilt { get; private set; }

        public int MemoryBytes
        {
            get
            {
                var bytes = 0;
                foreach (var maps in dist)
                {
                    foreach (var map in maps)
                    {
                        if (map != null) bytes += map.Length * 3; // ushort 거리 + byte 방향
                    }
                }
                return bytes + gateCount.Length * MaxGatesPerRoom * 12;
            }
        }

        public RoomGates(NavGrid grid)
        {
            this.grid = grid;
            cellsPerRoom = grid.CellsPerRoom;
            roomCells = cellsPerRoom * cellsPerRoom;
            MaxGatesPerRoom = 4 * ((cellsPerRoom + 1) / 2);
            var rooms = grid.RoomCount;
            gateCount = new int[rooms];
            gateCell = new int[rooms][];
            gateSide = new int[rooms][];
            gatePeer = new int[rooms][];
            dist = new ushort[rooms][][];
            step = new byte[rooms][][];
            for (var r = 0; r < rooms; r++)
            {
                gateCell[r] = new int[MaxGatesPerRoom];
                gateSide[r] = new int[MaxGatesPerRoom];
                gatePeer[r] = new int[MaxGatesPerRoom];
                dist[r] = new ushort[MaxGatesPerRoom][];
                step[r] = new byte[MaxGatesPerRoom][];
            }
            scratchCell = new int[MaxGatesPerRoom];
            scratchSide = new int[MaxGatesPerRoom];
            settled = new bool[roomCells];
        }

        public int GateCount(int room) => gateCount[room];
        public int GateCell(int room, int gate) => gateCell[room][gate];
        public int GatePeer(int room, int gate) => gatePeer[room][gate];

        /// <summary>방 안 cell → 문 비용. 다른 방 칸이거나 못 가면 ushort.MaxValue.</summary>
        public ushort Distance(int room, int gate, int cell)
        {
            var local = Local(room, cell);
            return local < 0 ? ushort.MaxValue : dist[room][gate][local];
        }

        /// <summary>cell에서 문 쪽 다음 칸. 문 칸이거나 못 가면 −1.</summary>
        public int StepTowardGate(int room, int gate, int cell)
        {
            var local = Local(room, cell);
            if (local < 0) return -1;
            var d = step[room][gate][local];
            if (d == NoStep) return -1;
            return grid.Index(cell % grid.Width + GridAStar.DirX[d], cell / grid.Width + GridAStar.DirZ[d]);
        }

        public void BuildAll()
        {
            LastMapsBuilt = 0;
            for (var r = 0; r < grid.RoomCount; r++)
            {
                gateCount[r] = FindGates(r, gateCell[r], gateSide[r]);
            }
            for (var r = 0; r < grid.RoomCount; r++) BuildMaps(r);
            for (var r = 0; r < grid.RoomCount; r++) LinkPeers(r);
            CountGates();
        }

        /// <summary>그 방의 문 · 문 지도를 다시 만들고, 공유 변의 문이 바뀐 이웃 방도 문 · 문 지도를 다시 만든다.</summary>
        public void RebuildRoom(int room)
        {
            LastMapsBuilt = 0;
            gateCount[room] = FindGates(room, gateCell[room], gateSide[room]);
            BuildMaps(room);
            for (var side = 0; side < 4; side++)
            {
                var n = Neighbor(room, side);
                if (n < 0) continue;
                var count = FindGates(n, scratchCell, scratchSide);
                if (!SameGates(n, count))
                {
                    gateCount[n] = count;
                    Array.Copy(scratchCell, gateCell[n], count);
                    Array.Copy(scratchSide, gateSide[n], count);
                    BuildMaps(n);
                }
            }
            // 이웃 문 번호가 바뀌면 그 이웃의 이웃이 가리키는 번호도 틀어지므로 전부 다시 잇는다 (문 수 × 이웃 문 수, 아주 쌈)
            for (var r = 0; r < grid.RoomCount; r++) LinkPeers(r);
            CountGates();
        }

        /// <summary>문은 그대로 두고 그 방 문 지도만 다시 (방 테두리가 안 바뀐 안쪽 변경).</summary>
        public void RebuildMaps(int room)
        {
            LastMapsBuilt = 0;
            BuildMaps(room);
        }

        // ── 문 찾기 ──

        /// <summary>방 room의 문을 cells · sides에 채우고 수를 돌려준다. 순서: 서 · 동 · 남 · 북, 변 안에서는 좌표 오름차순.</summary>
        int FindGates(int room, int[] cells, int[] sides)
        {
            if (!grid.IsRoomOpen(room)) return 0;
            var count = 0;
            var rect = grid.RoomRect(room);
            for (var side = 0; side < 4; side++)
            {
                var n = Neighbor(room, side);
                if (n < 0 || !grid.IsRoomOpen(n)) continue;
                var runStart = -1;
                for (var t = 0; t <= cellsPerRoom; t++)
                {
                    var open = t < cellsPerRoom && EdgePairOpen(rect, side, t);
                    if (open && runStart < 0) runStart = t;
                    if (open || runStart < 0) continue;
                    var mid = runStart + (t - runStart) / 2;
                    cells[count] = EdgeCell(rect, side, mid);
                    sides[count] = side;
                    count++;
                    runStart = -1;
                }
            }
            return count;
        }

        bool EdgePairOpen(RectInt rect, int side, int t)
        {
            var a = EdgeCell(rect, side, t);
            int x = a % grid.Width + SideX(side), z = a / grid.Width + SideZ(side);
            return grid.IsWalkable(a) && grid.IsWalkable(x, z);
        }

        /// <summary>변 side의 t번째 칸 (방 안쪽 칸). 0 = 서(−x) · 1 = 동(+x) · 2 = 남(−z) · 3 = 북(+z).</summary>
        int EdgeCell(RectInt rect, int side, int t) => side switch
        {
            0 => grid.Index(rect.xMin, rect.yMin + t),
            1 => grid.Index(rect.xMax - 1, rect.yMin + t),
            2 => grid.Index(rect.xMin + t, rect.yMin),
            _ => grid.Index(rect.xMin + t, rect.yMax - 1),
        };

        static int SideX(int side) => side == 0 ? -1 : side == 1 ? 1 : 0;
        static int SideZ(int side) => side == 2 ? -1 : side == 3 ? 1 : 0;

        int Neighbor(int room, int side)
        {
            int rx = room % grid.RoomsX + SideX(side), rz = room / grid.RoomsX + SideZ(side);
            return rx < 0 || rz < 0 || rx >= grid.RoomsX || rz >= grid.RoomsZ ? -1 : grid.RoomIndex(rx, rz);
        }

        bool SameGates(int room, int count)
        {
            if (count != gateCount[room]) return false;
            for (var i = 0; i < count; i++)
            {
                if (scratchCell[i] != gateCell[room][i]) return false;
            }
            return true;
        }

        void LinkPeers(int room)
        {
            for (var i = 0; i < gateCount[room]; i++)
            {
                var side = gateSide[room][i];
                var n = Neighbor(room, side);
                var cell = gateCell[room][i];
                var across = grid.Index(cell % grid.Width + SideX(side), cell / grid.Width + SideZ(side));
                gatePeer[room][i] = -1;
                for (var j = 0; j < gateCount[n]; j++)
                {
                    if (gateCell[n][j] != across) continue;
                    gatePeer[room][i] = n * MaxGatesPerRoom + j;
                    break;
                }
            }
        }

        void CountGates()
        {
            TotalGates = 0;
            foreach (var c in gateCount) TotalGates += c;
        }

        // ── 문 지도 ──

        void BuildMaps(int room)
        {
            for (var i = 0; i < gateCount[room]; i++)
            {
                dist[room][i] ??= new ushort[roomCells];
                step[room][i] ??= new byte[roomCells];
                Dijkstra(room, gateCell[room][i], dist[room][i], step[room][i]);
                LastMapsBuilt++;
            }
        }

        void Dijkstra(int room, int source, ushort[] distance, byte[] direction)
        {
            Array.Fill(distance, ushort.MaxValue);
            Array.Fill(direction, NoStep);
            Array.Clear(settled, 0, settled.Length);
            var rect = grid.RoomRect(room);
            var width = grid.Width;
            heap.Clear();
            distance[Local(room, source)] = 0;
            heap.Push(0, source);
            while (heap.Count > 0)
            {
                var cell = heap.Pop();
                var cellLocal = Local(room, cell);
                if (settled[cellLocal]) continue; // 더 싼 값으로 이미 꺼낸 칸 (힙에 남은 옛 값)
                settled[cellLocal] = true;
                var here = distance[cellLocal];
                int cx = cell % width, cz = cell / width;
                for (var d = 0; d < 8; d++)
                {
                    int nx = cx + GridAStar.DirX[d], nz = cz + GridAStar.DirZ[d];
                    if (nx < rect.xMin || nx >= rect.xMax || nz < rect.yMin || nz >= rect.yMax) continue;
                    var next = nz * width + nx;
                    if (!grid.IsWalkable(next)) continue;
                    if (d >= 4 && (!grid.IsWalkable(nx, cz) || !grid.IsWalkable(cx, nz))) continue;
                    var cost = here + (d >= 4 ? GridAStar.Diagonal : GridAStar.Straight);
                    var local = (nz - rect.yMin) * cellsPerRoom + (nx - rect.xMin);
                    if (cost >= distance[local]) continue;
                    distance[local] = (ushort)cost;
                    direction[local] = (byte)Opposite[d];
                    heap.Push(cost, next);
                }
            }
        }

        /// <summary>격자 칸 → 방 안 번호. 그 방 칸이 아니면 −1.</summary>
        int Local(int room, int cell)
        {
            var rect = grid.RoomRect(room);
            int x = cell % grid.Width - rect.xMin, z = cell / grid.Width - rect.yMin;
            return x < 0 || z < 0 || x >= cellsPerRoom || z >= cellsPerRoom ? -1 : z * cellsPerRoom + x;
        }
    }
}
