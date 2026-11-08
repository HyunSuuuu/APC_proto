using System.Collections.Generic;
using APC.Contracts;
using APC.Data;
using APC.Logistics;
using APC.Site;
using APC.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static APC.EditorTools.BuildKit;

namespace APC.EditorTools
{
    /// <summary>
    /// SiteLayout(config)대로 부지를 씬에 굽는다: 시작 모듈은 프리팹 인스턴스로, 잠긴 칸은 잠김 표시로.
    /// 해금은 실행 중 SiteGrid.Unlock이 모듈 프리팹을 붙인다. "Site" 오브젝트를 지우고 다시 만들므로 여러 번 실행해도 같다.
    /// 모듈 내용(존 · 시설)을 바꾸려면 씬이 아니라 모듈 프리팹(Assets/Prefabs/Modules)을 고친다.
    /// </summary>
    public static class SiteBuilder
    {
        [MenuItem("APC/Setup/Build Site")]
        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildInternal();
        }

        /// <summary>대화상자 없이 실행 (MCP · 자동화용).</summary>
        public static void BuildInternal()
        {
            var database = Database;
            var layout = database.Site;
            var size = layout.CellSize;

            var shelfPrefab = StandardModules.ShelfPrefab();
            StandardModules.TruckPrefab();
            StandardModules.EnsureAll(overwrite: false);
            AssignDefaultModules(layout);

            DestroyIfExists(GameObject.Find("Site"));
            DestroyIfExists(GameObject.Find("Ground (임시)"));
            var site = new GameObject("Site").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(site, false);
            ground.transform.localPosition = new Vector3(2f * size, -0.05f, -1f * size);
            ground.transform.localScale = new Vector3(14f * size / 10f, 1f, 9f * size / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("M_Ground", new Color(0.36f, 0.42f, 0.36f));

            var services = new GameObject("Logistics").transform;
            services.SetParent(site, false);
            SetRef(services.gameObject.AddComponent<Warehouse>(), "shelfPrefab", shelfPrefab);
            services.gameObject.AddComponent<ContractService>();

            BakeGrid(site, layout);
            BuildContractPopup();
            MoveTestZones(size);
            PlacePlayer(layout.CellCenter(2, 1) + new Vector3(1f, 0f, -1f));

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SiteBuilder] 부지 생성 완료 — 사무실 계약 PC에서 회사 A를 계약해 보세요.");
        }

        /// <summary>config에 모듈 참조가 비어 있는 배치만 기본 모듈로 채운다 (디자이너가 바꾼 값은 유지).</summary>
        static void AssignDefaultModules(SiteLayout layout)
        {
            var so = new SerializedObject(layout);
            var list = so.FindProperty("modules");
            for (var i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                var module = element.FindPropertyRelative("module");
                if (module.objectReferenceValue != null) continue;

                var id = element.FindPropertyRelative("id").stringValue;
                if (!StandardModules.DefaultAssignments.TryGetValue(id, out var assignment)) continue;
                module.objectReferenceValue = ModuleFactory.Load(assignment.asset);
                element.FindPropertyRelative("rotation").intValue = assignment.rotation;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }

        static void BakeGrid(Transform site, SiteLayout layout)
        {
            var gridGo = new GameObject("SiteGrid");
            gridGo.transform.SetParent(site, false);
            var grid = gridGo.AddComponent<SiteGrid>();
            var entries = new List<SiteGrid.Entry>();

            foreach (var placement in layout.Modules)
            {
                var entry = new SiteGrid.Entry { id = placement.id };
                var moduleSize = placement.module != null ? placement.module.Size : Vector2Int.one;
                var center = layout.GetModuleCenter(placement, moduleSize);

                if (placement.isStartArea && placement.module != null)
                {
                    var instance = (SiteModule)PrefabUtility.InstantiatePrefab(placement.module, gridGo.transform);
                    instance.transform.SetPositionAndRotation(center, SiteLayout.GetRotation(placement));
                    instance.name = $"Module_{placement.id}";
                    entry.instance = instance;
                }
                else
                {
                    if (placement.isStartArea) Debug.LogWarning($"[SiteBuilder] 시작 칸 '{placement.id}'에 모듈이 없습니다.");
                    entry.lockedMarker = LockedMarker(gridGo.transform, placement, center, SiteLayout.RotatedSize(moduleSize, placement.rotation), layout.CellSize);
                }
                entries.Add(entry);
            }

            grid.SetEntries(entries);
            EditorUtility.SetDirty(grid);
        }

        static GameObject LockedMarker(Transform parent, SiteLayout.ModulePlacement placement, Vector3 center, Vector2Int cells, float cellSize)
        {
            var marker = new GameObject($"Locked_{placement.id}");
            marker.transform.SetParent(parent, false);
            marker.transform.position = center;
            Cube("Floor", marker.transform, center + new Vector3(0f, -0.024f, 0f),
                new Vector3(cells.x * cellSize - 0.1f, 0.05f, cells.y * cellSize - 0.1f), FloorLocked, keepCollider: true);
            FlatLabel(marker.transform, $"{placement.id} / Lv.{placement.unlockLevel}\n{ShortName(placement.content)}",
                center + new Vector3(-cellSize * 0.25f, 0.01f, -cellSize * 0.3f), 4f, new Color(0.85f, 0.85f, 0.85f));
            return marker;
        }

        static void MoveTestZones(float size)
        {
            var names = new[] { "Test_Source", "Test_SinkAll", "Test_SinkRed", "Test_GaugePad" };
            for (var i = 0; i < names.Length; i++)
            {
                var zone = GameObject.Find(names[i]);
                if (zone == null) continue;
                zone.transform.SetPositionAndRotation(new Vector3(i * size, 0f, -4f * size), Quaternion.identity);
                var label = zone.transform.Find("Label");
                if (label != null) label.rotation = CameraFacing;
            }
        }

        static void PlacePlayer(Vector3 position)
        {
            var player = GameObject.Find("Player");
            if (player != null) player.transform.position = position;
        }

        // ── 계약 팝업 UI ────────────────────────────────────────────────

        static void BuildContractPopup()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("ContractPopup");
            if (old != null) DestroyIfExists(old.gameObject);

            var root = UIRect("ContractPopup", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var body = UIRect("Body", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            var dim = UIImage("Dim", body, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f), null);
            dim.raycastTarget = true;

            var panel = UIImage("Panel", body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 640f),
                new Color(0.97f, 0.96f, 0.93f), BuiltinSprite("UI/Skin/UISprite.psd"));
            panel.type = Image.Type.Sliced;
            var panelRect = panel.rectTransform;
            UIText("Title", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 90f), "계약", 60f, TitleFont, new Color(0.25f, 0.22f, 0.2f));

