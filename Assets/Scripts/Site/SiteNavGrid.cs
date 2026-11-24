using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Navigation;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 게임 → 길찾기 코어 어댑터 (2026-11-24, 측정용 · 알파 후보). SiteGrid 열린 칸 = 방, Site 아래 충돌체 = 장애물.
    /// 방 (rx, rz) = (열 − 최소 열, 최대 행 − 행) — 행이 늘수록 월드 −Z라 뒤집는다. 격자 범위는 만들 때의 열린 칸 범위로 정한다.
    /// 장애물: 높이 띠(ObstacleMinY ~ MaxY)에 걸친, 트리거가 아닌, Site 아래 켜진 충돌체. 위가 수직인 BoxCollider는 회전 상자,
    /// 나머지는 경계 상자. 지금 NavMesh는 같은 충돌체를 굽기(가구) · carving(선반)으로 반영한다.
    /// </summary>
    public sealed class SiteNavGrid : INavObstacleSource
    {
        readonly Transform siteRoot;
        readonly NavGridConfig config;
        readonly Collider[] hits = new Collider[512];
        readonly int minColumn;
        readonly int maxRow;

        public NavWorld World { get; }
        /// <summary>만들 때 전체 굽기 시간 (ms).</summary>
        public double BuildMs { get; }

        public SiteNavGrid(SiteGrid site, Transform siteRoot, NavGridConfig config)
        {
            this.siteRoot = siteRoot;
            this.config = config;
            int maxColumn = int.MinValue, minRow = int.MaxValue;
            minColumn = int.MaxValue;
            maxRow = int.MinValue;
            foreach (var cell in site.OpenedCells)
            {
                minColumn = Mathf.Min(minColumn, cell.x);
                maxColumn = Mathf.Max(maxColumn, cell.x);
                minRow = Mathf.Min(minRow, cell.y);
                maxRow = Mathf.Max(maxRow, cell.y);
            }
            if (minColumn == int.MaxValue) throw new System.InvalidOperationException("[SiteNavGrid] 열린 칸이 없습니다");

            var roomSize = GameManager.Instance.Database.Site.CellSize;
            var grid = new NavGrid(new Vector2(minColumn * roomSize - roomSize * 0.5f, -(maxRow - 1) * roomSize - roomSize * 0.5f),
                maxColumn - minColumn + 1, maxRow - minRow + 1, config.CellSize,
                Mathf.RoundToInt(roomSize / config.CellSize), Mathf.RoundToInt(config.TileSize / config.CellSize));
            foreach (var cell in site.OpenedCells) grid.SetRoomOpen(RoomOf(cell, grid), true);

            World = new NavWorld(grid, this, config.AgentRadius);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            World.RebuildAll();
            BuildMs = watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>SiteGrid 칸 (열, 행) → 방 번호. 격자 밖이면 −1.</summary>
        public int RoomOf(Vector2Int siteCell) => RoomOf(siteCell, World.Grid);

        int RoomOf(Vector2Int siteCell, NavGrid grid)
        {
            int rx = siteCell.x - minColumn, rz = maxRow - siteCell.y;
            return rx < 0 || rz < 0 || rx >= grid.RoomsX || rz >= grid.RoomsZ ? -1 : grid.RoomIndex(rx, rz);
        }

        /// <summary>방 번호 → SiteGrid 칸 (열, 행).</summary>
        public Vector2Int SiteCellOf(int room) => new(room % World.Grid.RoomsX + minColumn, maxRow - room / World.Grid.RoomsX);

        /// <summary>월드 위치의 방. 격자 밖이면 −1.</summary>
        public int RoomAt(Vector3 world)
        {
            var grid = World.Grid;
            var cell = grid.WorldToCell(world);
            return grid.InBounds(cell.x, cell.y) ? grid.RoomOfCell(grid.Index(cell.x, cell.y)) : -1;
        }

        public void Collect(Rect worldXZ, List<NavBox> output)
        {
            Physics.SyncTransforms(); // 방금 놓은 레일 · 옮긴 모듈이 물리에 반영되도록
            float minY = config.ObstacleMinY, maxY = config.ObstacleMaxY;
            var center = new Vector3(worldXZ.center.x, (minY + maxY) * 0.5f, worldXZ.center.y);
            var half = new Vector3(worldXZ.width * 0.5f, (maxY - minY) * 0.5f, worldXZ.height * 0.5f);
            var count = Physics.OverlapBoxNonAlloc(center, half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) Debug.LogWarning("[SiteNavGrid] 영역의 충돌체가 버퍼(512)를 넘었습니다 — 일부 장애물이 빠질 수 있습니다");
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.enabled && hit.transform.IsChildOf(siteRoot)) output.Add(ToBox(hit));
            }
        }

        static NavBox ToBox(Collider collider)
        {
            var t = collider.transform;
            if (collider is BoxCollider box && Mathf.Abs(Vector3.Dot(t.up, Vector3.up)) > 0.999f)
            {
                var center = t.TransformPoint(box.center);
                var scale = t.lossyScale;
                var right = t.right;
                return new NavBox(new Vector2(center.x, center.z),
                    new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.z * scale.z)) * 0.5f, new Vector2(right.x, right.z));
            }
            var bounds = collider.bounds;
            return new NavBox(new Vector2(bounds.center.x, bounds.center.z), new Vector2(bounds.extents.x, bounds.extents.z), Vector2.right);
        }
    }
}
