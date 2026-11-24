using System.Collections.Generic;
using APC.Core;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>벨트 Start (GDD 7.10): 벨트에 올릴 화물 1개를 꺼낸다 — 그 End가 받을 것만. 없으면 null.</summary>
    public interface IBeltStart
    {
        Cargo TakeForBelt(IBeltEnd end);
    }

    /// <summary>
    /// 벨트 End (GDD 7.10). `Wants` = 이 화물을 보내도 되는가 (종류 + 이미 벨트로 오는 수를 뺀 자리, Start가 꺼내기 전에 묻는다),
    /// `CanTakeFromBelt` = 맨 앞 화물을 지금 받을 자리가 있는가, `ReceiveFromBelt` = 받기.
    /// </summary>
    public interface IBeltEnd
    {
        bool Wants(Cargo cargo);
        bool CanTakeFromBelt(Cargo cargo);
        void ReceiveFromBelt(Cargo cargo);
    }

    /// <summary>
    /// 컨베이어 벨트 1개 (GDD 7.10, M18-1). 꺾임 점을 잇는 경로를 따라 화물을 `beltCellSize` 간격 · 초당 `beltCellsPerSecond`칸으로 옮긴다.
    /// Start는 첫 칸이 비면 하나 올리고, End가 못 받으면 맨 앞부터 줄지어 멈췄다가 자리가 나면 다시 간다.
    /// 벨트 위 화물은 아무 스택 · 더미에도 없으므로 누구도 집지 못한다. 바닥 장치라 충돌체 · 네비메시 영향 없음.
    /// </summary>
    public sealed class ConveyorBelt : MonoBehaviour
    {
        static readonly List<ConveyorBelt> Registered = new();

        /// <summary>있는 모든 벨트 ('찼다' 계산에서 벨트 위 수를 셀 때).</summary>
        public static IReadOnlyList<ConveyorBelt> All => Registered;

        readonly List<Vector3> points = new();
        readonly List<Cargo> cargos = new();   // 0 = End에 가장 가까운 것
        readonly List<float> distances = new(); // 경로 위 거리 (0 = Start)
        IBeltStart start;
        IBeltEnd end;
        float length;

        public IReadOnlyList<Cargo> Cargos => cargos;
        public IBeltStart Start => start;
        public IBeltEnd End => end;
        float lastFeed; // 같은 Start의 벨트끼리 번갈아 올리기 (오래 못 받은 벨트 먼저)
        static int fedFrame = -1;

        /// <summary>벨트 위 화물 중 조건에 맞는 수 (End가 맞는 벨트만). '찼다' 계산에 손에 든 수처럼 더한다 (GDD 7.10).</summary>
        public static int CountOnBelts(System.Func<ConveyorBelt, bool> beltMatch, System.Func<Cargo, bool> cargoMatch)
        {
            var n = 0;
            foreach (var belt in Registered)
            {
                if (!beltMatch(belt)) continue;
                foreach (var cargo in belt.cargos)
                {
                    if (cargoMatch(cargo)) n++;
                }
            }
            return n;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        /// <summary>벨트를 만든다. path = Start 쪽 끝부터 꺾임 점 · End 쪽 끝 (월드, 가로 · 세로로만 이어짐).</summary>
        public static ConveyorBelt Create(IBeltStart start, IBeltEnd end, IReadOnlyList<Vector3> path)
        {
            var belt = new GameObject("ConveyorBelt").AddComponent<ConveyorBelt>();
            belt.start = start;
            belt.end = end;
            foreach (var p in path)
            {
                if (belt.points.Count == 0 || belt.points[^1] != p) belt.points.Add(p); // 길이 0 구간 빼기 (방향을 못 정함)
            }
            for (var i = 1; i < belt.points.Count; i++) belt.length += Vector3.Distance(belt.points[i - 1], belt.points[i]);
            belt.BuildVisual();
            return belt;
        }

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);

        void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f || (start as Object) == null || (end as Object) == null) return;
            // 상차 시간이 끝나면(회차) 선반 → 상차 벨트 위 상자는 꺼낸 구역 선반으로 (GDD 7.10 · 7.6)
            if (end is TruckLoadZone && !Economy.PartService.OutboundOpen && start is IBeltEnd back)
            {
                foreach (var c in cargos) back.ReceiveFromBelt(c);
                cargos.Clear();
                distances.Clear();
            }
            var config = GameManager.Instance.Database.Logistics;
            var spacing = config.BeltCellSize;
            var speed = spacing * config.BeltCellsPerSecond;

            if (cargos.Count > 0 && distances[0] >= length && end.CanTakeFromBelt(cargos[0]))
            {
                end.ReceiveFromBelt(cargos[0]);
                cargos.RemoveAt(0);
                distances.RemoveAt(0);
            }

            // 앞 화물과 한 칸 간격을 두고 따라감 (End가 막히면 줄지어 멈춤)
            var limit = length;
            for (var i = 0; i < cargos.Count; i++)
            {
                var d = Mathf.Max(distances[i], Mathf.Min(distances[i] + speed * dt, limit));
                distances[i] = d;
                Place(cargos[i], d);
                limit = d - spacing;
            }

            if (fedFrame != Time.frameCount)
            {
                fedFrame = Time.frameCount;
                FeedAll();
            }
        }

        bool ReadyToFeed => cargos.Count == 0 || distances[^1] >= GameManager.Instance.Database.Logistics.BeltCellSize;

        /// <summary>한 프레임에 한 번: 첫 칸이 빈 벨트마다 Start에서 하나 올린다. 오래 못 받은 벨트부터 → 같은 Start의 벨트끼리 번갈아.</summary>
        static void FeedAll()
        {
            var ready = new List<ConveyorBelt>();
            foreach (var belt in Registered)
            {
                if (belt.ReadyToFeed && (belt.start as Object) != null && (belt.end as Object) != null) ready.Add(belt);
            }
            ready.Sort((a, b) => a.lastFeed.CompareTo(b.lastFeed));
            foreach (var belt in ready)
            {
                var cargo = belt.start.TakeForBelt(belt.end);
                if (cargo == null) continue;
                belt.lastFeed = Time.time;
                cargo.transform.DOKill();
                cargo.transform.SetParent(belt.transform, true);
                belt.cargos.Add(cargo);
                belt.distances.Add(0f);
                belt.Place(cargo, 0f);
            }
        }

        void Place(Cargo cargo, float distance)
        {
            var height = GameManager.Instance.Database.Logistics.BeltHeight;
            for (var i = 1; i < points.Count; i++)
            {
                var segment = Vector3.Distance(points[i - 1], points[i]);
                if (distance > segment && i < points.Count - 1)
                {
                    distance -= segment;
                    continue;
                }
                var dir = (points[i] - points[i - 1]).normalized;
                cargo.transform.SetPositionAndRotation(points[i - 1] + dir * Mathf.Min(distance, segment) + Vector3.up * height, Quaternion.LookRotation(dir));
                return;
            }
        }

        /// <summary>shortcut: 구간마다 회색 판 하나 (화살표 · 움직이는 무늬 없음) — 벨트 모델이 오면 교체 (GDD 18장 57)</summary>
        void BuildVisual()
        {
            var config = GameManager.Instance.Database.Logistics;
            var width = config.BeltCellSize * 0.7f;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(piece.GetComponent<Collider>());
                piece.name = "BeltPiece";
                piece.transform.SetParent(transform, false);
                piece.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * config.BeltHeight * 0.5f, Quaternion.LookRotation(b - a));
                piece.transform.localScale = new Vector3(width, config.BeltHeight, Vector3.Distance(a, b) + width); // 꺾이는 곳이 이어지게 끝을 늘림
                if (config.BeltMaterial != null) piece.GetComponent<Renderer>().sharedMaterial = config.BeltMaterial;
            }
        }
    }
}
