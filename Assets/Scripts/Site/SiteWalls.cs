using System.Collections.Generic;
using APC.Core;
using APC.Logistics;
using R3;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 부지 바깥 벽 (M14-2). 열린 칸과 열리지 않은 칸 · 부지 밖 사이 변에만 1m 조각을 이어 붙여 세우고, 꼭짓점에 기둥을 둔다.
    /// 카메라 쪽(앞) 변은 낮은 벽, 뒤쪽 변은 높은 벽. 하차 · 상차장 모듈의 주차장 쪽(모듈 로컬 −x) 변은 비운다 (트럭 출입).
    /// 해금(SiteGrid.OnUnlocked)마다 다시 세운다 — 칸 사이 벽이 사라지고 새 바깥 테두리에 생긴다. 허물어지는 연출은 나중에.
    /// 충돌체는 없다 (걷는 범위는 SiteGrid.IsWalkable · 네비메시가 정함).
    /// </summary>
    public sealed class SiteWalls : MonoBehaviour
    {
        static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        [SerializeField, Tooltip("앞(카메라 쪽) 벽 조각 1m (wall_s)")] GameObject lowWall;
        [SerializeField, Tooltip("뒤 벽 조각 1m (wall_l)")] GameObject highWall;
        [SerializeField, Tooltip("앞 꼭짓점 기둥 (pillar_s)")] GameObject lowPillar;
        [SerializeField, Tooltip("뒤 꼭짓점 기둥 (pillar_L)")] GameObject highPillar;
        [SerializeField, Tooltip("조각 재질 (M_Prop). 비우면 fbx 재질")] Material material;
        [SerializeField, Min(0.1f), Tooltip("벽 조각 길이 (m)")] float pieceLength = 1f;

        Transform root;

        void Start()
        {
            var grid = SiteGrid.Instance;
            if (grid == null || lowWall == null || highWall == null) return;
            Build();
            grid.OnUnlocked.Subscribe(_ => Build()).AddTo(this);
        }

        void Build()
        {
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Walls").transform;
            root.SetParent(transform, false);

            var grid = SiteGrid.Instance;
            var cellSize = GameManager.Instance.Database.Site.CellSize;
            var toCamera = Camera.main != null ? -Camera.main.transform.forward : Vector3.back;
            toCamera.y = 0f;
            var pieces = Mathf.Max(1, Mathf.RoundToInt(cellSize / pieceLength));
            var pillars = new Dictionary<Vector2Int, (Vector3 position, bool high)>();

            foreach (var cell in grid.OpenedCells)
            {
                var center = grid.CellCenter(cell);
                foreach (var d in Directions)
                {
                    if (grid.IsCellOpen(cell + d)) continue;
                    var outward = (grid.CellCenter(cell + d) - center).normalized;
                    if (IsTruckSide(grid.ModuleAt(cell), outward)) continue;

                    var high = Vector3.Dot(outward, toCamera) < 0f; // 카메라 반대쪽 = 뒤 벽
                    var prefab = high ? highWall : lowWall;
                    var middle = center + outward * (cellSize * 0.5f);
                    var along = new Vector3(-outward.z, 0f, outward.x);
                    var yaw = Quaternion.LookRotation(outward);
                    for (var i = 0; i < pieces; i++)
                    {
                        var position = middle + along * ((i + 0.5f) * pieceLength - cellSize * 0.5f);
                        Place(prefab, position, yaw);
                    }

                    foreach (var end in new[] { middle - along * (cellSize * 0.5f), middle + along * (cellSize * 0.5f) })
                    {
                        var key = new Vector2Int(Mathf.RoundToInt(end.x * 10f), Mathf.RoundToInt(end.z * 10f));
                        pillars[key] = (end, high || (pillars.TryGetValue(key, out var old) && old.high));
                    }
                }
            }

            foreach (var pillar in pillars.Values)
            {
                var prefab = pillar.high ? highPillar : lowPillar;
                if (prefab != null) Place(prefab, pillar.position, Quaternion.identity);
            }
        }

        /// <summary>하차 · 상차장 모듈의 주차장 쪽 (모듈 로컬 −x, SiteLayout 회전 규칙).</summary>
        static bool IsTruckSide(SiteModule module, Vector3 outward)
        {
            if (module == null) return false;
            var isLot = module.GetComponentInChildren<InboundBay>(true) != null || module.GetComponentInChildren<OutboundBay>(true) != null;
            return isLot && Vector3.Dot(module.transform.rotation * Vector3.left, outward) > 0.7f;
        }

        void Place(GameObject prefab, Vector3 position, Quaternion yaw)
        {
            var piece = Instantiate(prefab, position, yaw * prefab.transform.rotation, root);
            if (material == null) return;
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                var shared = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < shared.Length; i++) shared[i] = material;
                r.sharedMaterials = shared;
            }
        }
    }
}
