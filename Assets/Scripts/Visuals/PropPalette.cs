using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 모델 색 팔레트 고르기 (M22). 새 Prop 모델(`Art/Model/Prop`)은 새 팔레트 재질(`M_Prop`),
    /// 아직 새 모델이 없는 옛 모델(`Art/Model/Prop_old`)은 옛 팔레트 재질(`M_Prop_Old`)을 입어야 색이 맞는다.
    /// fbx가 원래 가진 재질의 텍스처가 옛 재질 텍스처와 같으면 옛 모델로 본다.
    /// shortcut: Prop_old 모델이 모두 새 모델로 바뀌면 이 클래스와 legacy 재질을 지운다.
    /// </summary>
    public static class PropPalette
    {
        static readonly int EmbeddedMap = Shader.PropertyToID("_BASE_COLOR_MAP");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        /// <summary>fbx 재질 → 입힐 재질. legacy가 없으면 늘 modern.</summary>
        public static Material Pick(Material embedded, Material modern, Material legacy)
            => legacy != null && embedded != null && embedded.HasProperty(EmbeddedMap)
               && embedded.GetTexture(EmbeddedMap) == legacy.GetTexture(BaseMap)
                ? legacy
                : modern;

        /// <summary>렌더러의 슬롯마다 Pick으로 갈아 끼운다. modern이 없으면 그대로 둔다.</summary>
        public static void Apply(Renderer renderer, Material modern, Material legacy)
        {
            if (modern == null) return;
            var source = renderer.sharedMaterials;
            var materials = new Material[source.Length];
            for (var i = 0; i < materials.Length; i++) materials[i] = Pick(source[i], modern, legacy);
            renderer.sharedMaterials = materials;
        }
    }
}
