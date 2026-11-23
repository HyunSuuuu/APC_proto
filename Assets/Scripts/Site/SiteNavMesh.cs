using APC.Core;
using DG.Tweening;
using R3;
using Unity.AI.Navigation;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 부지 네비메시 (체크리스트 M5-5, M6 알바 대비). Site 루트의 NavMeshSurface로 **열린 모듈 바닥(충돌체)** 에만 굽는다.
    /// 잠긴 칸에는 바닥이 없으므로 알바도 열린 칸 안에서만 다닌다 (플레이어의 칸 판정과 같은 효과).
    /// 실행 시작과 모듈 해금마다 다시 굽는다 — 해금 팝 연출(크기 0 → 1)이 끝난 뒤에 굽는다.
    /// 임시 초록 Ground는 NavMeshModifier(ignoreFromBuild)로 뺀다.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class SiteNavMesh : MonoBehaviour
    {
        NavMeshSurface surface;
        Tween pending;
        readonly System.Diagnostics.Stopwatch buildWatch = new();

        // 성능 측정용 (PerformanceHUD가 읽는다): 굽기 1회 시간 · 최대 · 횟수 — A* 전환 비교 기준 (2026-11-23)
        public static float LastBuildMs { get; private set; }
        public static float MaxBuildMs { get; private set; }
        public static int BuildCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStats() => LastBuildMs = MaxBuildMs = BuildCount = 0;

        void Start()
        {
            surface = GetComponent<NavMeshSurface>();
            Build();

            if (SiteGrid.Instance == null) return;
            SiteGrid.Instance.OnUnlocked.Subscribe(_ => ScheduleBuild()).AddTo(this);
            SiteGrid.Instance.OnMoved.Subscribe(_ => ScheduleBuild()).AddTo(this); // 모듈 재배치 (GDD 4.4)
        }

        /// <summary>묶음 해금처럼 한꺼번에 여러 모듈이 붙어도 한 번만 굽는다.</summary>
        void ScheduleBuild()
        {
            pending?.Kill();
            var delay = GameManager.Instance.Database.Logistics.FacilityPopDuration + 0.05f;
            pending = DOVirtual.DelayedCall(delay, Build, ignoreTimeScale: false).SetLink(gameObject);
        }

        void Build()
        {
            pending = null;
            buildWatch.Restart();
            surface.BuildNavMesh();
            buildWatch.Stop();
            LastBuildMs = (float)buildWatch.Elapsed.TotalMilliseconds;
            MaxBuildMs = Mathf.Max(MaxBuildMs, LastBuildMs);
            BuildCount++;
            Debug.Log($"[SiteNavMesh] 굽기 {LastBuildMs:F2} ms (#{BuildCount})");
        }
    }
}
