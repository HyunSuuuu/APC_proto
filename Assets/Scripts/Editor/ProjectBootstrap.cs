using System.Collections.Generic;
using System.IO;
using System.Linq;
using APC.Core;
using APC.Data;
using APC.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace APC.EditorTools
{
    /// <summary>
    /// M0 기반을 한 번에 만든다: 폴더, Player 설정(가로 고정), 데이터 에셋(GDD 수치), Game 씬.
    /// 여러 번 실행해도 안전하다 — 이미 있는 에셋·씬은 건드리지 않는다 (기획자가 고친 값 보존).
    /// </summary>
    public static class ProjectBootstrap
    {
        const string DataRoot = "Assets/Data";
        const string ScenePath = "Assets/Scenes/Game.unity";

        static readonly string[] Folders =
        {
            "Assets/Scripts", "Assets/Prefabs", "Assets/Art/Materials",
            DataRoot + "/Items", DataRoot + "/Companies", "Assets/Scenes",
        };

        [MenuItem("APC/Setup/Run M0 Setup")]
        static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (var folder in Folders) EnsureFolder(folder);
            ApplyPlayerSettings();
            CreateData();
            AssetDatabase.SaveAssets();
            CreateGameScene();
            AddSceneToBuild();

            Debug.Log("[Bootstrap] M0 셋업 완료 — Game 씬을 열고 Play로 확인하세요 (F1 또는 오른쪽 위 DEBUG).");
        }

        // ── Player 설정 (M0-2) ───────────────────────────────────────────

        static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true; // M21-1: 세로 · 가로 자동 회전 (UI = ScreenLayout 플래그)
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // API로 바꾼 값은 dirty 표시가 없으면 디스크에 저장되지 않는다.
            var settings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (settings.Length > 0) EditorUtility.SetDirty(settings[0]);
            Debug.Log("[Bootstrap] 화면 방향: 세로 · 가로 (Portrait · Landscape Left/Right 자동 회전)");
        }

        // ── 데이터 (M0-5) ────────────────────────────────────────────────

        static GameDatabase CreateData()
        {
            var a1 = CreateItem("A1", "빨강", "#E53935", 10);
            var a2 = CreateItem("A2", "파랑", "#1E88E5", 10);
            var b1 = CreateItem("B1", "노랑", "#FDD835", 14);
            var b2 = CreateItem("B2", "초록", "#43A047", 14);
            var b3 = CreateItem("B3", "보라", "#8E24AA", 14);

            // 선반 구역: A = 선반 구역 A(5번, R2C2), B = 선반 구역 B(3번, E1) — 회사마다 자기 구역
            var companyA = CreateCompany("A", "회사 A", 1, 8, "R2C2", 10, new[] { a1, a2 });
            var companyB = CreateCompany("B", "회사 B", 7, 10, "E1", 14, new[] { b1, b2, b3 });

            var levels = LoadOrCreate<LevelTable>($"{DataRoot}/LevelTable.asset", out _);
            var economy = LoadOrCreate<EconomyConfig>($"{DataRoot}/EconomyConfig.asset", out _);
            var orders = LoadOrCreate<OrderRules>($"{DataRoot}/OrderRules.asset", out _);
            var site = LoadOrCreate<SiteLayout>($"{DataRoot}/SiteLayout.asset", out _);
            var staff = LoadOrCreate<StaffRules>($"{DataRoot}/StaffRules.asset", out _);
            var player = LoadOrCreate<PlayerConfig>($"{DataRoot}/PlayerConfig.asset", out _);
            var controls = LoadOrCreate<ControlsConfig>($"{DataRoot}/ControlsConfig.asset", out _);
            var carry = LoadOrCreate<CarryConfig>($"{DataRoot}/CarryConfig.asset", out _);
            var logistics = LoadOrCreate<LogisticsConfig>($"{DataRoot}/LogisticsConfig.asset", out _);

            // 비어 있는 참조만 채운다 (나중에 추가된 테이블도 기존 GameDatabase에 연결되도록).
            var database = LoadOrCreate<GameDatabase>($"{DataRoot}/GameDatabase.asset", out _);
            var so = new SerializedObject(database);
            var companies = so.FindProperty("companies");
            if (companies.arraySize == 0) SetObjectList(companies, new Object[] { companyA, companyB });
            FillIfEmpty(so, "levels", levels);
            FillIfEmpty(so, "economy", economy);
            FillIfEmpty(so, "orders", orders);
            FillIfEmpty(so, "site", site);
            FillIfEmpty(so, "staff", staff);
            FillIfEmpty(so, "player", player);
            FillIfEmpty(so, "controls", controls);
            FillIfEmpty(so, "carry", carry);
            FillIfEmpty(so, "logistics", logistics);
            so.ApplyModifiedPropertiesWithoutUndo();
            return database;
        }

        static void FillIfEmpty(SerializedObject so, string property, Object value)
        {
            var prop = so.FindProperty(property);
            if (prop.objectReferenceValue == null) prop.objectReferenceValue = value;
        }

        static ItemDefinition CreateItem(string id, string colorName, string hex, int unitPrice)
        {
            var item = LoadOrCreate<ItemDefinition>($"{DataRoot}/Items/Item_{id}.asset", out var created);
            if (!created) return item;

            ColorUtility.TryParseHtmlString(hex, out var color);
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = $"{id} {colorName}";
            so.FindProperty("tapeColor").colorValue = color;
            so.FindProperty("unitPrice").intValue = unitPrice;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        static CompanyDefinition CreateCompany(string id, string displayName, int unlockLevel, int truckCapacity, string shelfAreaId, int unitPrice, ItemDefinition[] items)
        {
            var company = LoadOrCreate<CompanyDefinition>($"{DataRoot}/Companies/Company_{id}.asset", out var created);
            if (!created) return company;

            var so = new SerializedObject(company);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("unlockLevel").intValue = unlockLevel;
            so.FindProperty("inboundTruckCapacity").intValue = truckCapacity;
            SetObjectList(so.FindProperty("items"), items);
            so.FindProperty("shelfAreaId").stringValue = shelfAreaId;
            so.FindProperty("contractUnitPrice").intValue = unitPrice;
            so.ApplyModifiedPropertiesWithoutUndo();
            return company;
        }

        // ── 씬 (M0-3) ────────────────────────────────────────────────────

        static void CreateGameScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log($"[Bootstrap] {ScenePath} 이(가) 이미 있어 씬 생성은 건너뜁니다.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // NewScene(Single)이 메모리의 에셋을 내리므로, 씬을 만든 뒤에 다시 불러와야 참조가 끊기지 않는다.
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>($"{DataRoot}/GameDatabase.asset");

            // 쿼터뷰 임시 배치 (M1-3에서 카메라 리그로 교체)
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
                cam.transform.position = cam.transform.rotation * new Vector3(0f, 0f, -18f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (임시)";
            ground.transform.localScale = new Vector3(6f, 1f, 4f);

            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<GameManager>();
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("database").objectReferenceValue = database;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("DebugMenu").AddComponent<DebugMenu>();

            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // 가로 화면: 높이 기준으로 맞춘다

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Bootstrap] {ScenePath} 생성");
        }

        static void AddSceneToBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void SetObjectList(SerializedProperty list, IReadOnlyList<Object> values)
        {
            list.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
