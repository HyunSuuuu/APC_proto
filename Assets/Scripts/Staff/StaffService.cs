using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Site;
using APC.Economy;
using R3;
using UnityEngine;
using UnityEngine.AI;

namespace APC.Staff
{
    /// <summary>고용 탭 후보 1명 (GDD 11.1). 고용하면 Hired가 켜지고 다음 교체(파트 · 새로고침)까지 그 자리에 남는다.</summary>
    public sealed class StaffCandidate
    {
        public StaffCandidate(StaffRole role, StaffStats stats, bool costume, bool free = false)
        {
            Role = role;
            Stats = stats;
            Costume = costume;
            Free = free;
        }

        /// <summary>고용금 0 (튜토리얼 첫 고용, GDD 13).</summary>
        public bool Free { get; }

        /// <summary>정해져 나온 직무 (고용 뒤 바꿀 수 없음, GDD 11.1 · 2026-11-18).</summary>
        public StaffRole Role { get; }
        public StaffStats Stats { get; }
        /// <summary>코스튬 알바 (아니면 물류 유니폼, GDD 11.1).</summary>
        public bool Costume { get; }
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
    /// 후보는 자동 교체 타이머(StaffRules.CandidateRefreshSeconds)가 0이 될 때마다, 그리고 고용 탭 [새로고침]으로 다시 뽑는다. 고용금은 보유 재화에서만 낸다.
    /// 자동 교체 타이머는 1파트가 시작된 뒤부터 파트와 따로 흐르고, 팝업이 열려 있어도 실제 시간으로 줄어든다 (일시정지에서만 멈춤, 2026-11-26).
    /// </summary>
    public sealed class StaffService : MonoBehaviour
    {
        public static StaffService Instance { get; private set; }

        static readonly StaffRole[] Roles = { StaffRole.Unload, StaffRole.Sort, StaffRole.Pick, StaffRole.Returns, StaffRole.Forklift };

        [SerializeField] StaffWorker workerPrefab;

        readonly List<StaffWorker> workers = new(); // 고용 순서
        readonly List<StaffCandidate> candidates = new();
        readonly Subject<Unit> candidatesChanged = new();
        readonly Subject<Unit> workersChanged = new();
        int hireCount; // 알바 번호 (해고해도 다시 쓰지 않음)
        float refreshRemaining = -1f; // 후보 자동 교체까지 (초). 음수 = 아직 시작 안 함 (1파트 전)

        /// <summary>고용 순서대로 (시급 지급 순서, GDD 11.7).</summary>
        public IReadOnlyList<StaffWorker> Workers => workers;

        public IReadOnlyList<StaffCandidate> Candidates => candidates;

        /// <summary>후보가 새로 뽑히거나 누군가 고용됐다 (고용 탭이 다시 그린다).</summary>
        public Observable<Unit> OnCandidatesChanged => candidatesChanged;

        /// <summary>명단이 바뀌었다: 고용 · 해고 (배치 탭이 다시 그린다).</summary>
        public Observable<Unit> OnWorkersChanged => workersChanged;

        public int MaxStaff => GameManager.Instance.Level.MaxStaff;

        /// <summary>후보 자동 교체까지 남은 초. 음수 = 아직 시작 안 함 (1파트 전).</summary>
        public float CandidateRefreshRemaining => refreshRemaining;

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

        void Start() => RefreshCandidates(); // 처음 후보