            var close = UIImage("Close", panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(90f, 90f),
                new Color(0.85f, 0.4f, 0.35f), BuiltinSprite("UI/Skin/UISprite.psd"));
            close.type = Image.Type.Sliced;
            close.raycastTarget = true; // 버튼은 클릭을 받아야 한다
            var closeButton = close.gameObject.AddComponent<Button>();
            UIText("X", close.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f), "X", 48f, TitleFont, Color.white);

            var cards = UIRect("Cards", panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(960f, 440f));
            var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cardImage = UIImage("CardTemplate", cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 420f),
                Color.white, BuiltinSprite("UI/Skin/UISprite.psd"));
            cardImage.type = Image.Type.Sliced;
            var card = cardImage.rectTransform;
            var nameLabel = UIText("Name", card, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(380f, 70f), "회사", 46f, TitleFont, new Color(0.25f, 0.22f, 0.2f));
            var iconRow = UIRect("Icons", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(380f, 70f));
            var iconLayout = iconRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            iconLayout.spacing = 14f;
            iconLayout.childAlignment = TextAnchor.MiddleCenter;
            iconLayout.childControlWidth = false;
            iconLayout.childControlHeight = false;
            iconLayout.childForceExpandWidth = false;
            iconLayout.childForceExpandHeight = false;
            var icon = UIImage("IconTemplate", iconRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f),
                Color.white, BuiltinSprite("UI/Skin/UISprite.psd"));
            var infoLabel = UIText("Info", card, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(380f, 60f), "입고 트럭 8개", 34f, BodyFont, new Color(0.35f, 0.33f, 0.3f));
            var buttonImage = UIImage("Button", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(300f, 96f),
                new Color(0.3f, 0.75f, 0.4f), BuiltinSprite("UI/Skin/UISprite.psd"));
            buttonImage.type = Image.Type.Sliced;
            buttonImage.raycastTarget = true;
            var button = buttonImage.gameObject.AddComponent<Button>();
            var buttonLabel = UIText("Label", buttonImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 96f), "계약", 40f, TitleFont, Color.white);

            var contractCard = card.gameObject.AddComponent<ContractCard>();
            SetRef(contractCard, "nameLabel", nameLabel);
            SetRef(contractCard, "infoLabel", infoLabel);
            SetRef(contractCard, "iconRow", iconRow);
            SetRef(contractCard, "iconTemplate", icon);
            SetRef(contractCard, "button", button);
            SetRef(contractCard, "buttonImage", buttonImage);
            SetRef(contractCard, "buttonLabel", buttonLabel);

            var popup = root.gameObject.AddComponent<ContractPopup>();
            SetRef(popup, "body", body.gameObject);
            SetRef(popup, "group", group);
            SetRef(popup, "panel", panelRect);
            SetRef(popup, "closeButton", closeButton);
            SetRef(popup, "cardContainer", cards);
            SetRef(popup, "cardTemplate", contractCard);
            root.SetAsLastSibling();
        }
    }
}
