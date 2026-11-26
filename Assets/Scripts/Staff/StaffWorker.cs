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

namespace APC.Staff
{
    /// <summary>
    /// 알바 1명 (GDD 11). 부지 A* 격자(StaffNav)로 움직이고 (2026-11-26 NavMesh 삭제), 플레이어와 같은 Interactor + CarryStack으로 존과 상호작용한다.
    /// 작업 상태 머신 (GDD 11.4): 대기 → 대상 탐색 · 예약 → 집으러 이동 → 집기 → 운반 → 내려놓기 → 대기.
    /// - 집는 곳은 역할로 정한다: 하차 = 트럭 픽업 존, 분류 = 팔레트 둘레 존, 피킹 = 선반 둘레 존.
    /// - 내려놓는 곳은 **들고 있는 화물**로 정한다 (그 화물을 받는 존). 그래서 역할을 바꿔도 들고 있던 화물은 원래 목적지로 간다.
    /// - 2026-11-19 알바 상호작용 개편: 작업 자리(WorkSlot)는 **예약하지 않는다**. 존 사각형 안에 들어가 멈추면 일한다 (플레이어와 같음).
    ///   서는 위치 = 후보(WorkSlot) 중 사람 적은 곳 → 다 차면 존 안에서 비켜 서기. 존 고르기 = 거리 + 같은 직무 몰림 벌점.
    ///   재포장 테이블만 1명 (RepackTable.TryClaim). 화물 예약(트럭 · 선반 빈자리 · 주문 수요)은 그대로.
    /// - 할 일이 없으면 마지막 일한 존 밖으로 비켜 선 곳(일한 적 없으면 생성 지점 근처)에서 기다린다 (Parked, 아무 존도 막지 않음).
    /// - 작업 고정 · 전환 쿨타임 · 가이드 화살표는 플레이어 전용이라 쓰지 않는다 (GDD 5.3).
    /// </summary>
    [RequireComponent(typeof(Interactor), typeof(CarryStack))]
    public sealed class StaffWorker : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly List<StaffWorker> AllWorkers = new();
        // 대기 위치 후보 · 정렬 기준 재사용 (Park마다 List · 클로저 할당 → 알바 100명 GC, 2026-11-25). 메인 스레드 · 재진입 없음
        static readonly List<Vector3> WaitCandidates = new();
        static Vector3 sortOrigin;
        static readonly System.Comparison<Vector3> ByOriginDistance = (a, b) => FlatDistance(a, sortOrigin).CompareTo(FlatDistance(b, sortOrigin));
        const float ParkRetryDelay = 1f;

        const float ArriveDistance = 0.2f;
        // 도착했는데 이만큼(초) 동안 상호작용이 시작되지 않으면 포기한다 (서는 위치가 존 밖에 있는 등 배치 오류 대비)
        const float StuckTimeout = 3f;
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

        float speed;        // 이동 속도 (m/s, 스탯)
        int crowdId = -1;   // A* 이동 번호 (StaffNav). −1 = 아직 격자 없음
        bool pathFailed;    // A*: 마지막 길찾기 실패
        Vector3 destination; // 마지막으로 가라고 한 곳 (두 방식 공용)
        Interactor interactor;
        CarryStack stack;
        StaffRules rules;
        MaterialPropertyBlock block;

