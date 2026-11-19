using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 벽 컷아웃. 이 오브젝트(플레이어)와 WallCutoutTarget(주차한 트럭 등, 2026-11-20)을 가리는 벽에 원형 구멍을 뚫는다 — 최대 8개.
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
        [SerializeField, Min(0f), Tooltip("플레이어 구멍 반지름 (m). 다른 대상은 WallCutoutTarget마다")] float radius = 1.5f;
        [SerializeField, Min(0f), Tooltip("가장자리 디더 폭 (m)")] float softness = 0.4f;
        [SerializeField, Tooltip("구멍 중심 높이 (발밑에서 m)")] float height = 1f;
        [SerializeField, Min(0f), Tooltip("플레이어보다 이만큼(m) 카메라 쪽에 있는 벽부터 뚫는다")] float depthBias = 0.3f;
        [SerializeField, Min(0.1f), Tooltip("새로 생긴 벽 찾는 간격 (초). SiteWalls는 해금마다 벽을 다시 세운다")] float scanInterval = 0.5f;

        readonly Dictionary<Renderer, Material[]> originals = new();
        readonly Dictionary<Material, Material> copies = new();
        float nextScan;

        void LateUpdate()
        {
            // [0] = 플레이어, 나머지 = WallCutoutTarget (주차한 트럭 등, 넘치면 앞에서부터)
            var center = transform.position + Vector3.up * height;
            centers[0] = new Vector4(center.x, center.y, center.z, radius);
            var count = 1;
            foreach (var target in WallCutoutTarget.All)
            {
                if (count >= MaxTargets) break;
                var p = target.Center;
                centers[count++] = new Vector4(p.x, p.y, p.z, target.Radius);
            }
            Shader.SetGlobalVectorArray(CentersId, centers);
            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalVector(ParamsId, new Vector4(0f, softness, depthBias, 0f));

            if (Time.time < nextScan || cutoutMaterial == null) return;
            nextScan = Time.time + scanInterval;

            // shortcut: 0.5초마다 씬 렌더러 전부를 훑는다, 렌더러가 수천 개로 늘어 버벅이면 벽 생성 시점에 등록하는 방식으로 바꾼다
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
