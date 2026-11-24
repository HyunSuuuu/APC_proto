using System;
using System.Collections.Generic;
using APC.Data;
using APC.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace APC.Debugging
{
    /// <summary>
    /// 길찾기 비교용 측정 알바 (2026-11-24). 두 방식이 같은 일을 한다: 같은 캡슐 모양 · 같은 속도 · 같은 시드의 시작 자리와 목적지 순서,
    /// 알바마다 repathInterval초마다(시작 시점은 흩음) 다음 목적지. NavMesh = NavMeshAgent(알바 프리팹 설정 복사) + SetDestination,
    /// A* = NavCrowd + 프레임당 MaxPathsPerFrame개까지 줄 세운 FindPath. 충돌체 없음 (실제 알바와 같음).
    /// </summary>
    public sealed class NavBenchAgents : IDisposable
    {
        public enum Mode { NavMesh, AStar }

        readonly Mode mode;
        readonly IReadOnlyList<Vector3> targets;
        readonly float interval;
        readonly NavWorld world;
        readonly int snapCells;
        readonly int maxPathsPerFrame;
        readonly System.Random random;
        readonly List<GameObject> bodies = new();
        readonly List<NavMeshAgent> navAgents = new();
        readonly float[] timer;
        readonly int[] target;
        readonly bool[] queued;
        readonly Queue<int> pending = new();
        readonly List<Vector3> waypoints = new();

        public int PathRequests { get; private set; }
        public int PathFailures { get; private set; }
        public int Repaths { get; private set; }
        public NavCrowd Crowd { get; }
        public int Count => bodies.Count;

        public NavBenchAgents(Mode mode, NavBenchmarkConfig config, IReadOnlyList<Vector3> targets, float speed, float repathInterval, NavWorld world)
        {
            this.mode = mode;
            this.targets = targets;
            this.world = world;
            interval = repathInterval;
            snapCells = config.Grid.SnapCells;
            maxPathsPerFrame = config.Grid.MaxPathsPerFrame;
            random = new System.Random(config.Seed);
            var count = config.AgentCount;
            timer = new float[count];
            target = new int[count];
            queued = new bool[count];
            if (mode == Mode.AStar) Crowd = new NavCrowd();
            var template = config.StaffWorkerPrefab != null ? config.StaffWorkerPrefab.GetComponent<NavMeshAgent>() : null;

            for (var i = 0; i < count; i++)
            {
                var start = targets[random.Next(targets.Count)];
                timer[i] = (float)random.NextDouble() * interval;
                target[i] = random.Next(targets.Count);
                var body = CreateBody(i, start);
                bodies.Add(body);
                if (mode == Mode.NavMesh)
                {
                    if (NavMesh.SamplePosition(start, out var hit, 2f, NavMesh.AllAreas)) body.transform.position = hit.position;
                    var agent = body.AddComponent<NavMeshAgent>();
                    CopySettings(template, agent);
                    agent.speed = speed;
                    navAgents.Add(agent);
                }
                else
                {
                    Crowd.Add(body.transform, speed);
                }
            }
        }

        static GameObject CreateBody(int index, Vector3 position)
        {
            var root = new GameObject($"BenchAgent{index:000}");
            root.transform.position = position;
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(root.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            capsule.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            return root;
        }

        static void CopySettings(NavMeshAgent from, NavMeshAgent to)
        {
            if (from == null) return;
            to.agentTypeID = from.agentTypeID;
            to.baseOffset = from.baseOffset;
            to.radius = from.radius;
            to.height = from.height;
            to.acceleration = from.acceleration;
            to.angularSpeed = from.angularSpeed;
            to.stoppingDistance = from.stoppingDistance;
            to.autoBraking = from.autoBraking;
            to.autoRepath = from.autoRepath;
            to.obstacleAvoidanceType = from.obstacleAvoidanceType;
        }

        public void Tick(float deltaTime)
        {
            using (NavBenchRecorder.Driver.Auto())
            {
                for (var i = 0; i < timer.Length; i++)
                {
                    timer[i] -= deltaTime;
                    if (timer[i] > 0f) continue;
                    timer[i] += interval;
                    target[i] = random.Next(targets.Count);
                    if (mode == Mode.NavMesh) RequestNavMesh(i);
                    else Enqueue(i);
                }
            }
            if (mode != Mode.AStar) return;
            for (var n = 0; n < maxPathsPerFrame && pending.Count > 0; n++) FindPath(pending.Dequeue());
            Crowd.Tick(deltaTime);
        }

        /// <summary>A*: 남은 길이 rooms를 지나는 알바만 지금 목적지로 다시 길을 찾게 줄 세운다. 줄 세운 수를 돌려준다.</summary>
        public int RepathTouching(IReadOnlyList<int> rooms)
        {
            if (mode != Mode.AStar || rooms.Count == 0) return 0;
            var count = 0;
            for (var i = 0; i < bodies.Count; i++)
            {
                if (!Crowd.PathTouchesRooms(i, world.Grid, rooms)) continue;
                Enqueue(i);
                count++;
            }
            Repaths += count;
            return count;
        }

        void RequestNavMesh(int i)
        {
            var agent = navAgents[i];
            PathRequests++;
            bool ok;
            using (NavBenchRecorder.SetDestination.Auto()) ok = agent.isOnNavMesh && agent.SetDestination(targets[target[i]]);
            if (!ok) PathFailures++;
        }

        void Enqueue(int i)
        {
            if (queued[i]) return;
            queued[i] = true;
            pending.Enqueue(i);
        }

        void FindPath(int i)
        {
            queued[i] = false;
            PathRequests++;
            if (world.Pathfinder.FindPath(Crowd.Position(i), targets[target[i]], snapCells, waypoints) == PathStatus.Ok) Crowd.SetPath(i, waypoints);
            else
            {
                PathFailures++;
                Crowd.Stop(i);
            }
        }

        public void Dispose()
        {
            foreach (var body in bodies)
            {
                if (body != null) Object.Destroy(body);
            }
            bodies.Clear();
            navAgents.Clear();
        }
    }
}
