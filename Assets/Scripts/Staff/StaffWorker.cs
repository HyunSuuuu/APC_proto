using APC.Core;
using APC.Data;
using APC.Interaction;
using APC.Logistics;
using APC.Orders;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

namespace APC.Staff
{
    /// <summary>
    /// 알바 1명 (GDD 11). NavMeshAgent로 움직이고, 플레이어와 같은 Interactor + CarryStack으로 존과 상호작용한다.
    /// 작업 상태 머신 (GDD 11.4): 대기 → 대상 탐색 · 예약 → 집으러 이동 → 집기 → 운반 → 내려놓기 → 대기.
    /// - 집는 곳은 역할로 정한다: 하차 = 트럭 픽업 존, 분류 = 팔레트 둘레 존, 피킹 = 선반 둘레 존.
    /// - 내려놓는 곳은 **들고 있는 화물**로 정한다 (그 화물을 받는 존). 그래서 역할을 바꿔도 들고 있던 화물은 원래 목적지로 간다.
    /// - 존마다 작업 지점(WorkSlot)이 있다. 빈 슬롯을 예약하고 가서 그 지점에서만 상호작용한다. 빈 슬롯이 없으면 그 자리에서 멈춰 기다린다.
    /// - 할 일이 없으면 역할 존의 빈 슬롯에 가서 기다린다 (Parked, 상호작용 안 함).
    /// - 작업 고정 · 전환 쿨타임 · 가이드 화살표는 플레이어 전용이라 쓰지 않는다 (GDD 5.3).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Interactor), typeof(CarryStack))]
    public sealed class StaffWorker : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        const float ArriveDistance = 0.2f;
        // 도착했는데 이만큼(초) 동안 상호작용이 시작되지 않으면 포기한다 (슬롯이 존 밖에 있는 등 배치 오류 대비)
        const float StuckTimeout = 3f;

        public enum Phase { Idle, Moving, Working, Parked, Waiting }

        enum Search { None, NoSlot, Found }

        [SerializeField, Tooltip("역할 색을 칠할 몸통")] Renderer body;
        [SerializeField, Tooltip("미지급 표시 (머리 위 빨간 ₩, 꺼진 채)")] GameObject unpaidIcon;
        [SerializeField, Tooltip("퇴사할 때 깜빡이는 색")] Color angryColor = new(0.9f, 0.1f, 0.1f);

        NavMeshAgent agent;
        Interactor interactor;
        CarryStack stack;
        StaffRules rules;
        MaterialPropertyBlock block;

        CargoZone zone;
        WorkSlot slot;
        bool dropping;
        bool parking;
        bool leaving;
        System.Action releaseReservation; // 지금 잡고 있는 예약을 푸는 함수 (없으면 null)
        float thinkTimer;
        float workTime;
        float idleTime;
        int lastCount;
        float iconBaseY;
        Tween iconTween;

        public StaffRole Role { get; private set; }
        public StaffStats Stats { get; private set; }
        public Phase State { get; private set; }
        public CarryStack Stack => stack;

        /// <summary>고용 순서 번호 (배치 탭 이름 `알바 N`).</summary>
        public int Number { get; internal set; }
        public string DisplayName => $"알바 {Number}";

        /// <summary>해고됨: 명단에서 빠졌고, 들고 있던 화물을 배달한 뒤 생성 지점으로 걸어가 사라진다.</summary>
        public bool Fired { get; private set; }

        /// <summary>밀린 시급 (GDD 11.7). 0보다 크면 미지급 상태 — 머리 위 ₩. 다음 정산 때 `밀린 시급 + 이번 시급`을 못 받으면 퇴사.</summary>
        public int Owed { get; private set; }

