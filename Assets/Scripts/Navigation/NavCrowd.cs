using System.Collections.Generic;
using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 알바 이동 (멍청해도 됨, 2026-11-24). 모든 알바를 한 번의 Tick에서 움직인다 — 알바마다 Update를 두지 않는다 (CPU 목적).
    /// 웨이포인트를 일정 속도로 따라가고 가는 방향으로만 돌린다. 서로 피하지 않고 가속도 없다 (지금 NavMesh 알바도 회피 끔).
    /// </summary>
    public sealed class NavCrowd
    {
        sealed class Agent
        {
            public Transform Body;
            public float Speed;
            public bool Paused;
            public Vector3 Position;
            public int Next;
            public readonly List<Vector3> Path = new();
        }

        readonly List<Agent> agents = new();

        public int Count => agents.Count;

        public int Add(Transform body, float speed)
        {
            agents.Add(new Agent { Body = body, Speed = speed, Position = body.position });
            return agents.Count - 1;
        }

        /// <summary>웨이포인트를 복사한다. [0]은 출발점(지금 위치)이라 [1]부터 간다.</summary>
        public void SetPath(int agent, List<Vector3> waypoints)
        {
            var a = agents[agent];
            a.Path.Clear();
            a.Path.AddRange(waypoints);
            a.Next = Mathf.Min(1, a.Path.Count);
        }

        /// <summary>순간 이동 — 길을 지우고 그 자리에 둔다 (벨트 설치에 밀려남 등).</summary>
        public void Warp(int agent, Vector3 position)
        {
            var a = agents[agent];
            a.Path.Clear();
            a.Next = 0;
            a.Position = position;
            if (a.Body != null) a.Body.position = position;
        }

        public void Stop(int agent)
        {
            var a = agents[agent];
            a.Path.Clear();
            a.Next = 0;
        }

        /// <summary>빼기 — 길을 지우고 몸을 놓는다. 번호는 다시 쓰지 않는다 (shortcut: 고용 · 해고마다 칸이 하나씩 남음, 수천 명이면 빈 칸 재사용).</summary>
        public void Remove(int agent)
        {
            Stop(agent);
            agents[agent].Body = null;
        }

        /// <summary>멈춤 (길은 그대로, 풀면 이어서 감).</summary>
        public void SetPaused(int agent, bool paused) => agents[agent].Paused = paused;

        public void SetSpeed(int agent, float speed) => agents[agent].Speed = speed;

        /// <summary>남은 길이 (m, 지금 위치 → 남은 웨이포인트). 서 있으면 0.</summary>
        public float RemainingDistance(int agent)
        {
            var a = agents[agent];
            var total = 0f;
            var from = a.Position;
            for (var i = a.Next; i < a.Path.Count; i++)
            {
                total += Vector3.Distance(from, a.Path[i]);
                from = a.Path[i];
            }
            return total;
        }

        public bool IsMoving(int agent) => agents[agent].Next < agents[agent].Path.Count;

        public Vector3 Position(int agent) => agents[agent].Position;

        /// <summary>지금 위치나 남은 길(2m 간격으로 찍은 점)이 rooms 중 한 방에 드는가 — 다시 구운 방을 지나는 알바만 다시 길을 찾게.</summary>
        public bool PathTouchesRooms(int agent, NavGrid grid, IReadOnlyList<int> rooms)
        {
            var a = agents[agent];
            if (a.Next >= a.Path.Count) return false;
            var from = a.Position;
            if (InRooms(grid, from, rooms)) return true;
            var spacing = grid.CellSize * grid.CellsPerTile;
            for (var i = a.Next; i < a.Path.Count; i++)
            {
                var to = a.Path[i];
                var samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / spacing));
                for (var k = 1; k <= samples; k++)
                {
                    if (InRooms(grid, Vector3.Lerp(from, to, (float)k / samples), rooms)) return true;
                }
                from = to;
            }
            return false;
        }

        public void Tick(float deltaTime)
        {
            using (NavMarkers.Move.Auto())
            {
                for (var i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    if (a.Paused || a.Next >= a.Path.Count) continue;
                    var budget = a.Speed * deltaTime;
                    var position = a.Position;
                    var heading = Vector3.zero;
                    while (budget > 0f && a.Next < a.Path.Count)
                    {
                        var offset = a.Path[a.Next] - position;
                        var distance = offset.magnitude;
                        if (distance > 1e-4f) heading = offset;
                        if (distance <= budget)
                        {
                            position = a.Path[a.Next];
                            budget -= distance;
                            a.Next++;
                        }
                        else
                        {
                            position += offset * (budget / distance);
                            budget = 0f;
                        }
                    }
                    a.Position = position;
                    if (a.Body == null) continue;
                    heading.y = 0f;
                    if (heading.sqrMagnitude > 1e-6f) a.Body.SetPositionAndRotation(position, Quaternion.LookRotation(heading));
                    else a.Body.position = position;
                }
            }
        }

        static bool InRooms(NavGrid grid, Vector3 world, IReadOnlyList<int> rooms)
        {
            var cell = grid.WorldToCell(world);
            if (!grid.InBounds(cell.x, cell.y)) return false;
            var room = grid.RoomOfCell(grid.Index(cell.x, cell.y));
            for (var i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] == room) return true;
            }
            return false;
        }
    }
}
