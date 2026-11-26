using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 부지 모듈 프리팹의 루트 (GDD 4.2). n × m 칸 크기이며, 안에 바닥 · 존 · 시설을 담는다.
    /// 루트 위치 = 차지하는 칸들의 중심. 어디에 · 언제 붙는지는 SiteLayout(config)이 정한다.
    /// 새 모듈은 메뉴 APC > Module > New Module로 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SiteModule : MonoBehaviour
    {
        /// <summary>재배치 규칙 (GDD 4.4): 고정(사무실 · 쓰레기장 · 하차 · 상차) / 빈 공간 / 옮길 수 있음(보관 · 반품).</summary>
        public enum ModuleKind { Fixed, Empty, Movable }

        [SerializeField, Tooltip("차지하는 칸 수 (x = 가로 열, y = 세로 행). 회전 0° 기준")] Vector2Int size = Vector2Int.one;
        [SerializeField, Min(1f), Tooltip("칸 한 변 (m). SiteLayout의 칸 크기와 같아야 한다")] float cellSize = 10f;
        [SerializeField, Tooltip("빈 공간 = 해금 모듈이 그 칸에 붙으면 꺼진다. 옮길 수 있음 = 보관 · 반품 (GDD 4.4)")] ModuleKind kind;

        public Vector2Int Size => size;
        public float CellSize => cellSize;
        public ModuleKind Kind => kind;

        void Awake() => gameObject.AddComponent<Visuals.StaticShadows>(); // 메인 라이트 그림자 대신 고정 물체 그림자 판 (2026-11-27)

        /// <summary>에디터 생성용.</summary>
        public void Configure(Vector2Int moduleSize, float moduleCellSize, ModuleKind moduleKind = ModuleKind.Fixed)
        {
            kind = moduleKind;
            size = new Vector2Int(Mathf.Max(1, moduleSize.x), Mathf.Max(1, moduleSize.y));
            cellSize = Mathf.Max(1f, moduleCellSize);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            var width = size.x * cellSize;
            var depth = size.y * cellSize;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(width, 0.1f, depth));

            // 칸 경계
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            for (var x = 1; x < size.x; x++)
            {
                var px = -width * 0.5f + x * cellSize;
                Gizmos.DrawLine(new Vector3(px, 0.05f, -depth * 0.5f), new Vector3(px, 0.05f, depth * 0.5f));
            }
            for (var y = 1; y < size.y; y++)
            {
                var pz = -depth * 0.5f + y * cellSize;
                Gizmos.DrawLine(new Vector3(-width * 0.5f, 0.05f, pz), new Vector3(width * 0.5f, 0.05f, pz));
            }
        }
    }
}
