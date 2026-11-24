using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Interaction;
using APC.Logistics;
using APC.Orders;
using APC.Site;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.AI;

namespace APC.Staff
{
    /// <summary>
    /// 알바 1명 (GDD 11). NavMeshAgent로 움직이고, 플레이어와 같은 Interactor + CarryStack으로 존과 상호작용한다.
    /// 작업 상태 머신 (GDD 11.4): 대기 → 대상 탐색 · 예약 → 집으러 이동 → 집기 → 운반 → 내려놓기 → 대기.
    /// - 집는 곳은 역할로 정한다: 하차 = 트럭 픽업 존, 분류 = 팔레트 둘레 존, 피킹 = 선반 둘레 존.
    /// - 내려놓는 곳은 **들고 있는 화물**로 정한다 (그 화물을 받는 존). 그래서 역할을 바꿔도 들고 있던 화물은 원래 목적지로 간다.
    /// - 2026-11-19 알바 상호작용 개편: 작업 자리(WorkSlot)는 **예약하지 않는다**. 존 사각형 안에 들어가 멈추면 일한다 (플레이어와 같음).
    ///   서는 위치 = 후보(WorkSlot) 중 사람 적은 곳 → 다 차면 존 안에서 비켜 서기. 존 고르기 = 거리 + 같은 직무 몰림 벌점.
    ///   재포장 테이블만 1명 (RepackTable.TryClaim). 화물 예약(트럭 · 선반 빈자리 · 주문 수요)은 그대로.
    /// - 할 일이 없으면 마지막 일한 존 밖으로 비켜 선 곳(일한 적 없으면 생성 지점 근처)에서 기다린다 (Parked, 아무 존도 막지 않음).
    /// - 작업 고정 · 전환 쿨타임 · 가이드 화살표는 플레이어 전용이라 쓰지 않는다 (GDD 5.3).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Interactor), typeof(CarryStack))]
    public sealed class StaffWorker : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly List<StaffWorker> AllWorkers = new();

        const float ArriveDistance = 0.2f;
        // 도착했는데 이만큼(초) 동안 상호작용이 시작되지 않으면 포기한다 (서는 위치가 존 밖에 있는 등 배치 오류 대비)
        const float StuckTimeout = 3f;
        // 서는 · 대기 위치를 네비메시에 붙일 때 찾는 반경 (m)
        const float NavSnapRadius = 0.3f;
        // 대기 위치는 어떤 존 사각형에서도 이만큼(m) 떨어진다 (몸이 존에 걸쳐 바닥 색이 켜지지 않게, 알바 반경 0.3 근처)
        const float WaitZoneClearance = 0.2f;

        public enum Phase { Idle, Moving, Working, Parked, Waiting }

        /// <summary>반품 알바가 지금 찾는 일 (GDD 11.3 '막히는 곳부터').</summary>
        enum ReturnsFocus { Any, TrashFull, Pallet, Trash }

        [SerializeField, Tooltip("역할 색을 칠할 것 (모델이 있으면 발밑 원판)")] Renderer body;
        [SerializeField, Tooltip("외형 후보. [0] = 물류 유니폼, [1..] = 코스튬 (Setup에서 하나만 켠다, GDD 11.1). 비우면 그대로")] GameObject[] models;
        [SerializeField, Tooltip("미지급 표시 (머리 위 빨간 ₩, 꺼진 채)")] GameObject unpaidIcon;
        [SerializeField, Tooltip("지게차 모델 (Prop/Forklift + 포크 Plastic_Pallet, 꺼진 채). 지게차 직무일 때만 켠다 (M11-8)")] GameObject forklift;
        [SerializeField, Tooltip("지게차 포크 팔레트 위 — 큰 상자가 한 줄로 쌓이는 바닥")] Transform forkliftAnchor;
        [SerializeField, Tooltip("퇴사할 때 깜빡이는 색")] Color angryColor = new(0.9f, 0.1f, 0.1f);

        NavMeshAgent agent;
        Interactor interactor;
        CarryStack stack;
        StaffRules rules;
        MaterialPropertyBlock block;

