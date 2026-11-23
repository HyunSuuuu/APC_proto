using System;
using System.Threading.Tasks;
using APC.Core;
using APC.Staff;
using R3;
using UnityEngine;

namespace APC.Offline
{
    /// <summary>
    /// 오프라인 수익 (GDD 9.7). 앱이 백그라운드로 가거나 꺼질 때 상태(종료 시각 · 알바 하루 수익 · 하루 월급)를 저장하고,
    /// 돌아오거나 다시 켤 때 그 사이 시간만큼 `알바 몫 − 월급`을 보유 재화에 바로 넣는다 (최대 5시간 `임시`).
    /// 정산 방식 1(기기 시간) · 2(서버 시간)를 디버그 메뉴에서 바꾼다 — 모드는 종료할 때 스냅샷에 기록되므로 바꾸면 다음 종료부터 적용.
    /// GameManager가 있는 씬이 열리면 스스로 생긴다 (씬에 따로 두지 않음).
    /// </summary>
    public sealed class OfflineRewardService : MonoBehaviour
    {
        const string SnapshotKey = "APC.Offline.Snapshot";
        const string ModeKey = "APC.Offline.Mode";

        public static OfflineRewardService Instance { get; private set; }

        readonly Subject<OfflineResult> granted = new();
        readonly DeviceClock deviceClock = new();
        HttpDateClock serverClock;
        OfflineMode mode;
        bool claiming;

        /// <summary>다음 종료에 쓸 정산 방식 (디버그에서 바꿈, PlayerPrefs에 기억).</summary>
        public OfflineMode Mode
        {
            get => mode;
            set
            {
                mode = value;
                PlayerPrefs.SetInt(ModeKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>오프라인 수익을 지급했다 (복귀 팝업이 구독할 자리).</summary>
        public Observable<OfflineResult> OnGranted => granted;

        /// <summary>마지막 정산 결과 (디버그 표시).</summary>
        public OfflineResult? LastResult { get; private set; }

        /// <summary>마지막 상태 메시지 (디버그 표시).</summary>
        public string Status { get; private set; } = "-";

        public bool HasServerTime => serverClock != null && serverClock.HasAnchor;
        public bool HasPendingSnapshot => PlayerPrefs.HasKey(SnapshotKey);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null || GameManager.Instance == null) return;
            new GameObject(nameof(OfflineRewardService)).AddComponent<OfflineRewardService>();
        }

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            mode = (OfflineMode)PlayerPrefs.GetInt(ModeKey, (int)OfflineMode.Local);
            serverClock = new HttpDateClock(GameManager.Instance.Database.Economy.OfflineTimeUrl);
        }

        void Start()
        {
            _ = RefreshServerTimeAsync(); // 종료 저장 때 쓸 서버 시각 기준점
            _ = ClaimAsync(); // 다시 켰을 때 (지난 실행의 종료 스냅샷)
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            granted.Dispose();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveSnapshot();
            else _ = ClaimAsync(); // 시작할 때도 불릴 수 있다 — 스냅샷이 없으면 아무것도 안 함
        }

        void OnApplicationQuit() => SaveSnapshot(); // 에디터 Play 정지 · PC 종료. 모바일은 강제 종료되면 안 올 수 있어 Pause가 주 경로

        /// <summary>지금 상태로 종료 스냅샷을 저장한다. 2번인데 서버 시각을 아직 못 받았으면 저장하지 않는다 (기기 시계로 대신하지 않음).</summary>
        public bool SaveSnapshot()
        {
            if (!ClockFor(mode).TryGetNow(out var now))
            {
                Status = "서버 시각 없음 — 종료 상태 저장 안 함";
                Debug.LogWarning($"[Offline] {Status}");
                return false;
            }
            Save(Capture(mode, now));
            Status = $"종료 저장 ({(int)mode + 1}.{mode})";
            return true;
        }

