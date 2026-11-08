using APC.Site;
using UnityEditor;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// n × m 칸 모듈 프리팹의 빈 틀을 만든다: SiteModule + 바닥(n × m 칸 크기) + Content(내용물을 넣을 자리).
    /// 모듈은 원점에서 만들므로, 내용물 위치는 모듈 중심 기준 좌표로 생각하면 된다.
    /// </summary>
    public static class ModuleFactory
    {
        public const string ModuleFolder = "Assets/Prefabs/Modules";

        /// <summary>씬에 임시로 빈 모듈을 만든다 (저장은 Save).</summary>
        public static GameObject CreateEmpty(string name, Vector2Int size, float cellSize)
        {
            var root = new GameObject(name);
            root.AddComponent<SiteModule>().Configure(size, cellSize);

            var width = size.x * cellSize;
            var depth = size.y * cellSize;
            // 윗면이 y≈0에 오도록 얇은 바닥 (충돌 있음 — 플레이어가 걸어 다니는 면)
            BuildKit.Cube("Floor", root.transform, new Vector3(0f, -0.024f, 0f), new Vector3(width - 0.1f, 0.05f, depth - 0.1f), BuildKit.FloorOpen, keepCollider: true);

            var content = new GameObject("Content");
            content.transform.SetParent(root.transform, false);
            return root;
        }

        public static Transform Content(GameObject module) => module.transform.Find("Content");

        /// <summary>프리팹으로 저장하고 씬의 임시 오브젝트는 지운다.</summary>
        public static SiteModule Save(GameObject root, string assetName)
        {
            EnsureFolder();
            var path = $"{ModuleFolder}/{assetName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<SiteModule>(path);
        }

        public static SiteModule Load(string assetName) => AssetDatabase.LoadAssetAtPath<SiteModule>($"{ModuleFolder}/{assetName}.prefab");

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(ModuleFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Modules");
        }
    }

    /// <summary>
    /// 메뉴 APC > Module > New Module — 디자이너가 새 모듈 프리팹(빈 틀)을 만든다.
    /// 만든 뒤 프리팹을 열어 Content 아래에 존 · 시설을 넣고, SiteLayout(config)에 배치 · 해금 조건을 추가한다.
    /// </summary>
    public sealed class ModuleCreatorWindow : EditorWindow
    {
        string moduleName = "NewModule";
        Vector2Int size = Vector2Int.one;

        [MenuItem("APC/Module/New Module...")]
        static void Open() => GetWindow<ModuleCreatorWindow>(true, "새 모듈 만들기").minSize = new Vector2(320f, 170f);

        void OnGUI()
        {
            var cellSize = BuildKit.Database.Site.CellSize;
            EditorGUILayout.HelpBox($"칸 한 변 {cellSize}m (SiteLayout). 모듈은 {ModuleFactory.ModuleFolder}/Module_이름.prefab 으로 저장됩니다.", MessageType.Info);
            moduleName = EditorGUILayout.TextField("이름", moduleName);
            size = EditorGUILayout.Vector2IntField("크기 (가로 열 × 세로 행)", size);
            size = new Vector2Int(Mathf.Clamp(size.x, 1, 8), Mathf.Clamp(size.y, 1, 8));

            var assetName = $"Module_{moduleName}";
            var exists = ModuleFactory.Load(assetName) != null;
            if (exists) EditorGUILayout.HelpBox("같은 이름의 모듈이 이미 있습니다.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(moduleName) || exists))
            {
                if (!GUILayout.Button("만들기")) return;
            }

            var module = ModuleFactory.Save(ModuleFactory.CreateEmpty(assetName, size, cellSize), assetName);
            EditorGUIUtility.PingObject(module);
            Selection.activeObject = module;
            Debug.Log($"[Module] {assetName} ({size.x}×{size.y}) 생성 — 프리팹을 열어 Content에 내용을 넣고 SiteLayout에 배치를 추가하세요.");
        }
    }
}