        CargoZone zone;
        RepackTable gauge; // 반품 알바가 서서 재포장하는 테이블 (zone 대신, 1명 예약)
        ReturnsFocus focus;
        bool hasStand;
        Vector3 standLocal; // 서는 위치 (대상 존 로컬 — 모듈이 옮겨져도 같이 따라간다)
        InteractionZone lastZone; // 마지막으로 일한 존 (대기 위치 기준)
        bool hasWait;
        Vector3 waitPoint; // 대기 위치 (월드)
        bool dropping;
        bool scanning; // 하차 알바가 하차 칸 공간을 스캔하러 감 · 스캔 중 (GDD 7.2a)
        int pickBudget = -1; // 보관 알바: 이번에 집을 최대 수 = 출발할 때 스캔돼 있던 상자 (GDD 7.2a). -1 = 제한 없음
        bool parking;
        bool leaving;
        bool repathPending; // 모듈 재배치 알림을 받았고 팝 연출이 끝나기를 기다리는 중
        System.Action releaseReservation; // 지금 잡고 있는 예약을 푸는 함수 (없으면 null)
        float thinkTimer;
        float workTime;
        float idleTime;
        int lastCount;
        float iconBaseY;
        Tween iconTween;

        public StaffRole Role { get; private set; }
        public bool Costume { get; private set; }
        public StaffStats Stats { get; private set; }
        public Phase State { get; private set; }
        public CarryStack Stack => stack;

        /// <summary>지금 가고 있거나 일하는 존 (재포장 테이블 포함). 없으면 null.</summary>
        public InteractionZone TargetZone => gauge != null ? gauge : zone;

        /// <summary>고용 순서 번호 (배치 탭 이름 `알바 N`).</summary>
        public int Number { get; internal set; }
        public string DisplayName => $"알바 {Number}";

        /// <summary>해고됨: 명단에서 빠졌고, 들고 있던 화물을 배달한 뒤 생성 지점으로 걸어가 사라진다.</summary>
        public bool Fired { get; private set; }

        /// <summary>밀린 시급 (GDD 11.7). 0보다 크면 미지급 상태 — 머리 위 ₩. 다음 정산 때 `밀린 시급 + 이번 시급`을 못 받으면 퇴사.</summary>
        public int Owed { get; private set; }

        /// <summary>이번 파트에 낼 시급 비율. 고용한 파트는 고용 때 남은 시간 비율, 정산 뒤엔 1.</summary>
        public float PartShare { get; internal set; } = 1f;

