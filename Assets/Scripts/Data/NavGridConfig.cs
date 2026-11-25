using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 알바 길찾기 격자 (방 단위 2층 A*, 2026-11-24 측정용 · 알파 후보). `Assets/Data/NavGridConfig.asset`.
    /// 지금은 측정 도구(`NavBenchmarkConfig`)만 쓴다 — 게임에 넣을 때 GameDatabase에 연결한다.
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Data/Nav Grid Config", fileName = "NavGridConfig")]
    public sealed class NavGridConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f), Tooltip("격자 칸 한 변 (m). 방(모듈 칸 10m)과 타일을 나눠 떨어져야 한다 (임시)")] float cellSize = 0.5f;
        [SerializeField, Min(0f), Tooltip("장애물을 사방으로 키우는 거리 (m). 지금 NavMesh 굽기 에이전트 반지름과 같게")] float agentRadius = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("타일 한 변 (m) — 타일별 다시 굽기 단위 (바닥 타일 2m)")] float tileSize = 2f;
        [SerializeField, Tooltip("이 높이 띠(m)에 걸친 충돌체만 장애물 (바닥은 빠짐)")] float obstacleMinY = 0.3f;
        [SerializeField] float obstacleMaxY = 2f;
        [SerializeField, Min(0f), Tooltip("출발 · 도착이 막힌 칸이면 이 반경(m) 안 가장 가까운 칸에 붙인다")] float snapRadius = 2f;
        [SerializeField, Min(1), Tooltip("프레임당 길찾기 최대 수 (넘으면 다음 프레임으로 줄 세움, 임시)")] int maxPathsPerFrame = 20;

        public float CellSize => cellSize;
        public float AgentRadius => agentRadius;
        public float TileSize => tileSize;
        public float ObstacleMinY => obstacleMinY;
        public float ObstacleMaxY => obstacleMaxY;
        public float SnapRadius => snapRadius;
        public int MaxPathsPerFrame => maxPathsPerFrame;
        public int SnapCells => Mathf.RoundToInt(snapRadius / cellSize);

        static NavGridConfig fallback;

        public static NavGridConfig OrDefault(NavGridConfig config)
        {
            if (config != null) return config;
            if (fallback == null) fallback = CreateInstance<NavGridConfig>();
            return fallback;
        }
    }
}
