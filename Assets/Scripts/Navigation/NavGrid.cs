using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 길찾기 격자 (2026-11-24). 맵 = 방(RoomsX × RoomsZ) × 방마다 CellsPerRoom² 칸. 칸 (x, z)은 월드 X · Z 방향,
    /// 원점(Origin)은 격자 왼쪽 아래 모서리의 월드 XZ. 걸을 수 있음 = 방이 열림 && 키운 장애물이 칸 중심을 덮지 않음.
    /// 방 열림 표시(SetRoomOpen)와 칠하기(Rasterize)는 따로다 — 표시만 바꾸면 칸은 그대로.
    /// </summary>
    public sealed class NavGrid
    {
        readonly bool[] walkable;
        readonly bool[] roomOpen;
        readonly List<NavBox> nearBoxes = new();

        public Vector2 Origin { get; }
        public float CellSize { get; }
        public int RoomsX { get; }
        public int RoomsZ { get; }
        public int CellsPerRoom { get; }
        public int CellsPerTile { get; }
        public int Width { get; }
        public int Height { get; }
        public int RoomCount => RoomsX * RoomsZ;
        public int MemoryBytes => walkable.Length + roomOpen.Length;

        public NavGrid(Vector2 origin, int roomsX, int roomsZ, float cellSize, int cellsPerRoom, int cellsPerTile)
        {
            if (roomsX <= 0 || roomsZ <= 0 || cellSize <= 0f || cellsPerRoom <= 0 || cellsPerTile <= 0)
                throw new ArgumentException("[NavGrid] 크기는 0보다 커야 합니다");
            Origin = origin;
            RoomsX = roomsX;
            RoomsZ = roomsZ;
            CellSize = cellSize;
            CellsPerRoom = cellsPerRoom;
            CellsPerTile = cellsPerTile;
            Width = roomsX * cellsPerRoom;
            Height = roomsZ * cellsPerRoom;
            walkable = new bool[Width * Height];
            roomOpen = new bool[roomsX * roomsZ];
        }

        public int Index(int x, int z) => z * Width + x;
        public bool InBounds(int x, int z) => (uint)x < (uint)Width && (uint)z < (uint)Height;
        public bool IsWalkable(int index) => walkable[index];
        public bool IsWalkable(int x, int z) => InBounds(x, z) && walkable[z * Width + x];

        public int RoomIndex(int rx, int rz) => rz * RoomsX + rx;
        public int RoomOfCell(int index) => RoomIndex(index % Width / CellsPerRoom, index / Width / CellsPerRoom);
        public RectInt RoomRect(int room) => new(room % RoomsX * CellsPerRoom, room / RoomsX * CellsPerRoom, CellsPerRoom, CellsPerRoom);
        public bool IsRoomOpen(int room) => roomOpen[room];
        public void SetRoomOpen(int room, bool open) => roomOpen[room] = open;

        public Vector2Int WorldToCell(Vector3 world)
            => new(Mathf.FloorToInt((world.x - Origin.x) / CellSize), Mathf.FloorToInt((world.z - Origin.y) / CellSize));

        public Vector3 CellCenter(int index) => new(Origin.x + (index % Width + 0.5f) * CellSize, 0f, Origin.y + (index / Width + 0.5f) * CellSize);

        Vector2 CellCenter2(int x, int z) => new(Origin.x + (x + 0.5f) * CellSize, Origin.y + (z + 0.5f) * CellSize);

        /// <summary>월드 XZ 영역이 걸친 타일들을 통째로 덮는 칸 범위 (격자 안으로 자름, 걸친 게 없으면 크기 0).</summary>
        public RectInt TilesRect(Rect worldXZ)
        {
            var t = CellsPerTile;
            var x0 = FloorDiv(Mathf.FloorToInt((worldXZ.xMin - Origin.x) / CellSize), t) * t;
            var z0 = FloorDiv(Mathf.FloorToInt((worldXZ.yMin - Origin.y) / CellSize), t) * t;
            var x1 = (FloorDiv(Mathf.CeilToInt((worldXZ.xMax - Origin.x) / CellSize) - 1, t) + 1) * t;
            var z1 = (FloorDiv(Mathf.CeilToInt((worldXZ.yMax - Origin.y) / CellSize) - 1, t) + 1) * t;
            return Clamp(new RectInt(x0, z0, x1 - x0, z1 - z0));
        }

        /// <summary>
        /// cells 범위를 다시 칠한다. 반환 = 바뀐 칸 수, edgeChanged = 바뀐 칸 중 방 테두리 칸이 있음 (문이 바뀌었을 수 있음).
        /// </summary>
        public int Rasterize(RectInt cells, IReadOnlyList<NavBox> boxes, float inflate, out bool edgeChanged)
        {
            edgeChanged = false;
            cells = Clamp(cells);
            if (cells.width <= 0 || cells.height <= 0) return 0;

            // 이 범위에 닿는 상자만 추린다
            var area = new Rect(Origin.x + cells.xMin * CellSize, Origin.y + cells.yMin * CellSize, cells.width * CellSize, cells.height * CellSize);
            nearBoxes.Clear();
            for (var i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Bounds(inflate).Overlaps(area)) nearBoxes.Add(boxes[i]);
            }

            var changed = 0;
            var last = CellsPerRoom - 1;
            for (var z = cells.yMin; z < cells.yMax; z++)
            for (var x = cells.xMin; x < cells.xMax; x++)
            {
                var free = roomOpen[RoomIndex(x / CellsPerRoom, z / CellsPerRoom)];
                if (free)
                {
                    var center = CellCenter2(x, z);
                    for (var b = 0; b < nearBoxes.Count; b++)
                    {
                        if (!nearBoxes[b].Contains(center, inflate)) continue;
                        free = false;
                        break;
                    }
                }
                var index = z * Width + x;
                if (walkable[index] == free) continue;
                walkable[index] = free;
                changed++;
                int lx = x % CellsPerRoom, lz = z % CellsPerRoom;
                if (lx == 0 || lz == 0 || lx == last || lz == last) edgeChanged = true;
            }
            return changed;
        }

        /// <summary>(x, z)에서 maxRadius칸 안 가장 가까운 걸을 수 있는 칸 (가까운 고리부터, 고리 안에선 직선 거리).</summary>
        public bool TryNearestWalkable(int x, int z, int maxRadius, out int index)
        {
            index = -1;
            if (IsWalkable(x, z))
            {
                index = Index(x, z);
                return true;
            }
            for (var r = 1; r <= maxRadius; r++)
            {
                var best = int.MaxValue;
                for (var dz = -r; dz <= r; dz++)
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r || !IsWalkable(x + dx, z + dz)) continue;
                    var d = dx * dx + dz * dz;
                    if (d >= best) continue;
                    best = d;
                    index = Index(x + dx, z + dz);
                }
                if (index >= 0) return true;
            }
            return false;
        }

        /// <summary>두 칸 중심을 잇는 선분이 지나는 칸이 모두 걸을 수 있는가. 꼭짓점을 정확히 지나면 양옆 칸도 걸을 수 있어야 한다.</summary>
        public bool LineOfSight(int from, int to)
        {
            int x = from % Width, z = from / Width;
            int x1 = to % Width, z1 = to / Width;
            int dx = Math.Abs(x1 - x), dz = Math.Abs(z1 - z);
            int sx = x1 > x ? 1 : -1, sz = z1 > z ? 1 : -1;
            var n = 1 + dx + dz;
            var error = dx - dz;
            dx *= 2;
            dz *= 2;
            for (; n > 0; n--)
            {
                if (!walkable[z * Width + x]) return false;
                if (error > 0)
                {
                    x += sx;
                    error -= dz;
                }
                else if (error < 0)
                {
                    z += sz;
                    error += dx;
                }
                else
                {
                    if (n == 1) break; // 도착 칸
                    if (!IsWalkable(x + sx, z) || !IsWalkable(x, z + sz)) return false;
                    x += sx;
                    z += sz;
                    error += dx - dz;
                    n--;
                }
            }
            return true;
        }

        RectInt Clamp(RectInt r)
        {
            var x0 = Mathf.Max(0, r.xMin);
            var z0 = Mathf.Max(0, r.yMin);
            var x1 = Mathf.Min(Width, r.xMax);
            var z1 = Mathf.Min(Height, r.yMax);
            return new RectInt(x0, z0, Mathf.Max(0, x1 - x0), Mathf.Max(0, z1 - z0));
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
