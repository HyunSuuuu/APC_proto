using System.Collections.Generic;
using APC.Data;
using APC.Site;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace APC.EditorTools
{
    /// <summary>
    /// 타이쿤 시험 (2026-11-20): Game 씬 · 데이터를 복사해 8 × 8 부지를 만든다. 원본 씬 · 에셋은 건드리지 않는다.
    /// 왼쪽 열 = 하차장 8 (트럭은 서쪽), 오른쪽 열 = 상차장 8 (트럭은 동쪽), 가운데 6열 = 사무실 1 + 보관 A~D (대각선으로 섞음).
    /// 데이터 복사본(Assets/Data/Tycoon): 배치 · 트럭 간격 · 최대 알바 · 카메라 줌만 바꾼다. 알바 대량 생성은 씬의 TycoonTest.
    /// </summary>
    public static class TycoonTestBuilder
    {
        const int Size = 8;
        const string Folder = "Assets/Data/Tycoon";
        const string ScenePath = "Assets/Scenes/TycoonTest.unity";

        [MenuItem("APC/Test/Build Tycoon 8x8 (TycoonTest 씬 덮어쓰기)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Tycoon");

            var database = Copy<GameDatabase>("GameDatabase");
            var layout = Copy<SiteLayout>("SiteLayout");
            var logistics = Copy<LogisticsConfig>("LogisticsConfig");
            var levels = Copy<LevelTable>("LevelTable");
            var controls = Copy<ControlsConfig>("ControlsConfig");

            var db = new SerializedObject(database);
            db.FindProperty("site").objectReferenceValue = layout;
            db.FindProperty("logistics").objectReferenceValue = logistics;
            db.FindProperty("levels").objectReferenceValue = levels;
            db.FindProperty("controls").objectReferenceValue = controls;
            db.ApplyModifiedPropertiesWithoutUndo();

            Set(logistics, so =>
            {
                so.FindProperty("inboundArrivalInterval").floatValue = 2f; // 하차장 8칸이 놀지 않게 (원본 20초)
                so.FindProperty("inboundQueueSize").intValue = 30; // 하차 · 상차 동시라 알바가 쉬지 않게 줄을 길게
            });
            Set(levels, so =>
            {
                var list = so.FindProperty("levels");
                for (var i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).FindPropertyRelative("maxStaff").intValue = 999;
                so.FindProperty("overflowMaxStaff").intValue = 999;
            });
            Set(controls, so => so.FindProperty("maxDistance").floatValue = 120f); // 부지 전체가 보이게
            var placements = Placements();
            Set(layout, so => WriteLayout(so, placements));
            AssetDatabase.SaveAssets();

            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset("Assets/Scenes/Game.unity", ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            // 씬 복사 · 열기 뒤엔 위에서 받은 에셋 참조가 끊길 수 있어 다시 읽는다 (안 그러면 GameManager.database가 비어 저장됨)
            BuildScene(Copy<GameDatabase>("GameDatabase"), Copy<SiteLayout>("SiteLayout"), placements);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[TycoonTestBuilder] {ScenePath} 생성 — 모듈 {placements.Count}개. Play하면 알바가 생성된다 (TycoonTest).");
        }

        static List<(string id, string asset, int row, int column, int rotation)> Placements()
        {
            var list = new List<(string, string, int, int, int)>();
            var storeDNamed = false;
            for (var r = 1; r <= Size; r++)
            {
                list.Add(($"In{r}", "Module_LotInbound", r, 1, 0));
                list.Add(($"Out{r}", "Module_LotOutbound", r, Size, 180));
                for (var c = 2; c < Size; c++)
                {
                    if (r == 1 && c == 2) { list.Add(("Office", "Module_Office", r, c, 0)); continue; }
                    var company = "ABCD"[(r + c) % 4];
                    // 지게차 직무는 id "StoreD"가 열려야 고용 후보에 나온다 (StaffService.IsRoleOpen)
                    var id = company == 'D' && !storeDNamed ? "StoreD" : $"Store{company}_R{r}C{c}";
                    storeDNamed |= id == "StoreD";
                    list.Add((id, $"Module_ShelfArea_{company}", r, c, 0));
                }
            }
            return list;
        }

        static void WriteLayout(SerializedObject so, List<(string id, string asset, int row, int column, int rotation)> placements)
        {
            var modules = so.FindProperty("modules");
            modules.arraySize = placements.Count;
            for (var i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                var e = modules.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = p.id;
                e.FindPropertyRelative("module").objectReferenceValue = ModuleFactory.Load(p.asset);
                e.FindPropertyRelative("row").intValue = p.row;
                e.FindPropertyRelative("column").intValue = p.column;
                e.FindPropertyRelative("rotation").intValue = p.rotation;
                e.FindPropertyRelative("isStartArea").boolValue = true;
                e.FindPropertyRelative("unlockLevel").intValue = 1;
                e.FindPropertyRelative("cost").intValue = 0;
                e.FindPropertyRelative("unlockWith").stringValue = "";
                e.FindPropertyRelative("content").stringValue = p.id;
            }

            // 배경 = 주차 칸 바닥만 (하차 서쪽 0열, 상차 동쪽 9열)
            var parking = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Modules/Background/Module_BgParking_1.prefab");
            var background = so.FindProperty("background");
            background.arraySize = Size * 2;
            for (var r = 1; r <= Size; r++)
            {
                for (var side = 0; side < 2; side++)
                {
                    var e = background.GetArrayElementAtIndex((r - 1) * 2 + side);
                    e.FindPropertyRelative("prefab").objectReferenceValue = parking;
                    e.FindPropertyRelative("row").intValue = r;
                    e.FindPropertyRelative("column").intValue = side == 0 ? 0 : Size + 1;
                    e.FindPropertyRelative("rotation").intValue = side == 0 ? 0 : 180;
                }
            }
        }

        static void BuildScene(GameDatabase database, SiteLayout layout, List<(string id, string asset, int row, int column, int rotation)> placements)
        {
            BuildKit.SetRef(Object.FindFirstObjectByType<APC.Core.GameManager>(), "database", database);

            var grid = Object.FindFirstObjectByType<SiteGrid>();
            Object.DestroyImmediate(grid.GetComponent<StartSequence>()); // 시작 건설 순서 없이 전부 열린 채로
            for (var i = grid.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(grid.transform.GetChild(i).gameObject);

            var entries = new List<SiteGrid.Entry>();
            foreach (var placement in layout.Modules)
            {
                var instance = (SiteModule)PrefabUtility.InstantiatePrefab(placement.module, grid.transform);
                instance.transform.SetPositionAndRotation(layout.GetModuleCenter(placement, placement.module.Size), SiteLayout.GetRotation(placement));
                instance.name = $"Module_{placement.id}";
                entries.Add(new SiteGrid.Entry { id = placement.id, instance = instance });
            }
            grid.SetEntries(entries);
            EditorUtility.SetDirty(grid);

            // 하차 대기 줄은 부지 아래 (원본 자리는 하차 주차 칸과 겹침)
            var queue = grid.GetComponent<APC.Logistics.InboundQueue>();
            var size = layout.CellSize;
            var head = new Vector3(-size, 0f, -(Size + 0.5f) * size);
            var tail = 0f;
            Set(queue, so =>
            {
                so.FindProperty("headPosition").vector3Value = head;
                so.FindProperty("lineDirection").vector3Value = Vector3.back;
                tail = head.z - so.FindProperty("spacing").floatValue * (database.Logistics.InboundQueueSize - 1);
            });

            // 바닥 = 부지 + 대기 줄 끝까지 (Plane 1칸 = 10m)
            var north = 2f * size;
            var south = tail - size;
            var ground = GameObject.Find("Site/Ground");
            ground.transform.localPosition = new Vector3((Size + 1) * 0.5f * size, -0.05f, (north + south) * 0.5f);
            ground.transform.localScale = new Vector3((Size + 4) * size / 10f, 1f, (north - south) / 10f);

            var logistics = GameObject.Find("Site/Logistics");
            var tutorial = logistics.GetComponent<APC.UI.TutorialGuide>();
            if (tutorial != null) Object.DestroyImmediate(tutorial);
            logistics.AddComponent<APC.Debugging.TycoonTest>();
            AddPerformanceHud();

            var player = Object.FindFirstObjectByType<APC.Characters.PlayerMover>().transform;
            player.position = layout.CellCenter(1, 2) + new Vector3(0f, 0f, -3f);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>성능 패널: 왼쪽 아래 DEBUG 버튼 오른쪽에 [PERF], 패널은 디버그 패널(x 12~472) 오른쪽. 디버그 메뉴와 같은 1080 높이 기준.</summary>
        static void AddPerformanceHud()
        {
            var canvasGo = new GameObject("PerformanceHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 팝업 위
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var bottomLeft = Vector2.zero;
            var button = BuildKit.UIImage("Toggle", canvasGo.transform, bottomLeft, bottomLeft, new Vector2(152f + 64f, 12f + 28f), new Vector2(128f, 56f), new Color(0.15f, 0.15f, 0.15f, 0.85f), null);
            button.raycastTarget = true;
            var toggle = button.gameObject.AddComponent<Button>();
            var label = BuildKit.UIText("Label", button.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128f, 56f), "PERF", 26f, BuildKit.BodyFont, Color.white);

            var panel = BuildKit.UIImage("Panel", canvasGo.transform, bottomLeft, bottomLeft, new Vector2(484f + 240f, 80f + 440f), new Vector2(480f, 880f), new Color(0f, 0f, 0f, 0.7f), null);
            var text = BuildKit.UIText("Text", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(450f, 850f), "", 22f, BuildKit.BodyFont, Color.white);
            text.alignment = TextAlignmentOptions.TopLeft;

            var hud = canvasGo.AddComponent<APC.Debugging.PerformanceHUD>();
            BuildKit.SetRef(hud, "performanceText", text);
            BuildKit.SetRef(hud, "panel", panel.gameObject);
            BuildKit.SetRef(hud, "toggleButton", toggle);
            BuildKit.SetRef(hud, "toggleLabel", label);
        }

        static T Copy<T>(string name) where T : ScriptableObject
        {
            var path = $"{Folder}/{name}_Tycoon.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            AssetDatabase.CopyAsset($"Assets/Data/{name}.asset", path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
