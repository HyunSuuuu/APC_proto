using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace APC.Visuals
{
    /// <summary>
    /// 캐릭터 툰 + 검정 외곽선. 캐릭터 루트(플레이어 · 알바)에 붙인다.
    /// 켜지면 자식 SkinnedMeshRenderer(꺼진 코스튬 포함)의 URP Lit 재질을 "원래 재질 사본 + APC/Toon 셰이더"로 바꾼다.
    /// 외곽선은 같은 셰이더의 Outline* 패스라 슬롯을 따로 붙이지 않는다 (서브메시마다 선이 생긴다).
    /// 끄면 원래 재질 배열로 돌린다. 에셋 · 프리팹 · 기존 스크립트는 바꾸지 않는다 (WallCutout과 같은 방식).
    /// 들고 있는 상자 같은 MeshRenderer는 건드리지 않는다.
    /// </summary>
    public sealed class CharacterToon : MonoBehaviour
    {
        const string LitShaderName = "Universal Render Pipeline/Lit";

        [SerializeField, Tooltip("M_Toon — 셰이더(APC/Toon)와 Lit에 없는 값(그림자 · 외곽선)을 가져온다. 빌드에 셰이더가 들어가도록 참조로 둔다")] Material toonMaterial;
        [SerializeField, Tooltip("끄면 외곽선 패스(OutlineMask · OutlineHull)를 끈다")] bool outline = true;

        readonly Dictionary<Renderer, Material[]> originals = new();
        readonly Dictionary<Material, Material> copies = new();

        void OnEnable()
        {
            if (toonMaterial == null) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var shared = r.sharedMaterials;
                originals[r] = shared;
                var swapped = new Material[shared.Length];
                for (var i = 0; i < shared.Length; i++) swapped[i] = ToonCopy(shared[i]);
                r.sharedMaterials = swapped;
            }
        }

        /// <summary>URP Lit 재질만 사본을 만들어 셰이더를 바꾼다 (_BaseMap · _BaseColor 이름이 같아 그대로 옮겨진다).</summary>
        Material ToonCopy(Material original)
        {
            if (original == null || original.shader == null || original.shader.name != LitShaderName) return original;
            if (copies.TryGetValue(original, out var copy)) return copy;
            copy = new Material(original) { name = original.name + " (Toon)", shader = toonMaterial.shader };
            // Lit엔 없는 값(그림자 · 외곽선)은 M_Toon에서 가져온다
            var shader = toonMaterial.shader;
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                var id = shader.GetPropertyNameId(i);
                if (original.HasProperty(id)) continue;
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: copy.SetColor(id, toonMaterial.GetColor(id)); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: copy.SetFloat(id, toonMaterial.GetFloat(id)); break;
                }
            }
            copy.SetShaderPassEnabled("OutlineOuter", false); // 관찰 때만 ObserveOutline이 켠다
            copy.SetShaderPassEnabled("OutlineMask", outline);
            copy.SetShaderPassEnabled("OutlineHull", outline);
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
