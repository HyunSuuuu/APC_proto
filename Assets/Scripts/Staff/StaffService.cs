using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Economy;
using R3;
using UnityEngine;
using UnityEngine.AI;

namespace APC.Staff
{
    /// <summary>고용 탭 후보 1명 (GDD 11.1). 고용하면 Hired가 켜지고 다음 교체(파트 · 새로고침)까지 그 자리에 남는다.</summary>
    public sealed class StaffCandidate
    {
        public StaffCandidate(StaffStats stats) => Stats = stats;

        public StaffStats Stats { get; }
        public bool Hired { get; internal set; }
    }

    /// <summary>
    /// 파트 정산의 시급 계획 (GDD 11.7). 정산 팝업이 열릴 때 만들고([확인] 전엔 바뀌지 않음), [확인] 때 ApplyWages로 적용한다.
    /// </summary>
    public sealed class WagePlan
    {
        public static readonly WagePlan Empty = new();

        /// <summary>이번 정산에서 실제로 낸 시급 합계 (밀린 시급 포함).</summary>
        public int Paid { get; internal set; }
        public List<StaffWorker> PaidWorkers { get; } = new();

        /// <summary>처음 못 받은 알바와 밀린 금액 (= 이번 시급).</summary>
        public List<(StaffWorker worker, int owed)> Unpaid { get; } = new();

        /// <summary>두 번째로 못 받아 떠나는 알바.</summary>
        public List<StaffWorker> Quitting { get; } = new();
    }

    /// <summary>고용할 수 없는 이유.</summary>
    public enum HireBlock { None, Hired, MaxStaff, Cash }

    /// <summary>
    /// 알바 명단 · 후보 · 고용 (GDD 11.1, 11.3). 씬에 하나 둔다 (Site/Logistics).
    /// 후보는 파트가 바뀔 때마다, 그리고 고용 탭 [새로고침]으로 다시 뽑는다. 고용금은 보유 재화에서만 낸다.
    /// </summary>
    public sealed class StaffService : MonoBehaviour
    {
        public static StaffService Instance { get; private set; }

        static readonly StaffRole[] RoleOrder = { StaffRole.Unload, StaffRole.Sort, StaffRole.Pick };

        [SerializeField] StaffWorker workerPrefab;

        readonly List<StaffWorker> workers = new(); // 고용 순서
        readonly List<StaffCandidate> candidates = new();
        readonly Subject<Unit> candidatesChanged = new();
        readonly Subject<Unit> workersChanged = new();
        int hireCount; // 알바 번호 (해고해도 다시 쓰지 않음)

        /// <summary>고용 순서대로 (시급 지급 순서, GDD 11.7).</summary>
        public IReadOnlyList<StaffWorker> Workers => workers;

        public IReadOnlyList<StaffCandidate> Candidates => candidates;

        /// <summary>후보가 새로 뽑히거나 누군가 고용됐다 (고용 탭이 다시 그린다).</summary>
        public Observable<Unit> OnCandidatesChanged => candidatesChanged;

        /// <summary>명단이 바뀌었다: 고용 · 해고 · 역할 변경 (배치 탭이 다시 그린다).</summary>
        public Observable<Unit> OnWorkersChanged => workersChanged;

        public int MaxStaff => GameManager.Instance.Level.MaxStaff;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[StaffService] 씬에 StaffService가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // 파트가 바뀔 때마다 후보 교체 (구독하는 순간 한 번 = 처음 후보)
            if (PartService.Instance != null) PartService.Instance.Part.Subscribe(_ => RefreshCandidates()).AddTo(this);
            else RefreshCandidates();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            candidatesChanged.Dispose();
            workersChanged.Dispose();
        }

        /// <summary>후보를 모두 새로 뽑는다 (파트 교체 · 고용 탭 [새로고침], 무료).</summary>
        public void RefreshCandidates()
        {
            var rules = GameManager.Instance.Database.Staff;
            candidates.Clear();
            for (var i = 0; i < rules.CandidateCount; i++) candidates.Add(new StaffCandidate(rules.RollStats()));
            candidatesChanged.OnNext(Unit.Default);
        }

        public int GetHireCost(StaffCandidate candidate) => GameManager.Instance.Database.Staff.GetHireCost(candidate.Stats.Total);

        public HireBlock GetBlock(StaffCandidate candidate)
        {
            if (candidate.Hired) return HireBlock.Hired;
            if (workers.Count >= MaxStaff) return HireBlock.MaxStaff;
            return GameManager.Instance.Wallet.Cash.CurrentValue < GetHireCost(candidate) ? HireBlock.Cash : HireBlock.None;
        }

        /// <summary>
        /// 후보를 고용한다: 고용금(보유 재화) → 빈 역할로 사무실 생성 지점에 생성 → XP. 실패하면 이유를 돌려준다.
        /// </summary>
        public bool TryHire(StaffCandidate candidate, out HireBlock block)
        {
            block = GetBlock(candidate);
            if (block != HireBlock.None) return false;

            var gm = GameManager.Instance;
            var worker = Spawn(NextEmptyRole(), candidate.Stats);
            if (worker == null) return false;

            gm.Wallet.TrySpend(GetHireCost(candidate));
            candidate.Hired = true;
            candidatesChanged.OnNext(Unit.Default);
            return true;
        }

