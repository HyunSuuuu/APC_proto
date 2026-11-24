using UnityEditor;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// 외곽선 재질(APC/OutlineStencil · APC/Toon) 점검. 그룹 255(관찰)만 OutlineOuter 패스를 켜고 나머지는 끈다.
    /// 일반 그룹이 127 이상이거나 16의 배수(렌더러를 Deferred로 바꾸면 바닥 스텐실과 겹침)면 경고한다.
    /// </summary>
    public static class OutlineValidator
    {
        static readonly string[] ShaderPaths = { "Assets/Shaders/Outline/APC_OutlineStencil.shader", "Assets/Shaders/Toon/APC_Toon.shader" };
        const string OuterPass = "OutlineOuter";

        [MenuItem("APC/Art/Validate Outline Materials")]
        static void Validate()
        {
            var shaders = System.Array.ConvertAll(ShaderPaths, AssetDatabase.LoadAssetAtPath<Shader>);
            int found = 0, fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || System.Array.IndexOf(shaders, material.shader) < 0) continue;
                found++;
                var group = Mathf.RoundToInt(material.GetFloat("_StencilGroup"));
                var observed = group == 255;
                if (material.GetShaderPassEnabled(OuterPass) != observed)
                {
                    material.SetShaderPassEnabled(OuterPass, observed);
                    EditorUtility.SetDirty(material);
                    fixedCount++;
                }
                if (!observed && (group >= 127 || group % 16 == 0))
                    Debug.LogWarning($"[Outline] {path}: 그룹 {group} — 일반 그룹은 1~126, 16의 배수 금지 (관찰은 255)", material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Outline] 외곽선 재질 {found}개 점검, OutlineOuter 패스 {fixedCount}개 고침");
        }
    }
}
