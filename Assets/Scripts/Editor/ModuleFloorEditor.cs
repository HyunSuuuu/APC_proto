using System.Collections.Generic;
using System.Text.RegularExpressions;
using APC.Site;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// 모듈 바닥 타일 칠하기 (M14-2). 모듈 프리팹을 열고 루트(ModuleFloor)를 고르면 인스펙터에
    /// 팔레트(`Art/Model/Prop`의 floor · road · crosswalk fbx)와 위에서 본 칸 격자가 나온다.
    /// 왼쪽 클릭 · 끌기 = 칠하기, 오른쪽 클릭 = 90° 회전. 바뀔 때마다 `FloorArt` 아래 타일을 다시 깐다.
    /// 타일 fbx는 import Scale Factor 2(1m → 2m)로 맞춘다.
    /// </summary>
    [CustomEditor(typeof(ModuleFloor))]
    public sealed class ModuleFloorEditor : Editor
    {
        const string PropFolder = "Assets/Art/Model/Prop";
        const string PropMaterialPath = "Assets/Art/Materials/M_Prop.mat";
        const float TileScaleFactor = 2f;
        const float CellPixels = 44f;

        static readonly List<GameObject> Palette = new();
        static GameObject brush;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var floor = (ModuleFloor)target;
            if (Palette.Count == 0) LoadPalette();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("팔레트 (붓)", EditorStyles.boldLabel);
            DrawPalette();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("바닥 (위에서 본 모습 · 위 = 로컬 +Z)  왼쪽 = 칠하기 · 끌기, 오른쪽 = 90° 회전", EditorStyles.miniBoldLabel);
            DrawGrid(floor);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("붓으로 전체 채우기")) Fill(floor, false);
                if (GUILayout.Button("같은 종류 무작위 섞기")) Fill(floor, true);
                if (GUILayout.Button("다시 깔기")) Rebuild(floor);
            }
        }

        void DrawPalette()
        {
            const int perRow = 6;
            for (var i = -1; i < Palette.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var j = i; j < Mathf.Min(i + perRow, Palette.Count); j++)
                    {
                        var tile = j < 0 ? null : Palette[j];
                        var content = tile == null ? new GUIContent("지우개") : new GUIContent(Preview(tile), tile.name);
                        var style = new GUIStyle(GUI.skin.button) { fixedWidth = 52, fixedHeight = 52, imagePosition = ImagePosition.ImageOnly };
                        if (tile == null) style.imagePosition = ImagePosition.TextOnly;
                        var old = GUI.backgroundColor;
                        if (brush == tile) GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
                        if (GUILayout.Button(content, style)) brush = tile;
                        GUI.backgroundColor = old;
                    }
                }
            }
            EditorGUILayout.LabelField("붓: " + (brush != null ? brush.name : "지우개"), EditorStyles.miniLabel);
        }

        void DrawGrid(ModuleFloor floor)
        {
            var grid = floor.GridSize;
            var e = Event.current;
            for (var z = grid.y - 1; z >= 0; z--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var x = 0; x < grid.x; x++)
                    {
                        var rect = GUILayoutUtility.GetRect(CellPixels, CellPixels, GUILayout.Width(CellPixels), GUILayout.Height(CellPixels));
                        var tile = floor.GetTile(x, z);
                        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                        if (tile != null)
                        {
                            var matrix = GUI.matrix;
                            GUIUtility.RotateAroundPivot(90f * floor.GetTurn(x, z), rect.center);
                            var preview = Preview(tile);
                            if (preview != null) GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), preview, ScaleMode.ScaleToFit);
                            GUI.matrix = matrix;
                        }
                        if (!rect.Contains(e.mousePosition)) continue;
                        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                        {
                            if (floor.GetTile(x, z) != brush) Paint(floor, x, z, brush, floor.GetTurn(x, z));
                            e.Use();
                        }
                        else if (e.type == EventType.MouseDown && e.button == 1 && tile != null)
                        {
                            Paint(floor, x, z, tile, floor.GetTurn(x, z) + 1);
                            e.Use();
                        }
                    }
                }
            }
            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        static void Paint(ModuleFloor floor, int x, int z, GameObject tile, int turn)
        {
            Undo.RecordObject(floor, "바닥 칠하기");
            floor.Set(x, z, tile, turn);
            Rebuild(floor);
        }

        static void Fill(ModuleFloor floor, bool shuffleFamily)
        {
            var family = new List<GameObject>();
            if (shuffleFamily && brush != null)
            {
                var key = Family(brush.name);
                foreach (var tile in Palette)
                {
                    if (Family(tile.name) == key) family.Add(tile);
                }
            }
            Undo.RecordObject(floor, "바닥 채우기");
            var grid = floor.GridSize;
            for (var z = 0; z < grid.y; z++)
            for (var x = 0; x < grid.x; x++)
            {
                var tile = family.Count > 0 ? family[Random.Range(0, family.Count)] : brush;
                floor.Set(x, z, tile, shuffleFamily ? Random.Range(0, 4) : 0);
            }
            Rebuild(floor);
        }

        /// <summary>이름 끝 숫자를 뗀 것 (floor_basic3 → floor_basic).</summary>
        static string Family(string name) => Regex.Replace(name, @"\d+$", "");

        /// <summary>`FloorArt` 아래 타일을 다시 깐다. 타일 윗면 = y 0, 가운데 = 칸 가운데 (fbx 피벗이 달라도 bounds로 맞춤).</summary>
        public static void Rebuild(ModuleFloor floor)
        {
            var art = floor.transform.Find(ModuleFloor.ArtName);
            if (art != null) Undo.DestroyObjectImmediate(art.gameObject);
            art = new GameObject(ModuleFloor.ArtName).transform;
            Undo.RegisterCreatedObjectUndo(art.gameObject, "바닥 깔기");
            art.SetParent(floor.transform, false);

            var material = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);
            var grid = floor.GridSize;
            var any = false;
            for (var z = 0; z < grid.y; z++)
            for (var x = 0; x < grid.x; x++)
            {
                var tile = floor.GetTile(x, z);
                if (tile == null) continue;
                any = true;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(tile, art);
                go.name = $"{tile.name}_{x}_{z}";
                go.transform.localRotation = Quaternion.Euler(0f, 90f * floor.GetTurn(x, z), 0f) * tile.transform.localRotation;
                go.transform.localPosition = Vector3.zero;
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                var offset = art.InverseTransformVector(b.center - go.transform.position);
                var top = art.InverseTransformPoint(b.max).y;
                go.transform.localPosition = floor.CellCenter(x, z) - new Vector3(offset.x, top, offset.z);
                if (material != null)
                {
                    foreach (var r in renderers)
                    {
                        var shared = new Material[r.sharedMaterials.Length];
                        for (var i = 0; i < shared.Length; i++) shared[i] = material;
                        r.sharedMaterials = shared;
                    }
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }

            // 타일이 있으면 회색 바닥 판은 숨긴다 (충돌체 · 네비메시는 그대로)
            var plate = floor.transform.Find("Floor");
            if (plate != null && plate.TryGetComponent<MeshRenderer>(out var plateRenderer))
            {
                Undo.RecordObject(plateRenderer, "바닥 판");
                plateRenderer.enabled = !any;
            }

            EditorUtility.SetDirty(floor);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
            else if (floor.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);
        }

        static Texture Preview(GameObject tile) => AssetPreview.GetAssetPreview(tile) ?? AssetPreview.GetMiniThumbnail(tile);

        /// <summary>Prop 폴더의 바닥 타일 fbx (floor · road · crosswalk). import Scale Factor가 2가 아니면 맞춘다.</summary>
        static void LoadPalette()
        {
            Palette.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PropFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!name.StartsWith("floor") && !name.Contains("crosswalk") && !name.Contains("road")) continue;
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && !Mathf.Approximately(importer.globalScale, TileScaleFactor))
                {
                    importer.globalScale = TileScaleFactor;
                    importer.SaveAndReimport();
                }
                Palette.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            Palette.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        /// <summary>모든 모듈 프리팹에 ModuleFloor를 붙인다 (이미 있으면 그대로, 타일은 안 칠함).</summary>
        [MenuItem("APC/Art/Add Module Floor (모든 모듈에 바닥 칠하기 붙이기)")]
        static void AddToAllModules()
        {
            var added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ModuleFactory.ModuleFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Test/")) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root.GetComponent<SiteModule>() != null && root.GetComponent<ModuleFloor>() == null)
                {
                    root.AddComponent<ModuleFloor>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[ModuleFloor] {added}개 모듈에 붙임. 모듈 프리팹을 열고 루트를 골라 칠한다.");
        }
    }
}
