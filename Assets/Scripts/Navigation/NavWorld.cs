using System.Collections.Generic;
using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 길찾기 세계 = 격자 + 문 + 길찾기 + 다시 굽기 범위 (2026-11-24). 장애물은 INavObstacleSource에서 그때그때 받는다.
    /// 전체: 모든 방 칠하기 → 모든 문 · 문 지도. 방별: 그 방 칠하기 → 그 방(+ 공유 변 문이 바뀐 이웃) 문 · 문 지도.
    /// 타일별: 영역이 걸친 2m 타일만 칠하기 → 칸이 바뀐 방만 (테두리가 바뀌었으면 방별과 같게, 아니면 그 방 문 지도만).
    /// 방별은 그 방 칸만 칠한다 — 키운 장애물이 이웃 방 칸에 걸치면 이웃 방도 따로 다시 굽어야 한다.
    /// </summary>
    public sealed class NavWorld
    {
        readonly INavObstacleSource source;
        readonly float inflate;
        readonly List<NavBox> boxes = new();
        readonly List<int> changedRooms = new();

        public NavGrid Grid { get; }
        public RoomGates Gates { get; }
        public RoomPathfinder Pathfinder { get; }
        /// <summary>마지막 Rebuild 호출에서 칸이 바뀐 방.</summary>
        public IReadOnlyList<int> ChangedRooms => changedRooms;
        /// <summary>마지막 Rebuild 호출에서 만든 문 지도 수.</summary>
        public int LastMapsBuilt { get; private set; }

        public NavWorld(NavGrid grid, INavObstacleSource source, float inflate)
        {
            Grid = grid;
            this.source = source;
            this.inflate = inflate;
            Gates = new RoomGates(grid);
            Pathfinder = new RoomPathfinder(grid, Gates);
        }

        public void RebuildAll()
        {
            using (NavMarkers.Bake.Auto())
            {
                changedRooms.Clear();
                for (var room = 0; room < Grid.RoomCount; room++)
                {
                    if (Paint(Grid.RoomRect(room), out _) > 0) changedRooms.Add(room);
                }
                Gates.BuildAll();
                LastMapsBuilt = Gates.LastMapsBuilt;
            }
        }

        public void RebuildRoom(int room)
        {
            using (NavMarkers.Bake.Auto())
            {
                changedRooms.Clear();
                if (Paint(Grid.RoomRect(room), out _) > 0) changedRooms.Add(room);
                Gates.RebuildRoom(room);
                LastMapsBuilt = Gates.LastMapsBuilt;
            }
        }

        public void RebuildTiles(Rect worldXZ)
        {
            using (NavMarkers.Bake.Auto())
            {
                changedRooms.Clear();
                LastMapsBuilt = 0;
                var tiles = Grid.TilesRect(worldXZ);
                if (tiles.width <= 0 || tiles.height <= 0) return;
                var cpr = Grid.CellsPerRoom;
                for (var rz = tiles.yMin / cpr; rz <= (tiles.yMax - 1) / cpr; rz++)
                for (var rx = tiles.xMin / cpr; rx <= (tiles.xMax - 1) / cpr; rx++)
                {
                    var room = Grid.RoomIndex(rx, rz);
                    var roomRect = Grid.RoomRect(room);
                    var x0 = Mathf.Max(tiles.xMin, roomRect.xMin);
                    var z0 = Mathf.Max(tiles.yMin, roomRect.yMin);
                    var part = new RectInt(x0, z0, Mathf.Min(tiles.xMax, roomRect.xMax) - x0, Mathf.Min(tiles.yMax, roomRect.yMax) - z0);
                    if (Paint(part, out var edgeChanged) == 0) continue;
                    changedRooms.Add(room);
                    if (edgeChanged) Gates.RebuildRoom(room);
                    else Gates.RebuildMaps(room);
                    LastMapsBuilt += Gates.LastMapsBuilt;
                }
            }
        }

        /// <summary>방 열림 표시를 바꾸고 그 방을 다시 굽는다 (해금 = 열기).</summary>
        public void SetRoomOpen(int room, bool open)
        {
            Grid.SetRoomOpen(room, open);
            RebuildRoom(room);
        }

        /// <summary>cells 범위를 칠한다. 장애물은 키우기만큼 넓힌 영역에서 받는다 (영역 밖 장애물도 키우면 안쪽 칸을 덮을 수 있음).</summary>
        int Paint(RectInt cells, out bool edgeChanged)
        {
            var cs = Grid.CellSize;
            var area = new Rect(Grid.Origin.x + cells.xMin * cs - inflate, Grid.Origin.y + cells.yMin * cs - inflate,
                cells.width * cs + inflate * 2f, cells.height * cs + inflate * 2f);
            boxes.Clear();
            source.Collect(area, boxes);
            return Grid.Rasterize(cells, boxes, inflate, out edgeChanged);
        }
    }
}