        /// <summary>빈 역할 (하차 → 분류 → 피킹 순, GDD 11.1). 모두 차 있으면 인원이 가장 적은 역할.</summary>
        public StaffRole NextEmptyRole()
        {
            var best = RoleOrder[0];
            var bestCount = int.MaxValue;
            foreach (var role in RoleOrder)
            {
                var count = CountRole(role);
                if (count >= bestCount) continue;
                best = role;
                bestCount = count;
            }
            return best;
        }

        public int CountRole(StaffRole role)
        {
            var n = 0;
            foreach (var worker in workers)
            {
                if (worker.Role == role) n++;
            }
            return n;
        }

        /// <summary>디버그용: 무작위 스탯으로 바로 만든다 (고용금 · 최대 인원 없음).</summary>
        public StaffWorker Spawn(StaffRole role) => Spawn(role, GameManager.Instance.Database.Staff.RollStats());

        /// <summary>사무실 생성 지점(PC 책상 건너편)에 알바 1명을 만든다. 네비메시가 없으면 null.</summary>
        public StaffWorker Spawn(StaffRole role, StaffStats stats)
        {
            var spawn = StaffSpawnPoint.Current;
            var position = spawn != null ? spawn.transform.position : transform.position;
            if (!NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[StaffService] 알바를 놓을 네비메시가 없습니다.", this);
                return null;
            }

            var worker = Instantiate(workerPrefab, hit.position, spawn != null ? spawn.transform.rotation : Quaternion.identity, transform);
            worker.Number = ++hireCount;
            worker.name = $"Staff_{worker.Number}";
            worker.Setup(role, stats);
            worker.PartShare = CurrentPartShare();
            workers.Add(worker);
            FacilityPop.Play(worker.transform);
            workersChanged.OnNext(Unit.Default);
            return worker;
        }

        /// <summary>역할 변경 (배치 탭, GDD 11.3). 들고 있던 화물은 원래 목적지에 배달한 뒤 바뀐다.</summary>
        public void SetRole(StaffWorker worker, StaffRole role)
        {
            if (worker == null || worker.Role == role) return;
            worker.SetRole(role);
            workersChanged.OnNext(Unit.Default);
        }

        /// <summary>해고 (배치 탭, GDD 11.3). 고용금은 돌려받지 않는다.</summary>
        public void Fire(StaffWorker worker)
        {
            if (worker != null) worker.Fire(); // Remove가 명단에서 뺀다
        }

        /// <summary>이번 파트 시급 (고용한 파트는 남은 시간 비율만큼, 올림).</summary>
        public int GetPartWage(StaffWorker worker)
            => Mathf.CeilToInt(GameManager.Instance.Database.Staff.GetWage(worker.Stats.Total) * worker.PartShare);

        /// <summary>
        /// 시급 계획 (GDD 11.7): 쓸 수 있는 돈(정산 금액 + 보유 재화)으로 고용 순서대로 `밀린 시급 + 이번 시급`을 낸다. 부분 지급 없음.
        /// 못 받은 알바는 밀린 시급이 없으면 미지급, 있으면 퇴사. 상태는 ApplyWages 전엔 바꾸지 않는다.
        /// </summary>
        public WagePlan PlanWages(int available)
        {
            var plan = new WagePlan();
            foreach (var worker in workers)
            {
                var wage = GetPartWage(worker);
                var due = worker.Owed + wage;
                if (due <= available)
                {
                    available -= due;
                    plan.Paid += due;
                    plan.PaidWorkers.Add(worker);
                }
                else if (worker.Owed > 0) plan.Quitting.Add(worker);
                else plan.Unpaid.Add((worker, wage));
            }
            return plan;
        }

        /// <summary>정산 [확인]: 받은 알바는 밀린 시급을 지우고, 못 받은 알바는 미지급(₩), 두 번째면 퇴사. 남은 알바는 다음 파트 시급 비율 1.</summary>
        public void ApplyWages(WagePlan plan)
        {
            foreach (var worker in plan.PaidWorkers)
            {
                if (worker != null) worker.SetOwed(0);
            }
            foreach (var (worker, owed) in plan.Unpaid)
            {
                if (worker != null) worker.SetOwed(owed);
            }
            foreach (var worker in plan.Quitting)
            {
                if (worker != null) worker.Quit();
            }
            foreach (var worker in workers) worker.PartShare = 1f;
            workersChanged.OnNext(Unit.Default);
        }

        static float CurrentPartShare()
        {
            var parts = PartService.Instance;
            if (parts == null || !parts.IsRunning || parts.CurrentPartLength <= 0f) return 1f;
            return Mathf.Clamp01(parts.Remaining.CurrentValue / parts.CurrentPartLength);
        }

        internal void Remove(StaffWorker worker)
        {
            if (workers.Remove(worker)) workersChanged.OnNext(Unit.Default);
        }
    }
}