        /// <summary>
        /// 저장된 스냅샷으로 정산한다. 스냅샷은 지급 전에 지워 두 번 받지 않는다.
        /// 2번은 서버 시각을 새로 받아오고, 실패하면 스냅샷을 남겨 다음 복귀 때 다시 시도한다.
        /// </summary>
        public async Task ClaimAsync()
        {
            if (claiming || !TryLoad(out var snapshot)) return;
            claiming = true;
            try
            {
                var (ok, now, latency) = await ClockFor(snapshot.Mode).FetchNowAsync();
                if (this == null) return;
                if (!ok)
                {
                    Status = "서버 시각 실패 — 다음 복귀 때 다시 시도";
                    return;
                }

                var result = Calculate(snapshot, now, latency);
                PlayerPrefs.DeleteKey(SnapshotKey);
                PlayerPrefs.Save();

                if (result.ElapsedSeconds < GameManager.Instance.Database.Economy.OfflineMinSeconds)
                {
                    Status = $"{result.ElapsedSeconds:0}초 — 최소 시간 미만, 지급 안 함";
                    return;
                }
                if (result.Net > 0) GameManager.Instance.Wallet.Add((int)Math.Min(result.Net, int.MaxValue));
                LastResult = result;
                Status = $"지급 +{result.Net:N0}";
                Debug.Log($"[Offline] 정산 {result}");
                granted.OnNext(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                claiming = false;
            }
        }

        /// <summary>디버그: 지금 상태로 N분 전에 종료한 것처럼 스냅샷을 만들고 바로 정산한다 (현재 모드).</summary>
        public void DebugSimulate(int minutes)
        {
            if (!ClockFor(mode).TryGetNow(out var now))
            {
                Status = "서버 시각 없음 — 먼저 'Fetch server time'";
                return;
            }
            Save(Capture(mode, now - minutes * 60_000L));
            _ = ClaimAsync();
        }

        /// <summary>디버그: 같은 상태 · 같은 N분으로 1 · 2번을 모두 계산해 로그로 남긴다 (지급 없음).</summary>
        public async Task DebugCompareAsync(int minutes)
        {
            var local = await CalculateFor(OfflineMode.Local, minutes);
            var server = await CalculateFor(OfflineMode.ServerTime, minutes);
            if (this == null) return;
            var line = $"[Offline 비교 {minutes}분] {local?.ToString() ?? "1.Local 실패"} | {server?.ToString() ?? "2.ServerTime 실패"}";
            Status = "비교 완료 (Console)";
            Debug.Log(line);
        }

        public Task RefreshServerTimeAsync() => serverClock.FetchNowAsync();

        async Task<OfflineResult?> CalculateFor(OfflineMode target, int minutes)
        {
            var clock = ClockFor(target);
            if (!clock.TryGetNow(out var exit)) return null;
            var snapshot = Capture(target, exit - minutes * 60_000L);
            var (ok, now, latency) = await clock.FetchNowAsync();
            return ok ? Calculate(snapshot, now, latency) : null;
        }

        IOfflineClock ClockFor(OfflineMode target) => target == OfflineMode.ServerTime ? serverClock : deviceClock;

        static OfflineResult Calculate(OfflineSnapshot snapshot, long now, long latency)
        {
            var economy = GameManager.Instance.Database.Economy;
            var dayLength = economy.PartLengthSeconds * 2.0; // 하루 = 1 · 2파트
            return OfflineFormula.Calculate(snapshot, now, economy.OfflineMaxHours * 3600.0, dayLength, latency);
        }

        /// <summary>지금 알바 명단으로 하루 수익 · 하루 월급을 구한다 (밀린 시급 · 고용한 파트 비율은 오프라인에선 무시).</summary>
        static OfflineSnapshot Capture(OfflineMode target, long exitUnixMs)
        {
            var database = GameManager.Instance.Database;
            var snapshot = new OfflineSnapshot { mode = (int)target, exitUnixMs = exitUnixMs };
            var staff = StaffService.Instance;
            if (staff == null) return snapshot;
            foreach (var worker in staff.Workers)
            {
                if (worker == null) continue;
                var total = worker.Stats.Total;
                snapshot.grossPerDay += total * database.Economy.OfflineIncomePerStat;
                snapshot.wagePerDay += database.Staff.GetWage(total);
                snapshot.workerCount++;
            }
            return snapshot;
        }

        static void Save(OfflineSnapshot snapshot)
        {
            PlayerPrefs.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
        }

        static bool TryLoad(out OfflineSnapshot snapshot)
        {
            snapshot = null;
            var json = PlayerPrefs.GetString(SnapshotKey, null);
            if (string.IsNullOrEmpty(json)) return false;
            snapshot = JsonUtility.FromJson<OfflineSnapshot>(json);
            return snapshot != null;
        }
    }
}