        CargoZone zone;
        RepackTable gauge; // 반품 알바가 서서 재포장하는 테이블 (zone 대신, 1명 예약)
        InteractionZone scanStation; // 하차 알바가 서서 스캔하는 자리 (IScanStation — 스캔 팔레트 · 벨트 스캔 구역, zone 대신)
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
        // 지금 잡고 있는 예약 (2026-11-26: 람다 대신 필드 — StillValid가 판단마다 풀고 다시 잡아 클로저 · Dictionary가 쌓였다)
        enum Reservation { None, Truck, ShelfSpace, Demand }
        Reservation reserved;
        InboundTruck reservedTruck;
        SharedStorage reservedStorage; // 분류: 상자를 예약한 더미 (reservedPlan = 선반 빈자리 = 그 더미 상자, 품목별)
        ItemDefinition reservedItem;
        int reservedCount;
        readonly Dictionary<ItemDefinition, int> reservedPlan = new(); // 선반 빈자리 예약 (품목별)
        static readonly Dictionary<ItemDefinition, int> ScratchPlan = new(); // PickBudget 계산용 (메인 스레드 · 재진입 없음)
        float thinkTimer;
        float parkRetryAt; // 대기 위치 찾기 실패 뒤 다시 찾는 시각 (Park)
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
        public InteractionZone TargetZone => gauge != null ? gauge : scanStation != null ? scanStation : zone;

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
            thinkFrame = budgetFrame = -1;
            budgetUsed = 0;
            thinksThisFrame = maxThinksPerFrame = 0;
            ThinkScale = 1f;
            zoneListVersion = -1;
        }

        // 성능 측정용 (PerformanceHUD가 읽는다): 길 요청 수 · 길 없음 수 — A* 전환 비교 기준 (2026-11-23)
        public static int PathRequests { get; private set; }
        public static int NoPathCount { get; private set; }

        // 성능 측정용 (PerformanceHUD, 2026-11-26): 한 프레임에 Think가 몇 번 몰렸나 — HUD가 읽고 0으로 되돌린다
        static int thinkFrame = -1;
        static int budgetFrame = -1;
        static int budgetUsed;
        static int thinksThisFrame;
        static int maxThinksPerFrame;

        /// <summary>HUD 측정 구간 동안 한 프레임 Think 최대 수를 돌려주고 0으로 되돌린다.</summary>
        public static int TakeMaxThinksPerFrame()
        {
            var max = maxThinksPerFrame;
            maxThinksPerFrame = 0;
            return max;
        }

        /// <summary>판단 간격 배수 (DEBUG `Think x4`, 2026-11-26 성능 A/B). 1 = 설정 그대로.</summary>
        public static float ThinkScale { get; set; } = 1f;

        float ThinkInterval => rules.ThinkInterval * ThinkScale;

        /// <summary>HUD 측정용: 할 일이 없어 대기 위치로 가는 중.</summary>
        internal bool Parking => parking;

        internal static IReadOnlyList<StaffWorker> All => AllWorkers;
        internal int CrowdId => crowdId;
        bool OnNav => crowdId >= 0;

        bool SetPath(Vector3 point)
        {
            PathRequests++;
            destination = point;
            if (!OnNav && StaffNav.Ensure() is { } nav) crowdId = nav.Register(transform, speed); // 켜질 때 격자가 아직 없었으면 (열린 칸 없음) 지금 등록
            if (!OnNav) return pathFailed = false; // 격자가 아직 없음 — 다음 판단 때 다시
            pathFailed = !StaffNav.Shared.SetPath(crowdId, point, speed);
            return !pathFailed;
        }

        void StopPath()
        {
            if (OnNav) StaffNav.Shared.Stop(crowdId);
        }

        void PauseMove(bool paused)
        {
            if (OnNav) StaffNav.Shared.Pause(crowdId, paused);
        }

        /// <summary>순간 이동 (벨트 설치에 밀려남, BeltPlacer). 가던 중이면 새 자리에서 같은 목적지로 다시 길을 찾는다.</summary>
        internal void Warp(Vector3 position)
        {
            if (!OnNav)
            {
                transform.position = position;
                return;
            }
            StaffNav.Shared.Warp(crowdId, position);
            if (State == Phase.Moving) SetPath(destination);
        }

        /// <summary>A* 격자를 다시 구운 뒤 (StaffNav): 같은 목적지로 길을 다시 찾는다.</summary>
        internal void RefreshPath()
        {
            if (State == Phase.Moving) SetPath(destination);
        }

        void Awake()
        {
            interactor = GetComponent<Interactor>();
            stack = GetComponent<CarryStack>();
            interactor.RestrictToTarget = true; // 목표 존에서만 상호작용
            interactor.OwnReservation = OwnReserved;
            interactor.AllowScan = false; // 하차 알바가 스캔하러 갔을 때만 켠다 (GDD 7.2a)
            if (unpaidIcon != null)
            {
                iconBaseY = unpaidIcon.transform.localPosition.y;
                unpaidIcon.SetActive(false);
            }
        }

        void OnEnable()
        {
            AllWorkers.Add(this);
            var nav = StaffNav.Ensure(); // 첫 알바가 부지 A* 격자를 굽는다
            if (nav != null) crowdId = nav.Register(transform, speed);
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
            PauseMove(true);
            var roleColor = rules.GetRoleColor(Role);
            var bodyTransform = models != null && models.Length > 0 ? models[0].transform.parent : body.transform; // 모델이면 모델째 뛴다
            DOTween.Sequence()
                .Append(DOVirtual.Float(0f, 1f, 0.9f, t => SetColor(Color.Lerp(roleColor, angryColor, Mathf.PingPong(t * 6f, 1f)))))
                .Join(bodyTransform.DOLocalJump(bodyTransform.localPosition, 0.5f, 2, 0.9f))
                .OnComplete(() =>
                {
                    SetColor(roleColor);
                    PauseMove(false);
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
                Visuals.SkinnedCombine.Combine(models[pick]); // 스킨 메시 6개 → 재질별 하나 (드로 절반, 2026-11-27)
            }
            rules = GameManager.Instance.Database.Staff;
            Stats = stats;
            speed = rules.GetMoveSpeed(stats.moveSpeed);
            interactor.Configure(rules.PickInterval, rules.InteractionDelay);
            thinkTimer = Random.value * ThinkInterval; // 같은 프레임에 고용한 알바가 매번 같은 프레임에 판단하지 않게 (100명 = 90ms 끊김, 2026-11-25)
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
            Visuals.BlobShadow.Attach(gameObject, isForklift ? 2.4f : 1.1f); // 메인 라이트 그림자 대신 (2026-11-26)
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
            if (leaving || !OnNav) return;
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
                case Phase.Moving when (destination - stand).sqrMagnitude > 1f:
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
            if (OnNav && StaffNav.Shared != null) StaffNav.Shared.Release(crowdId);
            crowdId = -1;
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
            // 위상 유지: 긴 프레임(0.25초+) 한 번에 전원이 0 아래로 → '= 간격'이면 모두 같은 프레임에 판단하게 굳는다 (100명 = 80ms 끊김, 2026-11-25)
            if (think) thinkTimer = ThinkInterval - Mathf.Repeat(-thinkTimer, ThinkInterval);
            if (think && !TakeThinkBudget())
            {
                // shortcut: Update 순서가 앞인 알바가 먼저 예산을 쓴다 — 미룬 알바가 계속 밀리면 차례 큐로 바꾼다
                thinkTimer = 0f; // 다음 프레임에 다시
                think = false;
            }
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
                        // 길이 중간에 끊겨(부분 경로) 대기 위치에 못 갔어도 존을 막지 않는 곳이면 거기서 기다린다 —
                        // 예전엔 멈췄다가 다시 골라 또 걸어가서, 할 일 없는 알바가 돌아다니는 것처럼 보였다 (2026-11-26)
                        var fellShort = FlatDistance(transform.position, waitPoint) > rules.WaitSpacing;
                        if (fellShort && NearAnyZone(transform.position))
                        {
                            Hold();
                            parkRetryAt = Time.time + ParkRetryDelay; // 바로 다시 고르지 않는다
                        }
                        else
                        {
                            if (fellShort) waitPoint = transform.position;
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
            if (thinkFrame != Time.frameCount)
            {
                thinkFrame = Time.frameCount;
                thinksThisFrame = 0;
            }
            maxThinksPerFrame = Mathf.Max(maxThinksPerFrame, ++thinksThisFrame);

            if (Fired && stack.Count.CurrentValue == 0)
            {
                Leave();
                return;
            }
            if (stack.Count.CurrentValue > 0)
            {
                // 배달할 곳이 없으면 그 자리에서 기다린다
                if (Find(drop: true, out var dropZone)) Go(dropZone, drop: true);
                else
                {
                    Hold();
                    IdleBackoff();
                }
                return;
            }

            if (Role == StaffRole.Returns && ReturnsWork()) return;
            if (Role == StaffRole.Unload && TryScan()) return;

            if (Find(drop: false, out var pickZone))
            {
                Go(pickZone, drop: false);
                return;
            }
            if (State == Phase.Parked || (State == Phase.Moving && parking))
            {
                IdleBackoff(); // 이미 기다리는 중 — 일이 없으니 판단을 늦춘다
                return;
            }
            Park();
        }

        /// <summary>할 일이 없을 때 다음 판단을 `idleThinkMultiplier`배 뒤로 (2026-11-26). 대기 알바가 0.25초마다 존 전체를 훑던 것.</summary>
        void IdleBackoff() => thinkTimer = ThinkInterval * rules.IdleThinkMultiplier * (0.75f + 0.5f * Random.value); // 흩어서 위상이 묶이지 않게

        /// <summary>이번 프레임 판단 예산 (`maxThinksPerFrame`). 쓰면 true.</summary>
        bool TakeThinkBudget()
        {
            var frame = Time.frameCount;
            if (budgetFrame != frame)
            {
                budgetFrame = frame;
                budgetUsed = 0;
            }
            return ++budgetUsed <= rules.MaxThinksPerFrame;
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

            RefreshZoneLists();
            var candidates = drop ? DropZones : RoleZones[(int)Role];
            for (var i = 0; i < candidates.Count; i++)
            {
                var cargoZone = candidates[i];
                if (!IsSettled(cargoZone)) continue;
                var ok = drop ? cargoZone.CanReceiveAny(interactor) : IsRoleZone(cargoZone) && HasWork(cargoZone);
                if (!ok) continue;

                var score = NearestSlotDistance(cargoZone, from) + CountSameRoleAt(cargoZone) * rules.CrowdPenalty;
                if (score >= bestScore) continue;
                bestZone = cargoZone;
                bestScore = score;
            }
            return bestZone != null;
        }

        // 직무별로 볼 존을 미리 나눠 둔다 (2026-11-27). 판단마다 존 전체(TycoonTest 약 190개)를 돌며 직무와 상관없는 존까지 확인하던 것.
        // 종류(타입 · 보관 종류)만으로 나누고, 지금 상태에 따른 조건(반품 focus · 랙 품목)은 IsRoleZone이 그대로 본다
        static readonly List<CargoZone> DropZones = new();
        static readonly List<CargoZone>[] RoleZones = { new(), new(), new(), new(), new() }; // StaffRole 순서
        static int zoneListVersion = -1;

        static void RefreshZoneLists()
        {
            if (zoneListVersion == InteractionZone.ListVersion) return;
            zoneListVersion = InteractionZone.ListVersion;
            DropZones.Clear();
            foreach (var list in RoleZones) list.Clear();
            var all = InteractionZone.Active;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] is not CargoZone zone || zone.Slots.Length == 0) continue;
                DropZones.Add(zone);
                for (var role = 0; role < RoleZones.Length; role++)
                    if (CouldBeRoleZone((StaffRole)role, zone)) RoleZones[role].Add(zone);
            }
        }

        /// <summary>IsRoleZone의 종류 부분 (상태와 무관). IsRoleZone을 바꾸면 같이 바꾼다.</summary>
        static bool CouldBeRoleZone(StaffRole role, CargoZone zone) => role switch
        {
            StaffRole.Unload => zone is TruckPickupZone || (zone is StorageZone unload && unload.Storage.Kind == StorageKind.Pallet),
            StaffRole.Sort => zone is StorageZone storage && storage.Storage.Kind is StorageKind.Scan or StorageKind.Repacked,
            StaffRole.Pick => zone is ShelfZone,
            StaffRole.Forklift => zone is TruckPickupZone || zone is ShelfZone,
            StaffRole.Returns => zone is StorageZone returns && returns.Storage.Kind is StorageKind.Returns or StorageKind.Trash,
            _ => false,
        };

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
            // 하차 알바 = 트럭 → 하차 칸 공간 + 공간의 미분류 → 스캔 팔레트 (+ 스캔), 보관 알바 = 스캔 팔레트 → 선반 (2026-11-24)
            StaffRole.Unload => candidate is TruckPickupZone || (candidate is StorageZone unload && unload.Storage.Kind == StorageKind.Pallet),
            StaffRole.Sort => candidate is StorageZone storage && storage.Storage.Kind is StorageKind.Scan or StorageKind.Repacked, // 반품 팔레트 · 재포장 입력 · 쓰레기는 반품 알바 (GDD 7.7)
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
        /// 하차 알바 스캔 (GDD 7.2a): 미분류가 1개 이상(M20-1, 최소 개수 규칙 삭제)이고 아무도 스캔하지 않는
        /// 가장 가까운 하차 칸 공간으로 간다. 한 칸 한 사람 — 다른 하차 알바가 스캔하러 가는 칸은 고르지 않는다.
        /// </summary>
        bool TryScan()
        {
            // 벨트 스캔 구역 먼저 (상자가 오는 중 · 기다릴 때만, M20-5), 없으면 스캔 팔레트
            var best = NearestScanStation(beltOnly: true) ?? NearestScanStation(beltOnly: false);
            if (best == null) return false;
            ReleaseAll();
            scanStation = best;
            SetStand(best, ChooseStand(best));
            dropping = false;
            scanning = true; // 화물 예약 없음
            SetPath(OccupiedPosition);
            State = Phase.Moving;
            return true;
        }

        InteractionZone NearestScanStation(bool beltOnly)
        {
            InteractionZone best = null;
            var bestDistance = float.MaxValue;
            var from = transform.position;
            foreach (var candidate in InteractionZone.Active)
            {
                if (candidate is not IScanStation station || (beltOnly && candidate is not BeltScanZone) || candidate.Slots.Length == 0 || !IsSettled(candidate)) continue;
                if (!station.StaffScanWanted || (station.Scanner != null && station.Scanner != interactor) || ScanTakenByOther(candidate)) continue;
                var distance = NearestSlotDistance(candidate, from);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        bool ScanTakenByOther(InteractionZone target)
        {
            foreach (var other in AllWorkers)
            {
                if (other != this && other.scanning && other.scanStation == target) return true;
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
            if (!drop && Role == StaffRole.Sort && target is StorageZone sortZone && sortZone.Storage.Kind == StorageKind.Scan)
                pickBudget = PickBudget(sortZone.Storage); // 출발할 때 스캔된 상자만 (GDD 7.2a)
            if (!drop) Reserve();

            SetPath(OccupiedPosition);
            State = Phase.Moving;
        }

        /// <summary>보관 알바가 이번에 집을 최대 수: 지금 선반에 갈 스캔된 상자 + 찌그러진 상자 (적재 수까지).</summary>
        int PickBudget(SharedStorage storage)
        {
            var room = stack.Capacity.CurrentValue - stack.Count.CurrentValue;
            var total = storage.PlanForShelves(room, ScratchPlan);
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

        /// <summary>점을 존 안쪽으로 넣고 바닥에 붙인다. 붙인 점이 여전히 존 안쪽(가장자리 여유 절반 이상)이면 true. 격자 칸이라 소품 검사도 포함.</summary>
        bool TrySnapInside(InteractionZone target, Vector3 world, out Vector3 snapped)
        {
            if (!TrySnapWalkable(ClampInside(target, world), out snapped)) return false;
            var local = target.transform.InverseTransformPoint(snapped);
            var margin = rules.ZoneEdgeMargin * 0.5f;
            return Mathf.Abs(local.x) <= target.Size.x * 0.5f - margin && Mathf.Abs(local.z) <= target.Size.y * 0.5f - margin;
        }

        /// <summary>
        /// 그 점이 걸을 수 있는 바닥인가 (2026-11-26): A* 격자 칸 하나만 본다 — 장애물을 알바 반경만큼 키워 구워 두었으니
        /// 소품 검사까지 포함, 물리 · 네비 쿼리 없음. 닫힌 방 칸은 걸을 수 없다.
        /// </summary>
        bool TrySnapWalkable(Vector3 point, out Vector3 snapped)
        {
            snapped = point;
            if (!OnNav) return false;
            var grid = StaffNav.Shared.Site.World.Grid;
            var cell = grid.WorldToCell(point);
            return grid.IsWalkable(cell.x, cell.y);
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
            // 임시 1초: 대기 위치 찾기 실패 뒤엔 잠깐 다시 찾지 않는다 — 붐비면 후보 81곳을 0.25초마다 다 봐서 알바 100명 = 프레임당 30ms (2026-11-25). 일 찾기(Find)는 그대로
            if (Time.time < parkRetryAt)
            {
                Hold();
                return;
            }
            ReleaseAll();
            // 막 생성된 프레임 등 에이전트가 아직 네비메시에 없으면 길을 못 잡는다 → 그 자리에서 기다렸다가 다음 판단 때 다시
            if (!OnNav || !TryChooseWaitPoint(out var point) || !SetPath(point))
            {
                Hold();
                parkRetryAt = Time.time + ParkRetryDelay;
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
            var candidates = WaitCandidates;
            candidates.Clear();
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

            sortOrigin = from;
            candidates.Sort(ByOriginDistance);
            foreach (var candidate in candidates)
            {
                if (IsGoodWaitPoint(candidate, out point)) return true;
            }
            point = default;
            return false;
        }

        /// <summary>걸을 수 있는 바닥 · 어떤 존 사각형에서도 WaitZoneClearance 밖 · 다른 알바와 waitSpacing 이상 · 소품과 겹치지 않음.</summary>
        bool IsGoodWaitPoint(Vector3 candidate, out Vector3 point)
        {
            if (!TrySnapWalkable(candidate, out point)) return false;
            if (CountNear(point, rules.WaitSpacing * 0.95f) > 0) return false; // 붐벼서 실패가 가장 흔하다 → 존 193개보다 먼저 (결과는 같음)
            return !NearAnyZone(point);
        }

        /// <summary>어떤 존 사각형에서 WaitZoneClearance 안에 있는가 (대기 자리가 존 바닥을 막지 않게).</summary>
        static bool NearAnyZone(Vector3 point)
        {
            foreach (var other in InteractionZone.Active)
            {
                var local = other.transform.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) <= other.Size.x * 0.5f + WaitZoneClearance && Mathf.Abs(local.z) <= other.Size.y * 0.5f + WaitZoneClearance) return true;
            }
            return false;
        }

        bool Arrived()
        {
            if (!OnNav) return false;
            if (pathFailed)
            {
                var target = TargetZone;
                NoPathCount++;
                EditorWarning($"[StaffWorker] {name}: '{(leaving ? "출구" : target != null ? target.name : "대기 위치")}'까지 길이 없습니다.", this);
                Hold();
                return false;
            }
            return StaffNav.Shared.Remaining(crowdId) <= ArriveDistance;
        }

        /// <summary>가는 중인 목표가 아직 유효한가. 존은 자기 예약을 더해서 보므로(OwnReserved) 풀지 않고 그대로 확인한다.</summary>
        bool StillValid()
        {
            if (gauge != null) return gauge.isActiveAndEnabled && gauge.HasWork;
            if (scanning) return scanStation != null && scanStation.isActiveAndEnabled && scanStation is IScanStation scanZone && scanZone.StaffScanWanted && (scanZone.Scanner == null || scanZone.Scanner == interactor);
            if (zone == null || !zone.isActiveAndEnabled) return false;
            if (dropping) return zone.CanReceiveAny(interactor);

            return IsRoleZone(zone) && HasWork(zone);
        }

        void BeginWork()
        {
            // 예약은 도착 뒤에도 유지한다 — 풀면 집기 전(InteractionDelay) 다른 알바가 가져가 둘 다 헛걸음했다 (2026-11-26). 집은 만큼 TickWork에서 줄인다
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
            if (scanning) // 스캔: 게이지는 스캔 자리(팔레트 · 벨트 스캔 구역)가 채운다. 끝났거나(벨트가 비면 떠남) 다른 사람이 스캔하면 다음 일
            {
                var scanZone = scanStation as IScanStation;
                var stuckAtScan = workTime >= rules.InteractionDelay + StuckTimeout && interactor.CurrentZone != scanStation && !repathPending;
                var mine = scanZone != null && scanZone.Scanner == interactor;
                if (scanZone == null || stuckAtScan || !scanZone.StaffScanWanted || (!mine && scanZone.Scanner != null)) FinishWork();
                return;
            }
            var count = stack.Count.CurrentValue;
            if (count != lastCount)
            {
                if (!dropping) ConsumeReservation(lastCount, count);
                lastCount = count;
                idleTime = 0f;
            }
            else if (interactor.IsReady)
            {
                idleTime += deltaTime;
            }

            var done = dropping ? count == 0 : stack.IsFull || (pickBudget >= 0 && count >= pickBudget);
            var stuck = workTime >= rules.InteractionDelay + StuckTimeout && interactor.CurrentZone != zone && !repathPending; // 재배치 팝 연출 중엔 존이 잠깐 안 잡힌다
            if (stuck) EditorWarning($"[StaffWorker] {name}: 작업 지점 {OccupiedPosition}이 존 '{zone.name}' 밖에 있습니다.", zone);
            if (done || idleTime >= rules.WorkIdleTimeout || stuck) FinishWork();
        }

        void FinishWork()
        {
            ReleaseAll();
            State = Phase.Idle;
            ScatterNextThink(); // 다음 일을 곧 고른다
        }

        /// <summary>
        /// 다음 판단을 곧(0 ~ 간격의 절반 뒤) 하게 한다 (2026-11-26). 0으로 두면 같은 프레임에 일을 끝낸 알바들(트럭이 빔 등)이
        /// 다음 프레임에 함께 판단하고 그 뒤로도 위상이 묶여 한 프레임에 판단이 몰렸다.
        /// </summary>
        void ScatterNextThink() => thinkTimer = Random.value * ThinkInterval * 0.5f;

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
            StopPath();
            State = Phase.Waiting;
        }

        void Abort()
        {
            ReleaseAll();
            StopPath();
            State = Phase.Idle;
            ScatterNextThink();
        }

        /// <summary>화물 예약 · 테이블 예약 · 서는 / 대기 위치 · 대상 존을 모두 푼다.</summary>
        void ReleaseAll()
        {
            ReleaseReservation();
            if (gauge != null) gauge.Release(this);
            zone = null;
            gauge = null;
            scanStation = null;
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
                    reserved = Reservation.Truck;
                    reservedTruck = truck;
                    reservedCount = count;
                    break;
                }
                case StorageZone storageZone when Warehouse.Instance != null:
                {
                    var warehouse = Warehouse.Instance;
                    storageZone.Storage.PlanForShelves(room, reservedPlan);
                    reservedStorage = storageZone.Storage;
                    foreach (var pair in reservedPlan)
                    {
                        warehouse.ReserveShelfSpace(pair.Key, pair.Value);
                        reservedStorage.ReserveBoxes(pair.Key, pair.Value); // 같은 상자에 여러 명이 몰려가지 않게 (2026-11-26)
                    }
                    reserved = Reservation.ShelfSpace;
                    break;
                }
                case ShelfZone shelfZone when OrderService.Instance != null && shelfZone.Shelf.Item != null:
                {
                    var orders = OrderService.Instance;
                    var item = shelfZone.Shelf.Item;
                    var count = Mathf.Min(room, orders.GetDemand(item));
                    if (count <= 0) return;
                    orders.Reserve(item, count);
                    reserved = Reservation.Demand;
                    reservedItem = item;
                    reservedCount = count;
                    break;
                }
            }
        }

        /// <summary>존이 내줄 때 더해 볼 자기 예약 (Interactor.OwnReservation). 지금 대상 존이 아니면 0.</summary>
        int OwnReserved(InteractionZone at, ItemDefinition item)
        {
            if (at == null || at != zone) return 0;
            return reserved switch
            {
                Reservation.Truck => reservedCount,
                Reservation.Demand => item == reservedItem ? reservedCount : 0,
                Reservation.ShelfSpace => item != null && reservedPlan.TryGetValue(item, out var n) ? n : 0,
                _ => 0,
            };
        }

        /// <summary>새로 집은 상자(stack [from, to))만큼 예약을 줄인다 — 이제 들고 있는 수가 그 몫을 잡는다.</summary>
        void ConsumeReservation(int from, int to)
        {
            if (reserved == Reservation.None || to <= from) return;
            var items = stack.Items;
            for (var i = from; i < to && i < items.Count; i++)
            {
                switch (reserved)
                {
                    case Reservation.Truck when reservedCount > 0:
                        if (reservedTruck != null) reservedTruck.Unreserve(1);
                        reservedCount--;
                        break;
                    case Reservation.Demand when reservedCount > 0 && items[i].Item == reservedItem:
                        if (OrderService.Instance != null) OrderService.Instance.Unreserve(reservedItem, 1);
                        reservedCount--;
                        break;
                    case Reservation.ShelfSpace when items[i].State == CargoState.Normal && items[i].Item != null && reservedPlan.TryGetValue(items[i].Item, out var n) && n > 0:
                        if (Warehouse.Instance != null) Warehouse.Instance.UnreserveShelfSpace(items[i].Item, 1);
                        if (reservedStorage != null) reservedStorage.UnreserveBoxes(items[i].Item, 1);
                        reservedPlan[items[i].Item] = n - 1;
                        break;
                }
            }
        }

        void ReleaseReservation()
        {
            var kind = reserved;
            reserved = Reservation.None; // 먼저 비운다 (푸는 중 다시 불려도 두 번 풀지 않게)
            switch (kind)
            {
                case Reservation.Truck:
                    if (reservedTruck != null) reservedTruck.Unreserve(reservedCount);
                    break;
                case Reservation.ShelfSpace:
                    var warehouse = Warehouse.Instance;
                    if (warehouse != null)
                    {
                        foreach (var pair in reservedPlan) warehouse.UnreserveShelfSpace(pair.Key, pair.Value);
                    }
                    if (reservedStorage != null)
                    {
                        foreach (var pair in reservedPlan) reservedStorage.UnreserveBoxes(pair.Key, pair.Value);
                    }
                    reservedPlan.Clear();
                    break;
                case Reservation.Demand:
                    if (OrderService.Instance != null) OrderService.Instance.Unreserve(reservedItem, reservedCount);
                    break;
            }
            reservedTruck = null;
            reservedStorage = null;
            reservedItem = null;
        }

        /// <summary>
        /// 에디터에서만 경고 (2026-11-26). 빌드에서는 호출이 통째로 빠져 문자열도 만들지 않는다 —
        /// 기기에서 같은 상황이 반복되면 판단마다 문자열 할당 + 스택트레이스 수집(Stack Trace = ScriptOnly) + logcat 쓰기가 들었다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void EditorWarning(string message, Object context) => Debug.LogWarning(message, context);

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