        /// <summary>이번 파트에 낼 시급 비율. 고용한 파트는 고용 때 남은 시간 비율, 정산 뒤엔 1.</summary>
        public float PartShare { get; internal set; } = 1f;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            interactor = GetComponent<Interactor>();
            stack = GetComponent<CarryStack>();
            interactor.RestrictToTarget = true; // 목표 존의 작업 지점에서만 상호작용
            if (unpaidIcon != null)
            {
                iconBaseY = unpaidIcon.transform.localPosition.y;
                unpaidIcon.SetActive(false);
            }
        }

        /// <summary>밀린 시급을 정한다 (정산 [확인] 때 StaffService가 부름). 0보다 크면 머리 위 ₩이 둥실거린다.</summary>
        internal void SetOwed(int amount)
        {
            Owed = Mathf.Max(0, amount);
            if (unpaidIcon == null) return;
            iconTween?.Kill();
            unpaidIcon.SetActive(Owed > 0);
            if (Owed <= 0) return;
            var t = unpaidIcon.transform;
            t.localPosition = new Vector3(t.localPosition.x, iconBaseY, t.localPosition.z);
            iconTween = t.DOLocalMoveY(iconBaseY + 0.15f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        /// <summary>
        /// 퇴사 (GDD 11.7, 두 번째 미지급): 해고처럼 명단에서 빠지고, 몸통이 빨갛게 깜빡이며 제자리에서 두 번 뛴 뒤
        /// 들고 있던 화물을 배달하고 생성 지점으로 걸어 나가 사라진다.
        /// </summary>
        public void Quit()
        {
            if (Fired) return;
            Fire();
            SetOwed(0);
            if (agent.isOnNavMesh) agent.isStopped = true;
            var roleColor = rules.GetRoleColor(Role);
            var bodyTransform = body.transform;
            DOTween.Sequence()
                .Append(DOVirtual.Float(0f, 1f, 0.9f, t => SetColor(Color.Lerp(roleColor, angryColor, Mathf.PingPong(t * 6f, 1f)))))
                .Join(bodyTransform.DOLocalJump(bodyTransform.localPosition, 0.5f, 2, 0.9f))
                .OnComplete(() =>
                {
                    SetColor(roleColor);
                    if (agent.isOnNavMesh) agent.isStopped = false;
                })
                .SetLink(gameObject);
        }

        public void Setup(StaffRole role, StaffStats stats)
        {
            rules = GameManager.Instance.Database.Staff;
            Stats = stats;
            agent.speed = rules.GetMoveSpeed(stats.moveSpeed);
            stack.SetCapacity(rules.GetCarryCapacity(stats.carry));
            interactor.Configure(rules.PickInterval, rules.InteractionDelay);
            SetRole(role);
        }

        /// <summary>
        /// 역할 변경 (GDD 11.3). 빈손으로 가는 중이거나 기다리는 중이면 바로 새 역할로 다시 고른다.
        /// 집는 중이거나 들고 있으면 배달을 끝낸 뒤 새 역할이 된다.
        /// </summary>
        public void SetRole(StaffRole role)
        {
            Role = role;
            SetColor(rules.GetRoleColor(role));
            if (State == Phase.Parked || (State == Phase.Moving && !dropping)) Abort();
        }

        /// <summary>
        /// 해고 (GDD 11.3). 바로 명단에서 빠진다 (빈자리는 다시 고용 가능).
        /// 배달하러 가는 중 · 내려놓는 중이면 끝내고, 아니면 지금 일을 멈춘다. 손에 화물이 남아 있으면 배달부터 한다.
        /// </summary>
        public void Fire()
        {
            if (Fired) return;
            Fired = true;
            if (StaffService.Instance != null) StaffService.Instance.Remove(this);
            if (!dropping || State is not (Phase.Moving or Phase.Working)) Abort();
        }

        void OnDisable()
        {
            ReleaseAll();
            if (StaffService.Instance != null) StaffService.Instance.Remove(this);
        }

        void Update()
        {
            var deltaTime = Time.deltaTime;
            if (rules == null || deltaTime <= 0f) return;

            thinkTimer -= deltaTime;
            var think = thinkTimer <= 0f;
            if (think) thinkTimer = rules.ThinkInterval;

            switch (State)
            {
                case Phase.Moving when leaving:
                    if (Arrived()) Destroy(gameObject);
                    break;
                case Phase.Moving when parking:
                    if (think) Think(); // 기다리러 가는 중에도 일이 생기면 바로 간다
                    if (State == Phase.Moving && parking && Arrived())
                    {
                        parking = false;
                        State = Phase.Parked;
                    }
                    break;
                case Phase.Moving:
                    if (think && !StillValid()) // 가는 중에 대상이 사라지면 예약을 풀고 다시 고른다 (GDD 11.4)
                    {
                        Abort();
                        Think();
                    }
                    else if (Arrived()) BeginWork();
                    break;
                case Phase.Working:
                    TickWork(deltaTime);
                    break;
                default:
                    if (think) Think();
                    break;
            }
        }

        void Think()
        {
            if (Fired && stack.Count.CurrentValue == 0)
            {
                Leave();
                return;
            }
            if (stack.Count.CurrentValue > 0)
            {
                // 배달할 곳이 없거나 빈 슬롯이 없으면 그 자리에서 기다린다
                if (Find(drop: true, park: false, out var dropZone, out var dropSlot) == Search.Found) Go(dropZone, dropSlot, drop: true, park: false);
                else Hold();
                return;
            }

            var result = Find(drop: false, park: false, out var pickZone, out var pickSlot);
            if (result == Search.Found)
            {
                Go(pickZone, pickSlot, drop: false, park: false);
                return;
            }
            var waitingAtRoleZone = State == Phase.Parked || (State == Phase.Moving && parking);
            if (result == Search.NoSlot)
            {
                if (!waitingAtRoleZone) Hold(); // 일은 있는데 빈 슬롯이 없음 → 그 자리에서 멈춤
                return;
            }
            if (waitingAtRoleZone) return;

            if (Find(drop: false, park: true, out var parkZone, out var parkSlot) == Search.Found) Go(parkZone, parkSlot, drop: false, park: true);
            else Hold();
        }

        /// <summary>
        /// 가장 가까운 작업 지점 찾기. drop = 들고 있는 화물을 받는 존, 아니면 역할 존(park면 일이 없어도).
        /// 조건에 맞는 존은 있는데 빈 슬롯이 없으면 NoSlot.
        /// </summary>
        Search Find(bool drop, bool park, out CargoZone bestZone, out WorkSlot bestSlot)
        {
            bestZone = null;
            bestSlot = null;
            var matched = false;
            var bestDistance = float.MaxValue;
            var from = transform.position;

            foreach (var candidate in InteractionZone.Active)
            {
                if (candidate is not CargoZone cargoZone || cargoZone.Slots.Count == 0) continue;
                var ok = drop ? cargoZone.CanReceiveAny(interactor) : IsRoleZone(cargoZone) && (park || HasWork(cargoZone));
                if (!ok) continue;
                matched = true;

                var free = WorkSlot.FindNearestFree(cargoZone.Slots, this, from);
                if (free == null) continue;
                var distance = (free.transform.position - from).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestZone = cargoZone;
                bestSlot = free;
                bestDistance = distance;
            }
            return bestZone != null ? Search.Found : matched ? Search.NoSlot : Search.None;
        }

        bool IsRoleZone(CargoZone candidate) => Role switch
        {
            StaffRole.Unload => candidate is TruckPickupZone,
            StaffRole.Sort => candidate is StorageZone,
            _ => candidate is ShelfZone,
        };

        // 하차는 팔레트에 빈자리가 있을 때만 (들고 갈 곳이 없으면 집지 않는다). 분류 · 피킹은 존 규칙에 이미 빈자리 · 수요가 들어 있다.
        bool HasWork(CargoZone candidate)
            => candidate.HasCargoFor(interactor) && (Role != StaffRole.Unload || SharedStorage.AnyHasSpace());

        void Go(CargoZone target, WorkSlot targetSlot, bool drop, bool park)
        {
            if (slot != null && slot != targetSlot) slot.Release(this);
            ReleaseReservation();

            zone = target;
            slot = targetSlot;
            slot.TryClaim(this);
            dropping = drop;
            parking = park;
            interactor.Target = null;
            if (!drop && !park) Reserve();

            agent.SetDestination(slot.transform.position);
            State = Phase.Moving;
        }

        bool Arrived()
        {
            if (agent.pathPending) return false;
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[StaffWorker] {name}: '{(slot != null ? slot.name : "출구")}'까지 길이 없습니다.", this);
                Hold();
                return false;
            }
            return agent.remainingDistance <= ArriveDistance;
        }

        /// <summary>가는 중인 목표가 아직 유효한가. 예약은 잠깐 풀고 다시 확인해 다시 잡는다 (자기 예약 때문에 일이 없어 보이지 않게).</summary>
        bool StillValid()
        {
            if (zone == null || !zone.isActiveAndEnabled) return false;
            if (dropping) return zone.CanReceiveAny(interactor);

            ReleaseReservation();
            var ok = IsRoleZone(zone) && HasWork(zone);
            if (ok) Reserve();
            return ok;
        }

        void BeginWork()
        {
            ReleaseReservation(); // 도착하면 존이 실제 수요로 판단한다 (들고 있는 수가 예약을 대신함)
            interactor.AllowDrop = dropping;
            interactor.AllowPick = !dropping;
            interactor.Target = zone;
            workTime = 0f;
            idleTime = 0f;
            lastCount = stack.Count.CurrentValue;
            State = Phase.Working;
        }

        void TickWork(float deltaTime)
        {
            workTime += deltaTime;
            var count = stack.Count.CurrentValue;
            if (count != lastCount)
            {
                lastCount = count;
                idleTime = 0f;
            }
            else if (interactor.IsReady)
            {
                idleTime += deltaTime;
            }

            var done = dropping ? count == 0 : stack.IsFull;
            var stuck = workTime >= rules.InteractionDelay + StuckTimeout && interactor.CurrentZone != zone;
            if (stuck) Debug.LogWarning($"[StaffWorker] {name}: 작업 지점 '{slot.name}'이 존 '{zone.name}' 밖에 있습니다.", slot);
            if (done || idleTime >= rules.WorkIdleTimeout || stuck) FinishWork();
        }

        void FinishWork()
        {
            ReleaseAll();
            State = Phase.Idle;
            thinkTimer = 0f; // 다음 일을 바로 고른다
        }

        /// <summary>해고 후 빈손: 생성 지점(사무실 PC 앞)으로 걸어가 사라진다.</summary>
        void Leave()
        {
            ReleaseAll();
            leaving = true;
            var spawn = StaffSpawnPoint.Current;
            agent.SetDestination(spawn != null ? spawn.transform.position : transform.position);
            State = Phase.Moving;
        }

        /// <summary>그 자리에서 멈춰 기다린다 (빈 슬롯 없음 / 배달할 곳 없음).</summary>
        void Hold()
        {
            ReleaseAll();
            if (agent.isOnNavMesh) agent.ResetPath();
            State = Phase.Waiting;
        }

        void Abort()
        {
            ReleaseAll();
            if (agent.isOnNavMesh) agent.ResetPath();
            State = Phase.Idle;
            thinkTimer = 0f;
        }

        void ReleaseAll()
        {
            ReleaseReservation();
            if (slot != null) slot.Release(this);
            slot = null;
            zone = null;
            parking = false;
            interactor.Target = null;
        }

        /// <summary>
        /// 집으러 출발할 때 가져갈 만큼 예약한다 (GDD 8.4). 여러 알바가 같은 화물 · 수요를 두고 같은 곳으로 가지 않게.
        /// 하차 = 트럭 남은 화물, 분류 = 선반 빈자리(품목별), 피킹 = 주문 수요. 도착하면 푼다 (그 뒤로는 들고 있는 수가 대신 잡힌다).
        /// </summary>
        void Reserve()
        {
            var room = stack.Capacity.CurrentValue - stack.Count.CurrentValue;
            if (room <= 0) return;

            switch (zone)
            {
                case TruckPickupZone pickup when pickup.Bay.DockedTruck != null:
                {
                    var truck = pickup.Bay.DockedTruck;
                    var count = Mathf.Min(room, truck.Available);
                    if (count <= 0) return;
                    truck.Reserve(count);
                    releaseReservation = () => { if (truck != null) truck.Unreserve(count); };
                    break;
                }
                case StorageZone storageZone when Warehouse.Instance != null:
                {
                    var warehouse = Warehouse.Instance;
                    var plan = storageZone.Storage.PlanForShelves(room);
                    foreach (var pair in plan) warehouse.ReserveShelfSpace(pair.Key, pair.Value);
                    releaseReservation = () => { foreach (var pair in plan) warehouse.UnreserveShelfSpace(pair.Key, pair.Value); };
                    break;
                }
                case ShelfZone shelfZone when OrderService.Instance != null && shelfZone.Shelf.Item != null:
                {
                    var orders = OrderService.Instance;
                    var item = shelfZone.Shelf.Item;
                    var count = Mathf.Min(room, orders.GetDemand(item));
                    if (count <= 0) return;
                    orders.Reserve(item, count);
                    releaseReservation = () => orders.Unreserve(item, count);
                    break;
                }
            }
        }

        void ReleaseReservation()
        {
            var release = releaseReservation;
            releaseReservation = null;
            release?.Invoke();
        }

        void SetColor(Color color)
        {
            if (body == null) return;
            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            body.SetPropertyBlock(block);
        }
    }
}