        /// <summary>
        /// 이 알바가 차지하는(차지할) 바닥 위치: 가는 중 · 일하는 중이면 서는 위치, 대기하러 가는 중 · 대기 중이면 대기 위치, 아니면 지금 위치.
        /// 다른 알바가 서는 · 대기 위치를 고를 때 겹치지 않게 쓴다 (예약 아님).
        /// </summary>
        Vector3 OccupiedPosition
        {
            get
            {
                var target = TargetZone;
                if (hasStand && target != null) return target.transform.TransformPoint(standLocal);
                if (hasWait && (State == Phase.Parked || parking)) return waitPoint;
                return transform.position;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AllWorkers.Clear();
            PathRequests = NoPathCount = 0;
        }

        // 성능 측정용 (PerformanceHUD가 읽는다): 길 요청 수 · 길 없음 수 — A* 전환 비교 기준 (2026-11-23)
        public static int PathRequests { get; private set; }
        public static int NoPathCount { get; private set; }

        bool SetPath(Vector3 point)
        {
            PathRequests++;
            return agent.SetDestination(point);
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            interactor = GetComponent<Interactor>();
            stack = GetComponent<CarryStack>();
            interactor.RestrictToTarget = true; // 목표 존에서만 상호작용
            interactor.AllowScan = false; // 하차 알바가 스캔하러 갔을 때만 켠다 (GDD 7.2a)
            if (unpaidIcon != null)
            {
                iconBaseY = unpaidIcon.transform.localPosition.y;
                unpaidIcon.SetActive(false);
            }
        }

        void OnEnable() => AllWorkers.Add(this);

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
            var bodyTransform = models != null && models.Length > 0 ? models[0].transform.parent : body.transform; // 모델이면 모델째 뛴다
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

        /// <param name="costume">true면 models[1..] 중 무작위 코스튬, 아니면 models[0] 물류 유니폼 (GDD 11.1).</param>
        public void Setup(StaffRole role, StaffStats stats, bool costume)
        {
            if (models != null && models.Length > 0)
            {
                var pick = costume && models.Length > 1 ? Random.Range(1, models.Length) : 0;
                for (var i = 0; i < models.Length; i++) models[i].SetActive(i == pick);
                Costume = pick > 0;
            }
            rules = GameManager.Instance.Database.Staff;
            Stats = stats;
            agent.speed = rules.GetMoveSpeed(stats.moveSpeed);
            interactor.Configure(rules.PickInterval, rules.InteractionDelay);
            SetRole(role);
        }

        /// <summary>직무를 정한다 (고용 때 한 번 — 직무 변경은 삭제, GDD 11.3 · 2026-11-18).</summary>
        void SetRole(StaffRole role)
        {
            Role = role;
            stack.SetCapacity(rules.GetCarry(role, Stats.carry)); // 반품 12 · 지게차 4는 스탯 무관 (GDD 11.3)
            var isForklift = role == StaffRole.Forklift;
            interactor.Forklift = isForklift; // D 가구만 다룸 (Warehouse.CanHandle)
            if (forklift != null) forklift.SetActive(isForklift);
            if (isForklift && forkliftAnchor != null) stack.UseAnchor(forkliftAnchor);
            if (TryGetComponent<Characters.CharacterAnimator>(out var animator)) animator.UseRiding = isForklift; // riding 애니
            SetColor(rules.GetRoleColor(role));
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

        void Start()
        {
            if (SiteGrid.Instance != null) SiteGrid.Instance.OnMoved.Subscribe(_ => Repath()).AddTo(this);
        }

        /// <summary>모듈 재배치 알림 (GDD 4.4). 옮긴 모듈은 팝 연출(크기 0 → 1) 중이라 바로 계산하지 않고, 연출이 끝난 뒤 판단 때 <see cref="ApplyRepath"/>.</summary>
        void Repath()
        {
            if (rules == null || leaving) return;
            repathPending = true;
        }

        /// <summary>존이 제자리 크기(1)인가. 해금 · 재배치 팝 연출 중(크기 0 → 1)인 존은 위치 계산이 틀어지므로 쓰지 않는다.</summary>
        static bool IsSettled(InteractionZone target) => Mathf.Abs(target.transform.lossyScale.x - 1f) < 0.01f;

        /// <summary>
        /// 모듈 재배치 (GDD 4.4): 서는 위치는 대상 존 로컬로 들고 있어 존과 같이 옮겨진다 — 1m 넘게 옮겨갔으면 새 자리로 다시 간다.
        /// 대기 중이면 대기 위치를 다시 고른다 (옮겨진 모듈이 대기 자리를 덮었을 수 있음).
        /// </summary>
        void ApplyRepath()
        {
            var reference = TargetZone != null ? TargetZone : lastZone;
            if (reference != null && reference.isActiveAndEnabled && !IsSettled(reference)) return; // 팝 연출이 끝난 뒤에
            repathPending = false;
            if (leaving || !agent.isOnNavMesh) return;
            if (State == Phase.Parked || (State == Phase.Moving && parking))
            {
                Park();
                return;
            }
            var target = TargetZone;
            if (target == null || !hasStand) return;
            var stand = target.transform.TransformPoint(standLocal);
            switch (State)
            {
                case Phase.Moving when (agent.destination - stand).sqrMagnitude > 1f:
                    SetPath(stand);
                    break;
                case Phase.Working when (transform.position - stand).sqrMagnitude > 1f:
                    interactor.Target = null;
                    State = Phase.Moving;
                    SetPath(stand);
                    break;
            }
        }

        void OnDisable()
        {
            ReleaseAll();
            AllWorkers.Remove(this);
            if (StaffService.Instance != null) StaffService.Instance.Remove(this);
        }

        void Update()
        {
            var deltaTime = Time.deltaTime;
            if (rules == null || deltaTime <= 0f) return;

            thinkTimer -= deltaTime;
            var think = thinkTimer <= 0f;
            if (think) thinkTimer = rules.ThinkInterval;
            if (think && repathPending) ApplyRepath();

            switch (State)
            {
                case Phase.Moving when leaving:
                    if (Arrived()) Destroy(gameObject);
                    break;
                case Phase.Moving when parking:
                    if (think) Think(); // 기다리러 가는 중에도 일이 생기면 바로 간다
                    if (State == Phase.Moving && parking && Arrived())
                    {
                        // 길이 중간에 끊겨(부분 경로) 대기 위치에 못 갔으면 그 자리에서 멈췄다가 다시 고른다
                        if (FlatDistance(transform.position, waitPoint) > rules.WaitSpacing) Hold();
                        else
                        {
                            parking = false;
                            State = Phase.Parked;
                        }
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
                // 배달할 곳이 없으면 그 자리에서 기다린다
                if (Find(drop: true, out var dropZone)) Go(dropZone, drop: true);
                else Hold();
                return;
            }

            if (Role == StaffRole.Returns && ReturnsWork()) return;
            if (Role == StaffRole.Unload && TryScan()) return;

            if (Find(drop: false, out var pickZone))
            {
                Go(pickZone, drop: false);
                return;
            }
            if (State == Phase.Parked || (State == Phase.Moving && parking)) return; // 이미 기다리는 중
            Park();
        }

        /// <summary>
        /// 갈 존 고르기 (2026-11-19). drop = 들고 있는 화물을 받는 존, 아니면 할 일이 있는 역할 존.
        /// 점수 = 가장 가까운 서는 위치 후보까지 거리 + 그 존에 가고 있거나 일하는 같은 직무 수 × 몰림 벌점. 동시 인원 상한은 없다.
        /// </summary>
        bool Find(bool drop, out CargoZone bestZone)
        {
            bestZone = null;
            var bestScore = float.MaxValue;
            var from = transform.position;

            foreach (var candidate in InteractionZone.Active)
            {
                if (candidate is not CargoZone cargoZone || cargoZone.Slots.Count == 0 || !IsSettled(cargoZone)) continue;
                var ok = drop ? cargoZone.CanReceiveAny(interactor) : IsRoleZone(cargoZone) && HasWork(cargoZone);
                if (!ok) continue;

                var score = NearestSlotDistance(cargoZone, from) + CountSameRoleAt(cargoZone) * rules.CrowdPenalty;
                if (score >= bestScore) continue;
                bestZone = cargoZone;
                bestScore = score;
            }
            return bestZone != null;
        }

        static float NearestSlotDistance(InteractionZone target, Vector3 from)
        {
            var best = float.MaxValue;
            foreach (var slot in target.Slots)
            {
                if (!slot.isActiveAndEnabled) continue;
                best = Mathf.Min(best, FlatDistance(slot.transform.position, from));
            }
            return best < float.MaxValue ? best : FlatDistance(target.transform.position, from);
        }

        /// <summary>그 존에 가고 있거나 일하는 같은 직무 알바 수 (나 빼고).</summary>
        int CountSameRoleAt(InteractionZone target)
        {
            var n = 0;
            foreach (var other in AllWorkers)
            {
                if (other != this && other.Role == Role && other.TargetZone == target) n++;
            }
            return n;
        }

        bool IsRoleZone(CargoZone candidate) => Role switch
        {
            StaffRole.Unload => candidate is TruckPickupZone,
            StaffRole.Sort => candidate is StorageZone storage && storage.Storage.Kind is StorageKind.Pallet or StorageKind.Repacked, // 반품 팔레트 · 재포장 입력 · 쓰레기는 반품 알바 (GDD 7.7)
            StaffRole.Pick => candidate is ShelfZone,
            StaffRole.Forklift => candidate is TruckPickupZone || (candidate is ShelfZone rack && Warehouse.IsLarge(rack.Shelf.Item)), // D 트럭 → 랙 → 상차 (M11-8)
            StaffRole.Returns => candidate is StorageZone returns && focus switch
            {
                ReturnsFocus.TrashFull => returns.Storage.Kind == StorageKind.Trash && !returns.Storage.HasSpace,
                ReturnsFocus.Pallet => returns.Storage.Kind == StorageKind.Returns,
                ReturnsFocus.Trash => returns.Storage.Kind == StorageKind.Trash,
                _ => returns.Storage.Kind is StorageKind.Returns or StorageKind.Trash,
            },
            _ => false,
        };

        /// <summary>
        /// 반품 알바 (GDD 11.3): 막히는 곳부터 — 쓰레기 출력이 가득 차 재포장이 막혔으면 쓰레기, 아니면 재포장 → 반품 팔레트에서 테이블 입력 채우기 → 쓰레기.
        /// 무언가를 시작했으면 true.
        /// </summary>
        bool ReturnsWork()
        {
            if (TryPickFocus(ReturnsFocus.TrashFull))
            {
                focus = ReturnsFocus.Trash; // 줍는 동안 가득이 풀려도 계속 줍는다
                return true;
            }
            if (TryGauge()) return true;
            if (TryPickFocus(ReturnsFocus.Pallet) || TryPickFocus(ReturnsFocus.Trash)) return true;
            focus = ReturnsFocus.Any; // 할 일 없음 → 대기
            return false;
        }

        bool TryPickFocus(ReturnsFocus next)
        {
            focus = next;
            if (!Find(drop: false, out var pickZone)) return false;
            Go(pickZone, drop: false);
            return true;
        }

        /// <summary>
        /// 하차 알바 스캔 (GDD 7.2a): 미분류가 `staffScanMinBoxes`개 이상(트럭이 비었거나 없으면 그보다 적어도)이고 아무도 스캔하지 않는
        /// 가장 가까운 하차 칸 공간으로 간다. 한 칸 한 사람 — 다른 하차 알바가 스캔하러 가는 칸은 고르지 않는다.
        /// </summary>
        bool TryScan()
        {
            StorageZone best = null;
            var bestDistance = float.MaxValue;
            var from = transform.position;
            foreach (var candidate in InteractionZone.Active)
            {
                if (candidate is not StorageZone storageZone || storageZone.Slots.Count == 0 || !IsSettled(storageZone)) continue;
                if (!storageZone.StaffScanWanted || (storageZone.Scanner != null && storageZone.Scanner != interactor) || ScanTakenByOther(storageZone)) continue;
                var distance = NearestSlotDistance(storageZone, from);
                if (distance >= bestDistance) continue;
                best = storageZone;
                bestDistance = distance;
            }
            if (best == null) return false;
            ReleaseAll();
            zone = best;
            SetStand(best, ChooseStand(best));
            dropping = false;
            scanning = true; // 화물 예약 없음
            agent.SetDestination(OccupiedPosition);
            State = Phase.Moving;
            return true;
        }

        bool ScanTakenByOther(StorageZone target)
        {
            foreach (var other in AllWorkers)
            {
                if (other != this && other.scanning && other.zone == target) return true;
            }
            return false;
        }

        /// <summary>재포장할 수 있고 아무도 맡지 않은 가장 가까운 테이블로 간다 (테이블만 1명 예약, 2026-11-19).</summary>
        bool TryGauge()
        {
            RepackTable best = null;
            var bestDistance = float.MaxValue;
            var from = transform.position;
            foreach (var candidate in InteractionZone.Active)
            {
                if (candidate is not RepackTable table || !table.HasWork || !table.IsFreeFor(this) || !IsSettled(table)) continue;
                var distance = NearestSlotDistance(table, from);
                if (distance >= bestDistance) continue;
                best = table;
                bestDistance = distance;
            }
            if (best == null) return false;

            ReleaseAll();
            gauge = best;
            gauge.TryClaim(this);
            SetStand(best, ChooseStand(best));
            dropping = false;
            SetPath(OccupiedPosition);
            State = Phase.Moving;
            return true;
        }

        // 하차 칸 공간 빈자리 · 선반 빈자리 · 수요는 존 규칙(HasCargoFor)에 이미 들어 있다.
        bool HasWork(CargoZone candidate) => candidate.HasCargoFor(interactor);

        void Go(CargoZone target, bool drop)
        {
            ReleaseAll();
            zone = target;
            SetStand(target, ChooseStand(target));
            dropping = drop;
            if (!drop && Role == StaffRole.Sort && target is StorageZone sortZone && sortZone.Storage.Kind == StorageKind.Pallet)
                pickBudget = PickBudget(sortZone.Storage); // 출발할 때 스캔된 상자만 (GDD 7.2a)
            if (!drop) Reserve();

            SetPath(OccupiedPosition);
            State = Phase.Moving;
        }

        /// <summary>보관 알바가 이번에 집을 최대 수: 지금 선반에 갈 스캔된 상자 + 찌그러진 상자 (적재 수까지).</summary>
        int PickBudget(SharedStorage storage)
        {
            var room = stack.Capacity.CurrentValue - stack.Count.CurrentValue;
            var total = 0;
            foreach (var pair in storage.PlanForShelves(room)) total += pair.Value;
            foreach (var cargo in storage.Items)
            {
                if (cargo.State == CargoState.Return && !cargo.Unsorted) total++;
            }
            return stack.Count.CurrentValue + Mathf.Min(room, total);
        }

        void SetStand(InteractionZone target, Vector3 world)
        {
            standLocal = target.transform.InverseTransformPoint(world);
            hasStand = true;
            hasWait = false;
        }

        /// <summary>
        /// 서는 위치 고르기 (2026-11-19). 후보(WorkSlot) 중 다른 알바가 `standSpacing` 안에 가장 적은 곳 (같으면 가까운 곳).
        /// 모두 차 있으면 후보 둘레를 `standSpacing`씩 넓혀 가며 존 안 · 네비메시 위의 빈 곳을 찾는다. 없으면 가장 덜 붐비는 후보 (겹쳐 섬).
        /// 어느 경우든 존 가장자리에서 `zoneEdgeMargin` 안쪽이다.
        /// </summary>
        Vector3 ChooseStand(InteractionZone target)
        {
            var from = transform.position;
            var spacing = rules.StandSpacing;
            var best = ClampInside(target, target.transform.position);
            var bestCrowd = int.MaxValue;
            var bestDistance = float.MaxValue;
            foreach (var slot in target.Slots)
            {
                if (!slot.isActiveAndEnabled) continue;
                var p = ClampInside(target, slot.transform.position);
                var crowd = CountNear(p, spacing);
                var distance = FlatDistance(p, from);
                if (crowd > bestCrowd || (crowd == bestCrowd && distance >= bestDistance)) continue;
                best = p;
                bestCrowd = crowd;
                bestDistance = distance;
            }
            if (bestCrowd <= 0) return best;

            // 다 차 있음 → 존 안에서 비켜 서기 (가까운 둘레부터)
            for (var ring = 1; ring <= 4; ring++)
            {
                var found = false;
                var ringBest = best;
                var ringDistance = float.MaxValue;
                foreach (var slot in target.Slots)
                {
                    if (!slot.isActiveAndEnabled) continue;
                    var center = slot.transform.position;
                    for (var i = 0; i < 8; i++)
                    {
                        var angle = i * Mathf.PI * 0.25f;
                        var p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (spacing * ring);
                        if (!TrySnapInside(target, p, out var snapped) || CountNear(snapped, spacing) > 0) continue;
                        var distance = FlatDistance(snapped, from);
                        if (distance >= ringDistance) continue;
                        ringBest = snapped;
                        ringDistance = distance;
                        found = true;
                    }
                }
                if (found) return ringBest;
            }
            return best;
        }

        /// <summary>존 사각형을 가장자리에서 zoneEdgeMargin만큼 줄인 안쪽으로 점을 넣는다.</summary>
        Vector3 ClampInside(InteractionZone target, Vector3 world)
        {
            var t = target.transform;
            var local = t.InverseTransformPoint(world);
            var hx = Mathf.Max(0f, target.Size.x * 0.5f - rules.ZoneEdgeMargin);
            var hz = Mathf.Max(0f, target.Size.y * 0.5f - rules.ZoneEdgeMargin);
            local.x = Mathf.Clamp(local.x, -hx, hx);
            local.z = Mathf.Clamp(local.z, -hz, hz);
            return t.TransformPoint(local);
        }

        /// <summary>점을 존 안쪽으로 넣고 네비메시에 붙인다. 붙인 점이 여전히 존 안쪽(가장자리 여유 절반 이상)이면 true.</summary>
        bool TrySnapInside(InteractionZone target, Vector3 world, out Vector3 snapped)
        {
            snapped = ClampInside(target, world);
            if (!NavMesh.SamplePosition(snapped, out var hit, NavSnapRadius, NavMesh.AllAreas)) return false;
            snapped = hit.position;
            var local = target.transform.InverseTransformPoint(snapped);
            var margin = rules.ZoneEdgeMargin * 0.5f;
            return Mathf.Abs(local.x) <= target.Size.x * 0.5f - margin && Mathf.Abs(local.z) <= target.Size.y * 0.5f - margin && IsClear(snapped);
        }

        /// <summary>
        /// 그 자리에 소품(쓰레기통 · 선반 등 충돌체)이 없는가. 네비메시는 소품 밑에도 깔려 있어 네비메시만으로는 알 수 없다.
        /// 바닥에 닿지 않게 발목 위부터 머리까지 알바 반경으로 본다 (트리거 무시).
        /// </summary>
        bool IsClear(Vector3 point)
        {
            var radius = agent.radius;
            return !Physics.CheckCapsule(point + Vector3.up * (radius + 0.1f), point + Vector3.up * 1.5f, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>다른 알바(가는 · 일하는 · 대기 위치 포함, 나 빼고) 중 점에서 radius 안에 있는 수.</summary>
        int CountNear(Vector3 point, float radius)
        {
            var n = 0;
            foreach (var other in AllWorkers)
            {
                if (other == this || other.leaving) continue;
                if (FlatDistance(other.OccupiedPosition, point) < radius) n++;
            }
            return n;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// 할 일이 없을 때 기다리러 간다 (2026-11-19). 마지막 일한 존 가장자리에서 바깥으로 `waitOffset` 비켜 선 곳,
        /// 다른 알바와 겹치면 변을 따라 `waitSpacing`씩 옆으로. 일한 적이 없으면 생성 지점 근처. 대기 중엔 아무 존도 막지 않는다.
        /// 마땅한 곳이 없으면 그 자리에서 멈춘다.
        /// </summary>
        void Park()
        {
            ReleaseAll();
            // 막 생성된 프레임 등 에이전트가 아직 네비메시에 없으면 길을 못 잡는다 → 그 자리에서 기다렸다가 다음 판단 때 다시
            if (!agent.isOnNavMesh || !TryChooseWaitPoint(out var point) || !SetPath(point))
            {
                Hold();
                return;
            }
            waitPoint = point;
            hasWait = true;
            parking = true;
            dropping = false;
            State = Phase.Moving;
        }

        bool TryChooseWaitPoint(out Vector3 point)
        {
            var candidates = new List<Vector3>();
            var from = transform.position;
            point = default;
            if (lastZone != null && lastZone.isActiveAndEnabled && !IsSettled(lastZone)) return false; // 팝 연출 중 → 잠깐 그 자리에서, 다음 판단 때 다시
            if (lastZone != null && lastZone.isActiveAndEnabled)
            {
                // 존 네 변 바깥 (내 위치에서 가장 가까운 변 위 점부터), 변을 따라 waitSpacing씩 옆으로
                var t = lastZone.transform;
                var half = lastZone.Size * 0.5f;
                var local = t.InverseTransformPoint(from);
                var offset = rules.WaitOffset;
                var spacing = rules.WaitSpacing;
                for (var side = 0; side < 4; side++)
                {
                    var alongX = side < 2; // 0 · 1 = 앞뒤 변(z = ±), 2 · 3 = 좌우 변(x = ±)
                    var sign = side % 2 == 0 ? 1f : -1f;
                    var along = alongX ? Mathf.Clamp(local.x, -half.x, half.x) : Mathf.Clamp(local.z, -half.y, half.y);
                    var extent = alongX ? half.x : half.y;
                    var steps = Mathf.CeilToInt((extent * 2f + offset) / spacing) + 1;
                    for (var k = -steps; k <= steps; k++)
                    {
                        var a = along + k * spacing;
                        var p = alongX
                            ? new Vector3(a, 0f, sign * (half.y + offset))
                            : new Vector3(sign * (half.x + offset), 0f, a);
                        candidates.Add(t.TransformPoint(p));
                    }
                }
            }
            else
            {
                var spawn = StaffSpawnPoint.Current;
                var center = spawn != null ? spawn.transform.position : from;
                candidates.Add(center);
                var spacing = rules.WaitSpacing;
                for (var ring = 1; ring <= 4; ring++)
                {
                    var count = ring * 8;
                    for (var i = 0; i < count; i++)
                    {
                        var angle = i * Mathf.PI * 2f / count;
                        candidates.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (spacing * ring));
                    }
                }
                from = center;
            }

            var origin = from;
            candidates.Sort((a, b) => FlatDistance(a, origin).CompareTo(FlatDistance(b, origin)));
            foreach (var candidate in candidates)
            {
                if (IsGoodWaitPoint(candidate, out point)) return true;
            }
            point = default;
            return false;
        }

        /// <summary>네비메시 위 · 어떤 존 사각형에서도 WaitZoneClearance 밖 · 다른 알바와 waitSpacing 이상 · 소품과 겹치지 않음.</summary>
        bool IsGoodWaitPoint(Vector3 candidate, out Vector3 point)
        {
            point = candidate;
            if (!NavMesh.SamplePosition(candidate, out var hit, NavSnapRadius, NavMesh.AllAreas)) return false;
            point = hit.position;
            foreach (var other in InteractionZone.Active)
            {
                var local = other.transform.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) <= other.Size.x * 0.5f + WaitZoneClearance && Mathf.Abs(local.z) <= other.Size.y * 0.5f + WaitZoneClearance) return false;
            }
            return CountNear(point, rules.WaitSpacing * 0.95f) == 0 && IsClear(point);
        }

        bool Arrived()
        {
            if (agent.pathPending) return false;
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                var target = TargetZone;
                NoPathCount++;
                Debug.LogWarning($"[StaffWorker] {name}: '{(leaving ? "출구" : target != null ? target.name : "대기 위치")}'까지 길이 없습니다.", this);
                Hold();
                return false;
            }
            return agent.remainingDistance <= ArriveDistance;
        }

        /// <summary>가는 중인 목표가 아직 유효한가. 예약은 잠깐 풀고 다시 확인해 다시 잡는다 (자기 예약 때문에 일이 없어 보이지 않게).</summary>
        bool StillValid()
        {
            if (gauge != null) return gauge.isActiveAndEnabled && gauge.HasWork;
            if (zone == null || !zone.isActiveAndEnabled) return false;
            if (dropping) return zone.CanReceiveAny(interactor);
            if (scanning) return zone is StorageZone scanZone && scanZone.StaffScanWanted && (scanZone.Scanner == null || scanZone.Scanner == interactor);

            ReleaseReservation();
            var ok = IsRoleZone(zone) && HasWork(zone);
            if (ok) Reserve();
            return ok;
        }

        void BeginWork()
        {
            ReleaseReservation(); // 도착하면 존이 실제 수요로 판단한다 (들고 있는 수가 예약을 대신함)
            interactor.AllowDrop = dropping && !scanning;
            interactor.AllowPick = !dropping && !scanning;
            interactor.AllowScan = scanning;
            interactor.Target = TargetZone;
            lastZone = TargetZone;
            workTime = 0f;
            idleTime = 0f;
            lastCount = stack.Count.CurrentValue;
            State = Phase.Working;
        }

        void TickWork(float deltaTime)
        {
            workTime += deltaTime;
            if (gauge != null) // 재포장: 할 수 있는 동안 서 있는다 (게이지는 테이블이 채운다)
            {
                var stuckAtTable = workTime >= rules.InteractionDelay + StuckTimeout && interactor.CurrentZone != gauge && !repathPending;
                if (!gauge.HasWork || stuckAtTable) FinishWork();
                return;
            }
            if (scanning) // 스캔: 게이지는 StorageZone이 채운다. 끝났거나 다른 사람이 스캔하면 다음 일
            {
                var scanZone = zone as StorageZone;
                var stuckAtScan = workTime >= rules.InteractionDelay + StuckTimeout && interactor.CurrentZone != zone && !repathPending;
                var mine = scanZone != null && scanZone.Scanner == interactor;
                if (scanZone == null || stuckAtScan || (!mine && (!scanZone.StaffScanWanted || scanZone.Scanner != null))) FinishWork();
                return;
            }
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

            var done = dropping ? count == 0 : stack.IsFull || (pickBudget >= 0 && count >= pickBudget);
            var stuck = workTime >= rules.InteractionDelay + StuckTimeout && interactor.CurrentZone != zone && !repathPending; // 재배치 팝 연출 중엔 존이 잠깐 안 잡힌다
            if (stuck) Debug.LogWarning($"[StaffWorker] {name}: 작업 지점 {OccupiedPosition}이 존 '{zone.name}' 밖에 있습니다.", zone);
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
            SetPath(spawn != null ? spawn.transform.position : transform.position);
            State = Phase.Moving;
        }

        /// <summary>그 자리에서 멈춰 기다린다 (배달할 곳 없음 / 대기 위치 없음). 아무 존도 막지 않는다.</summary>
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

        /// <summary>화물 예약 · 테이블 예약 · 서는 / 대기 위치 · 대상 존을 모두 푼다.</summary>
        void ReleaseAll()
        {
            ReleaseReservation();
            if (gauge != null) gauge.Release(this);
            zone = null;
            gauge = null;
            hasStand = false;
            hasWait = false;
            parking = false;
            scanning = false;
            pickBudget = -1;
            if (interactor != null)
            {
                interactor.Target = null;
                interactor.AllowScan = false;
            }
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
                    var space = Role == StaffRole.Forklift && truck.Company != null ? Warehouse.Instance.LargeRoom(truck.Company.Items[0]) : pickup.Room; // 지게차 = 랙 빈자리
                    var count = Mathf.Min(room, Mathf.Min(truck.Available, space)); // 그 칸 공간 빈자리까지 (GDD 8.4)
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
