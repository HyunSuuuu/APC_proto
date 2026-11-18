using System.Collections.Generic;
using APC.Data;
using UnityEditor;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// 배경 모듈 배치 에디터 (2026-11-19). `SiteLayout.background` 표를 칸 격자로 고친다 — 잔디 · 도로 등을 부지 밖에 깔고 잇기.
    /// 팔레트에서 프리팹을 고르고 칸 왼클릭 = 놓기(같은 프리팹이면 90° 돌리기), 오른클릭 = 지우기. 회색 칸 = 부지(모듈 자리, 못 놓음).
    /// 씬 미리보기: 편집 중 씬에 배경을 그대로 깐다(저장 안 됨 · Play 때 지움 — 실제 배경은 실행 때 SiteBackground가 놓는다).
    /// 메뉴 APC > Art > Background Layout.
    /// </summary>
    public sealed class BackgroundLayoutWindow : EditorWindow
    {
        const string PrefabFolder = "Assets/Prefabs/Modules/Background";
        const string PreviewName = "[BackgroundPreview]";
        const int SiteRows = 4, SiteColumns = 6; // GDD 4.2 6 × 4 (행 1~4 · 열 1~6)
        static readonly int[] Rotations = { 0, 90, 180, 270 };

        SiteLayout layout;
        readonly List<GameObject> palette = new();
        int selected;
        int rotationIndex;
        int margin = 3;
        bool preview = true;
        float cellPixels = 34f;
        Vector2 scroll;
        Vector2 paletteScroll;
        float paletteHeight = 130f;
        GameObject previewRoot;

        [MenuItem("APC/Art/Background Layout (배경 모듈 배치)")]
        static void Open() => GetWindow<BackgroundLayoutWindow>("배경 배치");

        void OnEnable()
        {
            if (layout == null)
            {
                var guids = AssetDatabase.FindAssets("t:SiteLayout");
                if (guids.Length > 0) layout = AssetDatabase.LoadAssetAtPath<SiteLayout>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            RefreshPalette();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RebuildPreview();
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ClearPreview();
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) ClearPreview(); // 실행 중엔 SiteBackground가 깐다
            if (change == PlayModeStateChange.EnteredEditMode) RebuildPreview();
        }

        void RefreshPalette()
        {
            palette.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null) palette.Add(prefab);
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, palette.Count - 1));
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            layout = (SiteLayout)EditorGUILayout.ObjectField("SiteLayout", layout, typeof(SiteLayout), false);
            if (EditorGUI.EndChangeCheck()) RebuildPreview();
            if (layout == null)
            {
                EditorGUILayout.HelpBox("SiteLayout 에셋을 넣으세요.", MessageType.Info);
                return;
            }

            // 팔레트
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"팔레트 ({palette.Count}개, {PrefabFolder})", EditorStyles.boldLabel);
            if (GUILayout.Button("↻ 다시 읽기", GUILayout.Width(80f))) RefreshPalette();
            EditorGUILayout.EndHorizontal();
            // 상자 안 세로 목록 — 프리팹이 많아도 높이는 그대로, 세로로 스크롤 (2026-11-19)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.Height(paletteHeight));
            for (var i = 0; i < palette.Count; i++)
            {
                var label = new GUIContent("  " + palette[i].name, AssetPreview.GetMiniThumbnail(palette[i]));
                var style = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, fixedHeight = 22f };
                var rect = GUILayoutUtility.GetRect(label, style, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), ColorOf(palette[i])); // 격자 칸 색과 같은 띠
                if (GUI.Toggle(new Rect(rect.x + 6f, rect.y, rect.width - 6f, rect.height), selected == i, label, style)) selected = i;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            paletteHeight = EditorGUILayout.Slider("팔레트 높이 (px)", paletteHeight, 60f, 400f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("새로 놓을 회전", GUILayout.Width(90f));
            rotationIndex = GUILayout.Toolbar(rotationIndex, new[] { "0°", "90°", "180°", "270°" });
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            margin = EditorGUILayout.IntSlider("부지 바깥 칸 수", margin, 1, 10);
            cellPixels = EditorGUILayout.Slider("칸 크기 (px)", cellPixels, 22f, 60f);
            preview = EditorGUILayout.Toggle("씬 미리보기", preview);
            if (EditorGUI.EndChangeCheck()) RebuildPreview();
            EditorGUILayout.HelpBox("왼클릭 = 놓기 (같은 프리팹이면 90° 돌리기) · 오른클릭 = 지우기 · 회색 = 부지. 칸 글자 = 프리팹 앞 글자 + 회전 화살표.", MessageType.None);

            DrawGrid();
        }

        void DrawGrid()
        {
            var placements = new Dictionary<Vector2Int, int>(); // (행, 열) → 표 index
            var list = layout.Background;
            for (var i = 0; i < list.Count; i++) placements[new Vector2Int(list[i].row, list[i].column)] = i;

            var minRow = 1 - margin;
            var maxRow = SiteRows + margin;
            var minCol = 1 - margin;
            var maxCol = SiteColumns + margin;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var rect = GUILayoutUtility.GetRect((maxCol - minCol + 2) * cellPixels, (maxRow - minRow + 2) * cellPixels);
            var e = Event.current;
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = false };

            for (var row = minRow; row <= maxRow; row++)
            {
                for (var col = minCol; col <= maxCol; col++)
                {
                    var cell = new Rect(rect.x + (col - minCol + 1) * cellPixels, rect.y + (row - minRow + 1) * cellPixels, cellPixels - 2f, cellPixels - 2f);
                    var site = row >= 1 && row <= SiteRows && col >= 1 && col <= SiteColumns;
                    var key = new Vector2Int(row, col);
                    var has = placements.TryGetValue(key, out var index);

                    var color = site ? new Color(0.35f, 0.35f, 0.35f) : has ? ColorOf(list[index].prefab) : new Color(0.18f, 0.18f, 0.18f);
                    EditorGUI.DrawRect(cell, color);
                    if (has) GUI.Label(cell, Short(list[index].prefab != null ? list[index].prefab.name : "?") + "\n" + Arrow(list[index].rotation), style);
                    else if (site) GUI.Label(cell, $"{row},{col}", style);

                    if (site || e.type != EventType.MouseDown || !cell.Contains(e.mousePosition)) continue;
                    if (e.button == 0 && palette.Count > 0) Place(key, has ? index : -1);
                    else if (e.button == 1 && has) Remove(index);
                    e.Use();
                    GUIUtility.ExitGUI();
                }
            }
            // 행 · 열 번호
            for (var row = minRow; row <= maxRow; row++)
                GUI.Label(new Rect(rect.x, rect.y + (row - minRow + 1) * cellPixels, cellPixels, cellPixels), row.ToString(), style);
            for (var col = minCol; col <= maxCol; col++)
                GUI.Label(new Rect(rect.x + (col - minCol + 1) * cellPixels, rect.y, cellPixels, cellPixels), col.ToString(), style);
            EditorGUILayout.EndScrollView();
        }

        void Place(Vector2Int key, int index)
        {
            var so = new SerializedObject(layout);
            var list = so.FindProperty("background");
            Undo.RecordObject(layout, "배경 놓기");
            var prefab = palette[selected];
            SerializedProperty element;
            if (index >= 0)
            {
                element = list.GetArrayElementAtIndex(index);
                var rot = element.FindPropertyRelative("rotation");
                if (element.FindPropertyRelative("prefab").objectReferenceValue == prefab) rot.intValue = (rot.intValue + 90) % 360; // 같은 프리팹 = 돌리기
                else
                {
                    element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    rot.intValue = Rotations[rotationIndex];
                }
            }
            else
            {
                list.arraySize++;
                element = list.GetArrayElementAtIndex(list.arraySize - 1);
                element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                element.FindPropertyRelative("row").intValue = key.x;
                element.FindPropertyRelative("column").intValue = key.y;
                element.FindPropertyRelative("rotation").intValue = Rotations[rotationIndex];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            RebuildPreview();
        }

        void Remove(int index)
        {
            var so = new SerializedObject(layout);
            Undo.RecordObject(layout, "배경 지우기");
            so.FindProperty("background").DeleteArrayElementAtIndex(index);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            RebuildPreview();
        }

        void RebuildPreview()
        {
            ClearPreview();
            if (!preview || layout == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            previewRoot = new GameObject(PreviewName) { hideFlags = HideFlags.DontSave };
            foreach (var placement in layout.Background)
            {
                if (placement.prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(placement.prefab);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(previewRoot.transform, false);
                go.transform.SetPositionAndRotation(layout.CellCenter(placement.row, placement.column), Quaternion.Euler(0f, placement.rotation, 0f));
            }
            SceneView.RepaintAll();
        }

        void ClearPreview()
        {
            if (previewRoot != null) DestroyImmediate(previewRoot);
            previewRoot = null;
            // 창을 다시 열거나 스크립트가 다시 불렸을 때 남은 미리보기
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == PreviewName && go.scene.IsValid() && go.transform.parent == null) DestroyImmediate(go);
            }
        }

        static string Short(string name) => name.Replace("Module_Bg", "").Replace("Module_", "");

        static string Arrow(int rotation) => (((rotation % 360) + 360) % 360) switch
        {
            90 => "→",
            180 => "↓",
            270 => "←",
            _ => "↑",
        };

        static Color ColorOf(GameObject prefab)
        {
            if (prefab == null) return Color.magenta;
            var hash = prefab.name.GetHashCode();
            return Color.HSVToRGB(Mathf.Abs(hash % 360) / 360f, 0.45f, 0.55f);
        }
    }
}
