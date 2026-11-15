using System.Text;
using UnityEditor;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// Prop fbx(`Assets/Art/Model/Prop`)로 모듈 겉모습을 만드는 시험 도구.
    /// fbx마다 import 스케일이 달라서, 메시 실제 크기(렌더러 bounds)를 재서 바닥 타일 1장 = 1m × 1m로 맞춘다.
    /// 모듈 1칸 = 10m = 타일 10 × 10. 로직(존 · 선반 자리)은 없고, 바닥 충돌체는 ModuleFactory 그대로(렌더러만 끔).
    /// </summary>
    public static class ArtModuleBuilder
    {
        const string PropFolder = "Assets/Art/Model/Prop";
        const string TestFolder = "Assets/Prefabs/Modules/Test";
        const string PropMaterialPath = "Assets/Art/Materials/M_Prop.mat";
        const float TileSize = 1f;
        const int TilesPerCell = 10;

        [MenuItem("APC/Art/Build Test Module (빈 모듈 · 바닥 타일)")]
        static void BuildTestModule()
        {
            var tile = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/floor1_basic.fbx");
            if (tile == null)
            {
                Debug.LogError($"[ArtModuleBuilder] {PropFolder}/floor1_basic.fbx 가 없습니다.");
                return;
            }

            var root = ModuleFactory.CreateEmpty("Module_ArtTest", Vector2Int.one, TilesPerCell * TileSize);
            root.transform.Find("Floor").GetComponent<MeshRenderer>().enabled = false; // 충돌체만 남긴다 (걷는 면 · 네비메시)

            var art = new GameObject("Art").transform;
            art.SetParent(root.transform, false);

            var fit = MeasureFit(tile, out var size);
            var material = PropMaterial();
            var half = (TilesPerCell - 1) * 0.5f * TileSize;
            for (var z = 0; z < TilesPerCell; z++)
            for (var x = 0; x < TilesPerCell; x++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(tile, art);
                go.name = $"Floor_{x}_{z}";
                go.transform.localScale = Vector3.Scale(go.transform.localScale, fit.scale);
                go.transform.localPosition = new Vector3(x * TileSize - half, 0f, z * TileSize - half) + fit.offset;
                ApplyMaterial(go, material);
            }

            EnsureFolder();
            var path = $"{TestFolder}/Module_ArtTest.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[ArtModuleBuilder] {path} 생성. floor1_basic 원래 크기 {size.x:0.###} × {size.y:0.###} × {size.z:0.###} m → 배율 {fit.scale.x:0.###} · {fit.scale.y:0.###} · {fit.scale.z:0.###}");
        }

        /// <summary>Prop 폴더 fbx마다 실제 크기(m)를 콘솔에 찍는다. import 스케일을 통일할 때 기준으로 쓴다.</summary>
        [MenuItem("APC/Art/Log Prop Sizes (fbx 실제 크기)")]
        static void LogPropSizes()
        {
            var log = new StringBuilder("[ArtModuleBuilder] Prop 크기 (x × y × z m, import 스케일 적용 후)\n");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PropFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var size = Bounds(model).size;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                log.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path),-24} {size.x,7:0.###} × {size.y,7:0.###} × {size.z,7:0.###}   (globalScale {importer?.globalScale})");
            }
            Debug.Log(log.ToString());
        }

        /// <summary>바닥 타일이 TileSize × TileSize, 윗면이 y = 0, 가운데가 원점에 오도록 하는 배율 · 위치 보정.</summary>
        static (Vector3 scale, Vector3 offset) MeasureFit(GameObject model, out Vector3 size)
        {
            var b = Bounds(model);
            size = b.size;
            var sx = size.x > 1e-4f ? TileSize / size.x : 1f;
            var sz = size.z > 1e-4f ? TileSize / size.z : 1f;
            var scale = new Vector3(sx, (sx + sz) * 0.5f, sz);
            var offset = new Vector3(-b.center.x * sx, -b.max.y * scale.y, -b.center.z * sz);
            return (scale, offset);
        }

        /// <summary>모델을 원점에 잠시 꺼내 렌더러 bounds를 잰다 (월드 축 기준이라 fbx 회전이 반영됨).</summary>
        static Bounds Bounds(GameObject model)
        {
            var go = Object.Instantiate(model);
            go.transform.SetPositionAndRotation(Vector3.zero, model.transform.rotation);
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(go);
            return b;
        }

        /// <summary>공용 텍스처 `Color_Pallet.png`를 쓰는 URP Lit 재질 (Prop 전체 공용).</summary>
        static Material PropMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PropFolder}/Color_Pallet.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, PropMaterialPath);
            return material;
        }

        static void ApplyMaterial(GameObject go, Material material)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var shared = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < shared.Length; i++) shared[i] = material;
                r.sharedMaterials = shared;
            }
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder)) AssetDatabase.CreateFolder(ModuleFactory.ModuleFolder, "Test");
        }
    }
}