        // 후보 자동 교체: 1파트가 시작되면 타이머 시작. 팝업 중에도 실제 시간으로 흐르고, 일시정지에서만 멈춘다
        void Update()
        {
            var gm = GameManager.Instance;
            var parts = PartService.Instance;
            if (gm == null || parts == null || parts.Part.CurrentValue == 0) return;
            var interval = gm.Database.Staff.CandidateRefreshSeconds;
            if (refreshRemaining < 0f) refreshRemaining = interval;

            var state = gm.State.CurrentValue;
            if (state == GameState.Paused) return;
            refreshRemaining -= state == GameState.Popup ? Time.unscaledDeltaTime : Time.deltaTime;
            if (refreshRemaining > 0f) return;
            refreshRemaining += interval;
            RefreshCandidates();
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
            var open = new List<StaffRole>();
            foreach (var role in Roles)
            {
                if (IsRoleOpen(role)) open.Add(role);
            }
            // 후보마다 열린 직무 중 무작위 (같은 확률, GDD 11.1)
            for (var i = 0; i < rules.CandidateCount; i++) candidates.Add(new StaffCandidate(open[Random.Range(0, open.Count)], rules.RollStats(), rules.RollCostume()));
            candidatesChanged.OnNext(Unit.Default);
        }

        public int GetHireCost(StaffCandidate candidate) => candidate.Free ? 0 : GameManager.Instance.Database.Staff.GetHireCost(candidate.Role, candidate.Stats.Total);

        /// <summary>튜토리얼 첫 고용 (GDD 13): 첫 후보 자리를 무료 고정 알바로 바꾼다. 이미 있으면 그대로.</summary>
        public void OfferFreeCandidate(StaffRole role, StaffStats stats)
        {
            foreach (var c in candidates)
            {
                if (c.Free) return;
            }
            var free = new StaffCandidate(role, stats, false, true);
            if (candidates.Count > 0) candidates[0] = free;
            else candidates.Add(free);
            candidatesChanged.OnNext(Unit.Default);
        }

        public HireBlock GetBlock(StaffCandidate candidate)
        {
            if (candidate.Hired) return HireBlock.Hired;
            if (workers.Count >= MaxStaff) return HireBlock.MaxStaff;
            return GameManager.Instance.Wallet.Cash.CurrentValue < GetHireCost(candidate) ? HireBlock.Cash : HireBlock.None;
        }

        /// <summary>
        /// 후보를 고용한다: 고용금(지갑) → 후보 직무로 사무실 생성 지점에 생성. 실패하면 이유를 돌려준다.
        /// </summary>
        public bool TryHire(StaffCandidate candidate, out HireBlock block)
        {
            block = GetBlock(candidate);
            if (block != HireBlock.None) return false;

            var gm = GameManager.Instance;
            var worker = Spawn(candidate.Role, candidate.Stats, candidate.Costume);
            if (worker == null) return false;

            gm.Wallet.TrySpend(GetHireCost(candidate));
            candidate.Hired = true;
            candidatesChanged.OnNext(Unit.Default);
            return true;
        }

        /// <summary>반품 직무는 반품 구역(Ret1), 지게차 직무는 D보관(StoreD)을 지은 뒤에 열린다 (GDD 11.3).</summary>
        // shortcut: 첫 반품 구역 `Ret1` · `StoreD` id로만 본다 — 구역이 여러 종류가 되면 SiteGrid에서 Kind로 찾기
        public bool IsRoleOpen(StaffRole role) => role switch
        {
            StaffRole.Returns => SiteGrid.Instance != null && SiteGrid.Instance.IsUnlocked("Ret1"),
            StaffRole.Forklift => SiteGrid.Instance != null && SiteGrid.Instance.IsUnlocked("StoreD"),
            _ => true,
        };

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
        public StaffWorker Spawn(StaffRole role)
        {
            var rules = GameManager.Instance.Database.Staff;
            return Spawn(role, rules.RollStats(), rules.RollCostume());
        }

        /// <summary>사무실 생성 지점(PC 책상 건너편)에 알바 1명을 만든다. 네비메시가 없으면 null.</summary>
        public StaffWorker Spawn(StaffRole role, StaffStats stats, bool costume)
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
            worker.Setup(role, stats, costume);
            worker.PartShare = CurrentPartShare();
            workers.Add(worker);
            FacilityPop.Play(worker.transform);
            workersChanged.OnNext(Unit.Default);
            return worker;
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
