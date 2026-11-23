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
        [SerializeField, Tooltip("벽 조각(wall_s · wall_l)만 쓰는 재질 (M_Wall, APC/WallCutout + _WALL_GRADIENT). 비우면 material")] Material wallMaterial;
        [SerializeField, Tooltip("벽 안쪽 면 그라데이션 아래 색")] Color gradientBottom = new(234 / 255f, 98 / 255f, 54 / 255f);
        [SerializeField, Tooltip("벽 안쪽 면 그라데이션 위 색")] Color gradientTop = new(0xE6 / 255f, 0xD9 / 255f, 0xAE / 255f);
        [SerializeField, Min(0.01f), Tooltip("그라데이션 높이 (m, 조각 바닥부터). 위쪽 갈색 띠가 3.6955m에서 시작해 그 아래까지만")] float gradientHeight = 3.69f;
        [SerializeField, Min(0.1f), Tooltip("벽 조각 길이 (m)")] float pieceLength = 1f;
        [SerializeField, Tooltip("사무실 바깥 벽 창문 (window_outside, 폭 2m = 조각 2개). 비우면 창문 없음")] GameObject windowPrefab;
        [SerializeField, Tooltip("사무실 벽 한 변에서 창문이 시작하는 조각 번호 (0부터, 창문은 그 조각과 다음 조각 자리)")] int[] windowStarts = { 2, 6 };
        /// <summary>반투명 대상 하나 (벽 조각 렌더러 · WallFadeTarget 렌더러).</summary>
        sealed class Fade
        {
            public Material[] original;
            public float alpha = 1f;
            public bool hidden;   // 지금 플레이어를 가림
            public bool swapped;  // 반투명 재질로 바뀐 상태
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int GradientBottomId = Shader.PropertyToID("_WallGradientBottom");
        static readonly int GradientTopId = Shader.PropertyToID("_WallGradientTop");
        static readonly int GradientHeightId = Shader.PropertyToID("_WallGradientHeight");

        Transform root;
        readonly Dictionary<Renderer, Fade> fades = new();
        readonly List<Renderer> stale = new();
        MaterialPropertyBlock block;
        Transform player;
        float nextFadeCheck;

        void Start()
        {
            var grid = SiteGrid.Instance;
            if (grid == null || lowWall == null || highWall == null) return;
            ApplyGradient();
            Build();
            grid.OnUnlocked.Subscribe(_ => Build()).AddTo(this);
        }

        void OnValidate() => ApplyGradient(); // 인스펙터에서 바꾸면 Play 중에도 바로 반영

        /// <summary>안쪽 면 그라데이션 값을 전역 셰이더 값으로. SetGlobalColor가 sRGB → 선형으로 바꿔 준다 (WallCutout.hlsl _WALL_GRADIENT).</summary>
        void ApplyGradient()
        {
            Shader.SetGlobalColor(GradientBottomId, gradientBottom);
            Shader.SetGlobalColor(GradientTopId, gradientTop);
            Shader.SetGlobalFloat(GradientHeightId, gradientHeight);
        }

        void Build()
        {
            if (root != null) Destroy(root.gameObject);
            stale.Clear();
            foreach (var r in fades.Keys)
            {
                if (r == null || r.transform.IsChildOf(root)) stale.Add(r); // 옛 벽 조각 (주차장은 남김)
            }
            foreach (var r in stale) fades.Remove(r);
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
                    var office = windowPrefab != null && IsOffice(grid.ModuleAt(cell));
                    for (var i = 0; i < pieces; i++)
                    {
                        // 사무실 바깥 벽: 조각 2개 자리마다 창문 하나 (windowStarts, 2026-11-19)
                        if (office && i + 1 < pieces && System.Array.IndexOf(windowStarts, i) >= 0)
                        {
                            Place(windowPrefab, middle + along * ((i + 1f) * pieceLength - cellSize * 0.5f), yaw);
                            i++;
                            continue;
                        }
                        var position = middle + along * ((i + 0.5f) * pieceLength - cellSize * 0.5f);
                        Place(prefab, position, yaw, wallMaterial != null ? wallMaterial : material);
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

        /// <summary>사무실 모듈인가 (계약 PC = 책상이 있는 모듈).</summary>
        static bool IsOffice(SiteModule module) => module != null && module.GetComponentInChildren<Contracts.ContractPC>(true) != null;

        /// <summary>하차 · 상차장 모듈의 주차장 쪽 (모듈 로컬 −x, SiteLayout 회전 규칙).</summary>
        static bool IsTruckSide(SiteModule module, Vector3 outward)
        {
            if (module == null) return false;
            var isLot = module.GetComponentInChildren<InboundBay>(true) != null || module.GetComponentInChildren<OutboundBay>(true) != null;
            return isLot && Vector3.Dot(module.transform.rotation * Vector3.left, outward) > 0.7f;
        }

        void Place(GameObject prefab, Vector3 position, Quaternion yaw) => Place(prefab, position, yaw, material);

        void Place(GameObject prefab, Vector3 position, Quaternion yaw, Material pieceMaterial)
        {
            var piece = Instantiate(prefab, position, yaw * prefab.transform.rotation, root);
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                if (pieceMaterial != null)
                {
                    var shared = new Material[r.sharedMaterials.Length];
                    for (var i = 0; i < shared.Length; i++) shared[i] = pieceMaterial;
                    r.sharedMaterials = shared;
                }
                fades[r] = new Fade { original = r.sharedMaterials };
            }
        }

        /// <summary>
        /// 카메라 기준으로 플레이어를 가린 벽 조각 · 주차장(WallFadeTarget)을 부드럽게 반투명하게 (2026-11-19, 값은 WallFadeConfig).
        /// `CheckInterval`마다 가림을 다시 재고(카메라 → 플레이어 확인 높이 선이 경계 상자를 지나면 가림), 알파는 매 프레임 목표로 움직인다.
        /// 흐려지기 시작하면 반투명 재질로 바꾸고 MaterialPropertyBlock 알파를 보간, 다 돌아오면 원래 재질 · 블록을 되돌린다.
        /// </summary>
        void LateUpdate()
        {
            var config = GameManager.Instance != null ? GameManager.Instance.Database.WallFade : null;
            var on = config != null && config.Enabled;
            var cam = Camera.main;
            if (on && cam != null && Time.unscaledTime >= nextFadeCheck)
            {
                nextFadeCheck = Time.unscaledTime + config.CheckInterval;
                if (player == null)
                {
                    var mover = FindFirstObjectByType<Characters.PlayerMover>();
                    if (mover != null) player = mover.transform;
                }
                foreach (var target in WallFadeTarget.All)
                {
                    foreach (var r in target.Renderers)
                    {
                        if (r != null && !fades.ContainsKey(r)) fades[r] = new Fade { original = r.sharedMaterials };
                    }
                }
                if (player != null) CheckOcclusion(config, cam.transform.position);
            }

            var dt = Time.unscaledDeltaTime;
            stale.Clear();
            foreach (var pair in fades)
            {
                var r = pair.Key;
                var fade = pair.Value;
                if (r == null)
                {
                    stale.Add(r);
                    continue;
                }
                var hidden = on && fade.hidden;
                var goal = hidden ? config.FadedAlpha : 1f;
                if (Mathf.Approximately(fade.alpha, goal) && fade.swapped == hidden) continue;

                var duration = config == null ? 0.01f : hidden ? config.FadeOutDuration : config.FadeInDuration;
                var speed = (1f - (config != null ? config.FadedAlpha : 0f)) / Mathf.Max(0.01f, duration);
                fade.alpha = Mathf.MoveTowards(fade.alpha, goal, speed * dt);
                Apply(r, fade, config);
            }
            foreach (var r in stale) fades.Remove(r);
        }

        void CheckOcclusion(Data.WallFadeConfig config, Vector3 from)
        {
            var heights = config.CheckHeights;
            var margin = new Vector3(config.HorizontalMargin, config.VerticalMargin, config.HorizontalMargin) * 2f;
            foreach (var pair in fades)
            {
                var r = pair.Key;
                if (r == null) continue;
                var bounds = r.bounds;
                bounds.Expand(margin);
                var hide = false;
                for (var i = 0; i < heights.Length && !hide; i++) hide = Blocks(bounds, from, player.position + Vector3.up * heights[i], config.IgnoreNear);
                pair.Value.hidden = hide;
            }
        }

        void Apply(Renderer r, Fade fade, Data.WallFadeConfig config)
        {
            var opaque = fade.alpha >= 0.999f;
            if (opaque)
            {
                if (!fade.swapped) return;
                r.sharedMaterials = fade.original;
                r.SetPropertyBlock(null);
                fade.swapped = false;
                fade.alpha = 1f;
                return;
            }
            if (!fade.swapped && config != null)
            {
                var mats = new Material[fade.original.Length];
                for (var i = 0; i < mats.Length; i++) mats[i] = config.FadeMaterial;
                r.sharedMaterials = mats;
                fade.swapped = true;
            }
            if (config == null) return;
            block ??= new MaterialPropertyBlock();
            var color = config.FadeMaterial.GetColor(BaseColorId);
            color.a = fade.alpha;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);
        }

        static bool Blocks(Bounds bounds, Vector3 from, Vector3 to, float ignoreNear)
        {
            var dir = to - from;
            var length = dir.magnitude;
            if (length < 0.01f) return false;
            return bounds.IntersectRay(new Ray(from, dir / length), out var distance) && distance < length - ignoreNear;
        }
    }
}
