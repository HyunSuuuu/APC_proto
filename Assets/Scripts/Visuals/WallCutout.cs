using System;
using System.Collections.Generic;
using APC.Core;
using APC.Site;
using R3;
using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 벽 컷아웃. 이 오브젝트(플레이어)를 가리는 벽에 원형 구멍을 뚫는다. 셰이더는 최대 8개 배열을 받지만 지금은 [0] 플레이어만 쓴다 (트럭 대상 2026-11-23 삭제).
    /// 플레이어에 붙인다. 매 프레임 위치 · 반지름을 전역 셰이더 배열로 넘기고 (Assets/Shaders/WallCutout.hlsl),
    /// Play 중에만 이름이 structurePrefixes로 시작하는 렌더러(또는 그 부모)와 extraWalls 아래 렌더러의 재질을
    /// "원래 재질 사본 + APC/WallCutout 셰이더"로 바꾼다 — 색 · 텍스처는 원래 재질 그대로.
    /// 끄면 원래 재질로 돌린다. 에셋 · 프리팹 · 기존 스크립트는 바꾸지 않는다.
    /// </summary>
    public sealed class WallCutout : MonoBehaviour
    {
        const string LitShaderName = "Universal Render Pipeline/Lit";
        const int MaxTargets = 8; // WallCutout.hlsl WALL_CUTOUT_MAX와 같아야 한다
        static readonly int CentersId = Shader.PropertyToID("_WallCutoutCenters");
        static readonly int CountId = Shader.PropertyToID("_WallCutoutCount");
        static readonly int ParamsId = Shader.PropertyToID("_WallCutoutParams");
        readonly Vector4[] centers = new Vector4[MaxTargets];

        [SerializeField, Tooltip("M_WallCutout — 셰이더(APC/WallCutout)만 쓴다. 빌드에 셰이더가 들어가도록 참조로 둔다")] Material cutoutMaterial;
        // 구조물 · 배경만 뚫는다. 선반 · 상자 · 팔레트 · 트럭 · 책상 같은 필드 위 작업 물건, 캐릭터, 바닥(floor_ · crosswalk_)은 넣지 않는다
        [SerializeField, Tooltip("이름이 이것으로 시작하면 구조물로 본다 (대소문자 무시). 렌더러 자신 또는 바로 위 부모 이름")]
        string[] structurePrefixes =
        {
            "wall_", "pillar_", "window_", "blind", "DoorPost", "DoorLintel", // 벽 · 기둥 · 창문 · 문틀
            "parking", "truck_parking",                                        // 주차장 구조물
            "street_lamp", "tree_", "sign_", "background",                     // 배경
            "ConstructionFence",                                               // 잠긴 칸 울타리
        };
        [SerializeField, Tooltip("이름과 상관없이 구멍을 뚫을 벽. 아래 렌더러 전부가 바뀐다")] Transform[] extraWalls;
        [SerializeField, Min(0f), Tooltip("플레이어 구멍 반지름 (m)")] float radius = 1.5f;
        [SerializeField, Min(0f), Tooltip("가장자리 디더 폭 (m)")] float softness = 0.4f;
        [SerializeField, Tooltip("구멍 중심 높이 (발밑에서 m)")] float height = 1f;
        [SerializeField, Min(0f), Tooltip("플레이어보다 이만큼(m) 카메라 쪽에 있는 벽부터 뚫는다")] float depthBias = 0.3f;

        readonly Dictionary<Renderer, Material[]> originals = new();
        readonly Dictionary<Material, Material> copies = new();
        readonly List<Renderer> stale = new();
        readonly System.Diagnostics.Stopwatch scanWatch = new();
        bool dirty; // 부지가 바뀜 → 이번 프레임 끝에 한 번 벽을 찾는다

        // 성능 측정용 (PerformanceHUD가 읽는다): 벽 찾기 1회 시간 · 지금까지 최대 · 횟수 · 바꾼 렌더러 수
        public static float LastScanMs { get; private set; }
        public static float MaxScanMs { get; private set; }
        public static int ScanCount { get; private set; }
        public static double TotalScanMs { get; private set; }
        public static int SwappedCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStats()
        {
            LastScanMs = MaxScanMs = ScanCount = SwappedCount = 0;
            TotalScanMs = 0;
        }

        void OnEnable() => dirty = true;

        void Start()
        {
            // 구조물은 해금(벽 다시 세움 · 모듈) · 모듈 이동 · 레벨업(공사 울타리) 때만 생긴다 — 모두 그 프레임 안에서 만들어지므로 LateUpdate에서 찾으면 된다
            var grid = SiteGrid.Instance;
            if (grid != null)
            {
                grid.OnUnlocked.Subscribe(_ => dirty = true).AddTo(this);
                grid.OnMoved.Subscribe(_ => dirty = true).AddTo(this);
            }
            if (GameManager.Instance != null) GameManager.Instance.Level.Level.Subscribe(_ => dirty = true).AddTo(this);
        }

        void LateUpdate()
        {
            // [0] = 플레이어만 (트럭 대상은 2026-11-23 삭제, 셰이더 배열은 그대로)
            var center = transform.position + Vector3.up * height;
            centers[0] = new Vector4(center.x, center.y, center.z, radius);
            Shader.SetGlobalVectorArray(CentersId, centers);
            Shader.SetGlobalFloat(CountId, 1);
            Shader.SetGlobalVector(ParamsId, new Vector4(0f, softness, depthBias, 0f));

            // 예전엔 0.5초마다 훑었다 — 실기기 TycoonTest에서 1회 12.7ms · GC 절반이라 바꿈 (2026-11-20)
            if (!dirty || cutoutMaterial == null) return;
            dirty = false;

            scanWatch.Restart();
            Scan();
            scanWatch.Stop();
            LastScanMs = (float)scanWatch.Elapsed.TotalMilliseconds;
            MaxScanMs = Mathf.Max(MaxScanMs, LastScanMs);
            ScanCount++;
            TotalScanMs += LastScanMs;
            SwappedCount = originals.Count;
        }

        void Scan()
        {
            // 해금 때 지워진 옛 벽 조각은 목록에서 뺀다
            stale.Clear();
            foreach (var r in originals.Keys)
                if (r == null) stale.Add(r);
            foreach (var r in stale) originals.Remove(r);

            // 부지가 바뀔 때만 불리므로 씬 전체를 훑어도 된다 (타이쿤 시험 실기기 약 7ms, 해금 프레임에 한 번)
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (!originals.ContainsKey(r) && IsWall(r.transform)) Swap(r);
            if (extraWalls == null) return;
            foreach (var root in extraWalls)
                if (root != null)
                    foreach (var r in root.GetComponentsInChildren<Renderer>())
                        if (!originals.ContainsKey(r)) Swap(r);
        }

        bool IsWall(Transform t) => HasWallName(t.name) || (t.parent != null && HasWallName(t.parent.name));

        bool HasWallName(string objectName)
        {
            foreach (var prefix in structurePrefixes)
                if (!string.IsNullOrEmpty(prefix) && objectName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void Swap(Renderer r)
        {
            var shared = r.sharedMaterials;
            originals[r] = shared;
            var swapped = new Material[shared.Length];
            for (var i = 0; i < shared.Length; i++) swapped[i] = CutoutCopy(shared[i]);
            r.sharedMaterials = swapped;
        }

        /// <summary>URP Lit 재질만 사본을 만들어 셰이더를 바꾼다 (속성 이름이 같아 색 · 텍스처가 그대로 옮겨진다). 그 밖의 셰이더는 그대로 둔다.</summary>
        Material CutoutCopy(Material original)
        {
            if (original == null || original.shader == null || original.shader.name != LitShaderName) return original;
            if (copies.TryGetValue(original, out var copy)) return copy;
            copy = new Material(original) { name = original.name + " (Cutout)", shader = cutoutMaterial.shader };
            copies[original] = copy;
            return copy;
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(CountId, 0f); // 대상 0개 → 셰이더가 아무것도 안 버림
            foreach (var (r, materials) in originals)
                if (r != null) r.sharedMaterials = materials;
            originals.Clear();
            foreach (var copy in copies.Values) Destroy(copy);
            copies.Clear();
        }
    }
}
