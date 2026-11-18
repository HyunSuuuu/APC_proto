using System.Collections.Generic;
using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 캐릭터 툰 + 검정 외곽선 (셰이더 실험). 캐릭터 루트(플레이어 · 알바)에 붙인다.
    /// 켜지면 자식 SkinnedMeshRenderer(꺼진 코스튬 포함)의 URP Lit 재질을 "원래 재질 사본 + 툰 셰이더"로 바꾸고 맨 끝에 외곽선 슬롯을 붙인다.
    /// 끄면 원래 재질 배열로 돌린다. 에셋 · 프리팹 · 기존 스크립트는 바꾸지 않는다 (WallCutout과 같은 방식).
    /// 들고 있는 상자 같은 MeshRenderer는 건드리지 않는다.
    /// </summary>
    public sealed class CharacterToon : MonoBehaviour
    {
        const string LitShaderName = "Universal Render Pipeline/Lit";

        [SerializeField, Tooltip("M_Toon — 셰이더(Shader Graphs/APC_Toon)만 쓴다. 빌드에 셰이더가 들어가도록 참조로 둔다")] Material toonMaterial;
        [SerializeField, Tooltip("M_Outline — 렌더러마다 마지막 슬롯에 붙는다. 비우면 툰만")] Material outlineMaterial;

        readonly Dictionary<Renderer, Material[]> originals = new();
        readonly Dictionary<Material, Material> copies = new();

        void OnEnable()
        {
            if (toonMaterial == null) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var shared = r.sharedMaterials;
                originals[r] = shared;
                var count = outlineMaterial != null ? shared.Length + 1 : shared.Length;
                var swapped = new Material[count];
                for (var i = 0; i < shared.Length; i++) swapped[i] = ToonCopy(shared[i]);
                if (outlineMaterial != null) swapped[^1] = outlineMaterial; // 서브메시가 여럿이면 마지막 서브메시에만 선이 생긴다
                r.sharedMaterials = swapped;
            }
        }

        /// <summary>URP Lit 재질만 사본을 만들어 셰이더를 바꾼다 (_BaseMap · _BaseColor 이름이 같아 그대로 옮겨진다).</summary>
        Material ToonCopy(Material original)
        {
            if (original == null || original.shader == null || original.shader.name != LitShaderName) return original;
            if (copies.TryGetValue(original, out var copy)) return copy;
            copy = new Material(original) { name = original.name + " (Toon)", shader = toonMaterial.shader };
            copies[original] = copy;
            return copy;
        }

        void OnDisable()
        {
            foreach (var (r, materials) in originals)
                if (r != null) r.sharedMaterials = materials;
            originals.Clear();
            foreach (var copy in copies.Values) Destroy(copy);
            copies.Clear();
        }
    }
}
