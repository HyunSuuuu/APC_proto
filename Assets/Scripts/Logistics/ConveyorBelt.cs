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

        /// <summary>철거 · 회차로 벨트 위 화물을 되돌려 받는다 — 가득 찼어도 넣는다 (넘침 허용, GDD 7.10). 출처(Origin)도 원래대로.</summary>
        void ReturnFromBelt(Cargo cargo);
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
    /// 벨트 위 화물은 아무 스택 · 더미에도 없으므로 누구도 집지 못한다. 벨트는 벽처럼 막는다 — 사람 · 알바가 못 지나감 (M20-4).
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
        public IReadOnlyList<Vector3> Points => points;

        /// <summary>설치비 (철거 환불 기준: 설치 상태 철거 50%, 모듈 재배치 철거 100%, GDD 7.10).</summary>
        public int Cost { get; set; }
        public IBeltStart Start => start;
        public IBeltEnd End => end;
        public float Length => length;

        /// <summary>벨트 스캔 구역 (벨트당 1개, 없으면 null, M20-5). 미분류 상자는 그 지점에서 스캔될 때까지 멈춘다.</summary>
        public BeltScanZone ScanZone { get; set; }

        /// <summary>스캔 지점에 와 있는(또는 그 앞에서 오는) 미분류 상자 수 — 하차 알바가 올지 정한다.</summary>
        public int UnsortedBeforeScan
        {
            get
            {
                if (ScanZone == null) return 0;
                var n = 0;
                for (var i = 0; i < cargos.Count; i++)
                {
                    if (cargos[i].Unsorted && distances[i] <= ScanZone.Distance + 0.01f) n++;
                }
                return n;
            }
        }

        /// <summary>스캔 지점에 도착해 멈춘 미분류 상자 (맨 앞 하나). 없으면 null.</summary>
        public Cargo CargoAtScan()
        {
            if (ScanZone == null) return null;
            for (var i = 0; i < cargos.Count; i++)
            {
                if (cargos[i].Unsorted && distances[i] >= ScanZone.Distance - 0.01f && !DOTween.IsTweening(cargos[i].transform)) return cargos[i];
            }
            return null;
        }
        float lastFeed; // 같은 Start의 벨트끼리 번갈아 올리기 (오래 못 받은 벨트 먼저)
        static int fedFrame = -1;

        /// <summary>벨트 위 그 품목 · 출처 화물 수. 클로저 없이 — Warehouse.CountCarried가 빈 상차 칸마다 매 프레임 부른다 (2026-11-25 GC).</summary>
        public static int CountOnBelts(Data.ItemDefinition item, CargoOrigin origin)
        {
            var n = 0;
            foreach (var belt in Registered)
            {
                foreach (var cargo in belt.cargos)
                {
                    if (cargo.Item == item && cargo.Origin == origin) n++;
                }
            }
            return n;
        }

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

        /// <summary>
        /// 이을 수 있는 Start → End: **하차 칸 공간 → 스캔 모듈 팔레트** 하나뿐 (2026-11-24 축소, 옛 7구간 삭제 — GDD 7.10).
        /// </summary>
        public static bool CanConnect(IBeltStart start, IBeltEnd end)
            => start is SharedStorage { Kind: StorageKind.Pallet } && end is SharedStorage { Kind: StorageKind.Scan };

        /// <summary>착지 후 beltPickupDelay가 지났는가 — 쌓이자마자 벨트로 빠져 순간이동처럼 보이지 않게 (GDD 7.10).</summary>
        public static bool Settled(Cargo cargo) => Time.time >= cargo.LandedAt + GameManager.Instance.Database.Logistics.BeltPickupDelay;

        /// <summary>벨트 폭 (m) — 판 · 막는 상자 · 설치 판정이 같이 쓴다.</summary>
        public static float Width => GameManager.Instance.Database.Logistics.BeltWidth;

        /// <summary>a → b 구간 중심선 위 0.25m마다 표본 (양 끝 포함). 판정은 표본마다 벨트 폭 사각형으로.</summary>
        public static void Footprint(Vector3 a, Vector3 b, List<Vector3> output)
        {
            var length = Vector3.Distance(a, b);
            if (length <= 0f) return;
            var steps = Mathf.CeilToInt(length / 0.25f);
            for (var i = 0; i <= steps; i++) output.Add(Vector3.Lerp(a, b, (float)i / steps));
        }

        public static float PathLength(IReadOnlyList<Vector3> path)
        {
            var length = 0f;
            for (var i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            return length;
        }

        /// <summary>설치비 = 길이(m, 올림, 최소 1) × beltCostPerMeter (GDD 7.10 M20-4).</summary>
        public static int CostOf(IReadOnlyList<Vector3> path)
            => Mathf.Max(1, Mathf.CeilToInt(PathLength(path) - 0.01f)) * GameManager.Instance.Database.Logistics.BeltCostPerMeter;

        /// <summary>경로 구간마다 벨트가 막는 바닥 사각형 (x = 월드 x, y = 월드 z, 꺾이는 곳 이어지게 끝을 폭 절반 늘림). 가로 · 세로 구간만.</summary>
        public static void BlockRects(IReadOnlyList<Vector3> path, float inflate, List<Rect> output)
        {
            var half = Width * 0.5f + inflate;
            for (var i = 1; i < path.Count; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                output.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - half, Mathf.Min(a.z, b.z) - half, Mathf.Max(a.x, b.x) + half, Mathf.Max(a.z, b.z) + half));
            }
        }

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
            Staff.StaffNav.NotifyObstacleChanged(belt.points, Width); // 알바 A* 격자에 막는 상자 반영
            return belt;
        }

        /// <summary>
        /// 철거 (GDD 7.10): 벨트 위 화물을 모두 Start로 되돌리고(넘침 허용) 없앤다. 환불은 부르는 쪽 (설치 상태 50% · 모듈 재배치 100%).
        /// </summary>
        public void Dismantle()
        {
            foreach (var cargo in cargos)
            {
                cargo.transform.DOKill();
                if ((start as Object) != null) start.ReturnFromBelt(cargo);
                else cargo.Release();
            }
            cargos.Clear();
            distances.Clear();
            gameObject.SetActive(false); // 바로 목록에서 빠지게 (Destroy는 프레임 끝)
            Staff.StaffNav.NotifyObstacleChanged(points, Width); // 꺼진 막는 상자는 물리에서 빠졌으므로 바로 다시 굽는다
            Destroy(gameObject);
        }

        /// <summary>
        /// 모듈 재배치 때 같이 철거할 벨트인가 (GDD 7.10): Start · End가 그 모듈 안이거나, 경로가 그 모듈 바닥을 지나감.
        /// </summary>
        public bool Touches(Site.SiteModule module)
        {
            if (Owner(start) == module || Owner(end) == module) return true;
            var half = (Vector2)module.Size * (module.CellSize * 0.5f) - Vector2.one * 0.05f; // 경계선 위는 안 침
            var rect = Interaction.InteractionZone.FootprintRect(module.transform, half);
            for (var i = 1; i < points.Count; i++)
            {
                var length = Vector3.Distance(points[i - 1], points[i]);
                var steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
                for (var k = 0; k <= steps; k++)
                {
                    var p = Vector3.Lerp(points[i - 1], points[i], (float)k / steps);
                    if (rect.Contains(new Vector2(p.x, p.z))) return true;
                }
            }
            return false;
        }

        /// <summary>점 p에서 이 벨트 경로까지 가장 가까운 거리 (설치 상태에서 누른 벨트 찾기).</summary>
        public float DistanceTo(Vector3 p)
        {
            var best = float.MaxValue;
            for (var i = 1; i < points.Count; i++)
            {
                var ab = points[i] - points[i - 1];
                var t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - points[i - 1], ab) / ab.sqrMagnitude) : 0f;
                var q = points[i - 1] + ab * t;
                best = Mathf.Min(best, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)));
            }
            return best;
        }

        static Site.SiteModule Owner(object part) => part is Component c && c != null ? c.GetComponentInParent<Site.SiteModule>() : null;

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);

        void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f || (start as Object) == null || (end as Object) == null) return;
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
                if (DOTween.IsTweening(cargos[i].transform)) // 올라가는 점프 중: 첫 칸을 차지한 채 기다림
                {
                    limit = distances[i] - spacing;
                    continue;
                }
                var stop = limit;
                if (ScanZone != null && cargos[i].Unsorted && distances[i] <= ScanZone.Distance + 0.01f) stop = Mathf.Min(stop, ScanZone.Distance); // 스캔될 때까지 지점에서 멈춤 (M20-5)
                var d = Mathf.Max(distances[i], Mathf.Min(distances[i] + speed * dt, stop));
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
                // 꺼낸 자리 → 벨트 첫 점: 플레이어가 집을 때와 같은 포물선 (GDD 7.10)
                var carry = GameManager.Instance.Database.Carry;
                belt.Pose(0f, out var position, out var rotation);
                cargo.transform.DOJump(position, carry.JumpPower, 1, carry.FlyDuration);
                cargo.transform.DORotateQuaternion(rotation, carry.FlyDuration);
            }
        }

        void Place(Cargo cargo, float distance)
        {
            Pose(distance, out var position, out var rotation);
            cargo.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>경로 위 거리 → 바닥 위치 · 진행 방향 (스캔 구역 놓기).</summary>
        public void PoseAt(float distance, out Vector3 point, out Vector3 direction)
        {
            Pose(distance, out var position, out var rotation);
            point = new Vector3(position.x, 0f, position.z);
            direction = rotation * Vector3.forward;
        }

        /// <summary>바닥 점 p에 가장 가까운 경로 위 거리.</summary>
        public float DistanceAlong(Vector3 p)
        {
            var best = float.MaxValue;
            var result = 0f;
            var walked = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                var ab = points[i] - points[i - 1];
                var segment = ab.magnitude;
                var t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - points[i - 1], ab) / ab.sqrMagnitude) : 0f;
                var q = points[i - 1] + ab * t;
                var d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
                if (d < best)
                {
                    best = d;
                    result = walked + segment * t;
                }
                walked += segment;
            }
            return result;
        }

        /// <summary>경로 위 거리 → 화물 위치 · 방향 (벨트 위 높이).</summary>
        void Pose(float distance, out Vector3 position, out Quaternion rotation)
        {
            var height = GameManager.Instance.Database.Logistics.BeltHeight;
            position = points.Count > 0 ? points[0] : transform.position;
            rotation = Quaternion.identity;
            for (var i = 1; i < points.Count; i++)
            {
                var segment = Vector3.Distance(points[i - 1], points[i]);
                if (distance > segment && i < points.Count - 1)
                {
                    distance -= segment;
                    continue;
                }
                var dir = (points[i] - points[i - 1]).normalized;
                position = points[i - 1] + dir * Mathf.Min(distance, segment) + Vector3.up * height;
                rotation = Quaternion.LookRotation(dir);
                return;
            }
        }

        /// <summary>
        /// shortcut: 구간마다 회색 판 하나 (화살표 · 움직이는 무늬 없음) — 벨트 모델이 오면 교체 (GDD 18장 57).
        /// 구간마다 막는 상자(충돌체 + 네비메시 장애물 carving) — 사람 · 알바가 못 지나감 (M20-4). 충돌체가 바닥으로 구워지지 않게 굽기에서 뺀다 (선반과 같음).
        /// </summary>
        void BuildVisual()
        {
            var config = GameManager.Instance.Database.Logistics;
            var width = Width;
            gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var block = new GameObject("BeltBlock");
                block.transform.SetParent(transform, false);
                block.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(b - a));
                var size = new Vector3(width, config.BeltBlockHeight, Vector3.Distance(a, b) + width);
                var center = new Vector3(0f, config.BeltBlockHeight * 0.5f, 0f);
                var box = block.AddComponent<BoxCollider>();
                box.center = center;
                box.size = size;
                var obstacle = block.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.center = center;
                obstacle.size = size;
                obstacle.carving = true;
            }
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
