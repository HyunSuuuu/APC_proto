using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 길찾기 비교 측정 (NavMesh vs 방 단위 A*, 2026-11-24) 설정. `Assets/Data/NavBenchmarkConfig.asset`.
    /// 메뉴 `APC > Test > Nav Benchmark (A* vs NavMesh)`가 에셋 경로로 읽는다 — GameDatabase에 넣지 않는다 (측정용).
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Data/Nav Benchmark Config", fileName = "NavBenchmarkConfig")]
    public sealed class NavBenchmarkConfig : ScriptableObject
    {
        [SerializeField] NavGridConfig grid;
        [SerializeField, Tooltip("NavMeshAgent 설정(반지름 · 높이 · 가속 · 회전 · 멈춤 거리 · 회피)을 복사할 알바 프리팹")] GameObject staffWorkerPrefab;

        [Header("측정 알바")]
        [SerializeField, Min(1)] int agentCount = 100;
        [SerializeField, Min(0f)] float warmupSeconds = 5f;
        [SerializeField, Min(1f)] float measureSeconds = 20f;
        [SerializeField, Min(1)] int repeats = 3;

        [Header("0단계 보정 (실제 알바)")]
        [SerializeField, Tooltip("실제 알바를 뽑아 초당 길 요청 수를 잰다. 끄면 기본 길 요청 간격")] bool calibrate = true;
        [SerializeField, Min(1f)] float calibrateSeconds = 60f;
        [SerializeField, Tooltip("직무별 수: 하차 · 보관 · 상차 · 지게차 (TycoonTest 비율 20 · 30 · 20 · 8을 100명으로)")] int[] calibrateRoles = { 26, 38, 26, 10 };
        [SerializeField, Min(0f), Tooltip("실제 알바를 한 명씩 뽑는 간격 (초). 한 프레임에 100명을 뽑자 에디터가 GPU 장치 오류로 꺼졌음 (2026-11-24) — TycoonTest와 같은 0.1초")] float calibrateSpawnInterval = 0.1f;
        [SerializeField, Min(0.1f), Tooltip("보정을 안 할 때 측정 알바가 새 목적지를 받는 간격 (초, 임시)")] float defaultRepathInterval = 4f;

        [Header("길 1개")]
        [SerializeField, Min(1)] int pathQueries = 1000;
        [SerializeField, Min(1f), Tooltip("자기 검사: 방 2층 길 길이 ≤ 한 장 격자 × 이 값 (임시)")] float lengthRatioLimit = 1.4f;

        [Header("레일 (지나갈 수 없는 상자로 흉내)")]
        [SerializeField] Vector3 railSize = new(6f, 1f, 1f);
        [SerializeField, Min(1), Tooltip("한 번에 까는 레일 수 (서로 다른 방에 하나씩)")] int railBurst = 10;
        [SerializeField, Min(1), Tooltip("레일 자리 고르기 시도 수 (방마다)")] int railTries = 20;
        [SerializeField, Min(1), Tooltip("carving · 비동기 굽기 · 다시 길찾기 비용을 모으는 프레임 수")] int eventWindowFrames = 30;

        [Header("기타")]
        [SerializeField] int seed = 1234;
        [SerializeField, Tooltip("첫 반복의 평소 단계마다 Profiler 기록(.raw)을 남긴다 — Profiler 창 Load로 연다")] bool recordProfilerLog = true;
        [SerializeField, Min(1f), Tooltip("Profiler 기록 길이 (초). 에디터에서 1초에 약 20MB")] float profilerLogSeconds = 5f;

        public NavGridConfig Grid => grid;
        public GameObject StaffWorkerPrefab => staffWorkerPrefab;
        public int AgentCount => agentCount;
        public float WarmupSeconds => warmupSeconds;
        public float MeasureSeconds => measureSeconds;
        public int Repeats => repeats;
        public bool Calibrate => calibrate;
        public float CalibrateSeconds => calibrateSeconds;
        public int[] CalibrateRoles => calibrateRoles;
        public float CalibrateSpawnInterval => calibrateSpawnInterval;
        public float DefaultRepathInterval => defaultRepathInterval;
        public int PathQueries => pathQueries;
        public float LengthRatioLimit => lengthRatioLimit;
        public Vector3 RailSize => railSize;
        public int RailBurst => railBurst;
        public int RailTries => railTries;
        public int EventWindowFrames => eventWindowFrames;
        public int Seed => seed;
        public bool RecordProfilerLog => recordProfilerLog;
        public float ProfilerLogSeconds => profilerLogSeconds;
    }
}
