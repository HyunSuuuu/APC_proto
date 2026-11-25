using System.Collections.Generic;
using APC.Core;
using APC.Navigation;
using APC.Site;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Staff
{
    /// <summary>
    /// 알바 길찾기 스위치 (2026-11-25): NavMesh(기본) ↔ 방 단위 A* (`Scripts/Navigation`). DEBUG 메뉴 Staff `Path A*`로 바꾼다 —
    /// 켜져 있는 동안 고용되는 알바도 A*. 처음 켤 때 부지 격자를 굽고(`SiteNavGrid`), 해금은 그 방만 · 재배치는 전체를
    /// 팝 연출이 끝난 뒤 다시 굽는다 (SiteNavMesh와 같은 지연). 다시 구운 방을 지나던 알바만 길을 다시 찾는다.
    /// 이동은 NavCrowd 하나가 모든 A* 알바를 한 Update에서 움직인다. NavMesh는 끄지 않는다 (대기 위치 고르기 · 생성 위치는 NavMesh로 붙임).
    /// </summary>
    public sealed class StaffNav : MonoBehaviour
    {
        public static bool UseAStar { get; private set; }
        /// <summary>만들어진 스위치 (꺼도 남는다 — 알바가 A*에서 빠질 때 쓴다).</summary>
        internal static StaffNav Shared { get; private set; }

        readonly NavCrowd crowd = new();
        readonly List<Vector3> waypoints = new();
        readonly List<string> unlockedIds = new();
        readonly List<int> changedRooms = new();
        SiteNavGrid site;
        Tween pending;
        bool rebuildAll;

        public SiteNavGrid Site => site;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            UseAStar = false;
            Shared = null;
        }

        public static void SetUseAStar(bool on)
        {
            if (on == UseAStar) return;
            if (on && Shared == null)
            {
                if (SiteGrid.Instance == null) return;
                Shared = new GameObject("StaffNav").AddComponent<StaffNav>();
            }
            UseAStar = on;
            Debug.Log($"[StaffNav] 알바 길찾기 = {(on ? "A*" : "NavMesh")}");
            foreach (var worker in StaffWorker.All) worker.ApplyNavMode();
        }

        void Awake()
        {
            site = Build();
            var grid = SiteGrid.Instance;
            grid.OnUnlocked.Subscribe(id => Schedule(id)).AddTo(this);
            grid.OnMoved.Subscribe(_ => Schedule(null)).AddTo(this);
        }

        static SiteNavGrid Build() => new(SiteGrid.Instance, SiteGrid.Instance.transform, GameManager.Instance.Database.NavGrid);

        void Update() => crowd.Tick(Time.deltaTime);

        internal int Register(Transform body, float speed) => crowd.Add(body, speed);
        internal void Release(int id) => crowd.Remove(id);
        internal void Stop(int id) => crowd.Stop(id);
        internal void Pause(int id, bool paused) => crowd.SetPaused(id, paused);
        internal float Remaining(int id) => crowd.RemainingDistance(id);

        /// <summary>지금 위치 → to 길을 찾아 걷게 한다. 못 찾으면 멈추고 false.</summary>
        internal bool SetPath(int id, Vector3 to, float speed)
        {
            crowd.SetSpeed(id, speed);
            var found = site.World.Pathfinder.FindPath(crowd.Position(id), to, GameManager.Instance.Database.NavGrid.SnapCells, waypoints) == PathStatus.Ok;
            if (found) crowd.SetPath(id, waypoints);
            else crowd.Stop(id);
            return found;
        }

        /// <summary>해금 id(빈 칸 열기는 빈 문자열) · 재배치(null)를 모아 팝 연출이 끝난 뒤 한 번에 다시 굽는다.</summary>
        void Schedule(string unlockedId)
        {
            if (unlockedId == null) rebuildAll = true;
            else unlockedIds.Add(unlockedId);
            pending?.Kill();
            pending = DOVirtual.DelayedCall(GameManager.Instance.Database.Logistics.FacilityPopDuration + 0.05f, Rebuild, false).SetLink(gameObject);
        }

        void Rebuild()
        {
            pending = null;
            var grid = SiteGrid.Instance;
            changedRooms.Clear();
            foreach (var cell in grid.OpenedCells)
            {
                if (site.RoomOf(cell) >= 0) continue;
                site = Build(); // 격자 범위 밖 칸이 열림 → 새로 굽는다 (방 수가 바뀜)
                Finish(true);
                return;
            }

            var world = site.World;
            if (rebuildAll)
            {
                world.RebuildAll();
                Finish(true);
                return;
            }
            foreach (var cell in grid.OpenedCells)
            {
                var room = site.RoomOf(cell);
                if (world.Grid.IsRoomOpen(room)) continue;
                world.SetRoomOpen(room, true);
                changedRooms.Add(room);
            }
            foreach (var id in unlockedIds)
            {
                var module = FindModule(grid, id);
                var room = module != null ? site.RoomAt(module.transform.position) : -1;
                if (room < 0 || changedRooms.Contains(room)) continue;
                world.RebuildRoom(room); // 열려 있던 칸에 모듈이 지어짐 (시작 순서) → 장애물 다시 칠하기
                changedRooms.Add(room);
            }
            Finish(false);
        }

        /// <summary>다시 구운 방을 지나던 A* 알바만 지금 목적지로 길을 다시 찾는다 (all = 전부).</summary>
        void Finish(bool all)
        {
            unlockedIds.Clear();
            rebuildAll = false;
            foreach (var worker in StaffWorker.All)
            {
                if (worker.CrowdId < 0 || !crowd.IsMoving(worker.CrowdId)) continue;
                if (all || crowd.PathTouchesRooms(worker.CrowdId, site.World.Grid, changedRooms)) worker.RefreshPath();
            }
        }

        static SiteModule FindModule(SiteGrid grid, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var entry in grid.Entries)
            {
                if (entry.id == id) return entry.instance;
            }
            return null;
        }
    }
}
