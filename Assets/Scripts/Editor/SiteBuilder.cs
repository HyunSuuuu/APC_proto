using System.Collections.Generic;
using APC.Characters;
using APC.Contracts;
using APC.Data;
using APC.Interaction;
using APC.Logistics;
using APC.UI;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace APC.EditorTools
{
    /// <summary>
    /// M3 부지를 만든다: 바닥 칸(SiteLayout 데이터), 도로, 입고 베이 + 트럭 경로, 공용 보관소, 선반 자리, 사무실 계약 PC,
    /// 계약 팝업, 시설 프리팹(선반 · 입고 트럭). "Site" 오브젝트를 지우고 다시 만들므로 여러 번 실행해도 같다.
    /// 좌표: 칸 (행 R, 열 C) 중심 = (C × 칸 크기, 0, −(R − 1) × 칸 크기). 도로는 R1 위쪽(+z).
    /// </summary>
    public static class SiteBuilder
    {
        const string PrefabRoot = "Assets/Prefabs";
        const string MaterialRoot = "Assets/Art/Materials";
        // 카메라 각도(ControlsConfig)에 맞춰 Build 때 정한다.
        static Quaternion CameraFacing = Quaternion.Euler(45f, 0f, 0f);
        static Quaternion FlatOnFloor = Quaternion.Euler(90f, 0f, 0f);

        static TMP_FontAsset titleFont;
        static TMP_FontAsset bodyFont;

        [MenuItem("APC/Setup/Build Site (M3)")]
        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildInternal();
        }

        /// <summary>대화상자 없이 실행 (MCP · 자동화용).</summary>
        public static void BuildInternal()
        {
            titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/TmoneyRoundWindExtraBold SDF.asset");
            bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SUITE-Medium SDF.asset");

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var siteData = database.Site;
            var size = siteData.CellSize;
            CameraFacing = Quaternion.Euler(database.Controls.Pitch, database.Controls.Yaw, 0f);
            FlatOnFloor = Quaternion.Euler(90f, database.Controls.Yaw, 0f);

            var shelfPrefab = BuildShelfPrefab();
            var truckPrefab = BuildTruckPrefab();

            DestroyIfExists(GameObject.Find("Site"));
            DestroyIfExists(GameObject.Find("Ground (임시)"));
            var site = new GameObject("Site").transform;

            // 바닥 · 도로
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(site, false);
            ground.transform.localPosition = new Vector3(2.5f * size, -0.05f, -0.5f * size);
            ground.transform.localScale = new Vector3(14f * size / 10f, 1f, 9f * size / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("M_Ground", new Color(0.36f, 0.42f, 0.36f));

            Cube("Road", site, new Vector3(2.5f * size, -0.03f, RoadZ(size)), new Vector3(13f * size, 0.04f, 4f), Mat("M_Road", new Color(0.24f, 0.24f, 0.26f)));

            var cells = new GameObject("Cells").transform;
            cells.SetParent(site, false);
            foreach (var cell in siteData.Cells)
            {
                var center = CellCenter(cell.row, cell.column, size);
                var floor = Cube($"Cell_{cell.id}", cells, center + new Vector3(0f, -0.024f, 0f), new Vector3(size - 0.1f, 0.05f, size - 0.1f),
                    cell.isStartArea ? Mat("M_FloorOpen", new Color(0.8f, 0.8f, 0.78f)) : Mat("M_FloorLocked", new Color(0.5f, 0.5f, 0.52f)),
                    keepCollider: true);
                var text = cell.isStartArea ? ShortName(cell.content) : $"{cell.id} / Lv.{cell.unlockLevel}\n{ShortName(cell.content)}";
                FlatLabel(floor.transform.parent, text, center + new Vector3(-size * 0.3f, 0.01f, -size * 0.3f), cell.isStartArea ? 5f : 4f,
                    cell.isStartArea ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.85f, 0.85f, 0.85f));
            }

            // 서비스
            var services = new GameObject("Logistics").transform;
            services.SetParent(site, false);
            var warehouse = services.gameObject.AddComponent<Warehouse>();
            services.gameObject.AddComponent<ContractService>();

            BuildInboundBay(site, truckPrefab, size);
            BuildSharedStorage(site, size);
            var slots = BuildShelfArea(site, size);
            SetRef(warehouse, "shelfPrefab", shelfPrefab);
            SetList(warehouse, "shelfSlots", slots.ToArray());
            var contractPc = BuildOffice(site, size);
            SetRef(contractPc, "popup", BuildContractPopup());

            MoveTestZones(size);
            PlacePlayer(CellCenter(2, 1, size) + new Vector3(1f, 0f, -1f));

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SiteBuilder] 부지 생성 완료 — 사무실 계약 PC에서 회사 A를 계약해 보세요.");
        }

        // ── 구역 ────────────────────────────────────────────────────────

        static float RoadZ(float size) => size * 0.5f + 3f;

        static Vector3 CellCenter(int row, int column, float size) => new(column * size, 0f, -(row - 1) * size);

        static void BuildInboundBay(Transform site, InboundTruck truckPrefab, float size)
        {
            var dock = new Vector3(CellCenter(1, 1, size).x, 0f, RoadZ(size));
            var bayGo = new GameObject("InboundBay_1");
            bayGo.transform.SetParent(site, false);
            bayGo.transform.position = dock;
            var bay = bayGo.AddComponent<InboundBay>();

            var arrive = Path(bayGo.transform, "Path_Arrive", new Vector3(-3.5f * size, 0f, dock.z), dock);
            var leave = Path(bayGo.transform, "Path_Leave", dock, new Vector3(8f * size, 0f, dock.z));
            SetRef(bay, "truckPrefab", truckPrefab);
            SetRef(bay, "arrivePath", arrive);
            SetRef(bay, "leavePath", leave);

            var zone = Zone<TruckPickupZone>(site, "TruckPickupZone_1", new Vector3(dock.x, 0f, size * 0.5f - 1.1f), new Vector2(3f, 2.2f),
                new Color(0.85f, 0.75f, 0.4f), new Color(1f, 0.85f, 0.3f), "트럭 하차");
            SetRef(zone, "bay", bay);
        }

        /// <summary>공용 보관소: [내려놓기 존 (트럭 쪽, −x)] [팔레트] [집기 존 (선반 쪽, +x)].</summary>
        static void BuildSharedStorage(Transform site, float size)
        {
            var center = CellCenter(1, 2, size);
            var storageGo = new GameObject("SharedStorage");
            storageGo.transform.SetParent(site, false);
            storageGo.transform.position = center;
            var storage = storageGo.AddComponent<SharedStorage>();

            var palletPos = center + new Vector3(0f, 0f, 0.5f);
            Cube("Pallet", storageGo.transform, palletPos + new Vector3(0f, 0.05f, 0f), new Vector3(3.4f, 0.1f, 2.8f), Mat("M_Pallet", new Color(0.6f, 0.45f, 0.28f)));
            var pileRoot = new GameObject("Pile").transform;
            pileRoot.SetParent(storageGo.transform, false);
            pileRoot.position = palletPos + new Vector3(0f, 0.1f, 0f);
            var label = BillboardLabel(storageGo.transform, "0/20", palletPos + new Vector3(0f, 2.2f, 0f), 7f);
            SetRef(storage, "pileRoot", pileRoot);
            SetRef(storage, "countLabel", label);

            var zoneSize = new Vector2(2.4f, 3.2f);
            var drop = Zone<StorageDropZone>(storageGo.transform, "DropZone", palletPos + new Vector3(-3.1f, 0f, 0f), zoneSize,
                new Color(0.5f, 0.72f, 0.5f), new Color(0.35f, 0.95f, 0.35f), "내려놓기");
            var pick = Zone<StoragePickZone>(storageGo.transform, "PickZone", palletPos + new Vector3(3.1f, 0f, 0f), zoneSize,
                new Color(0.5f, 0.62f, 0.8f), new Color(0.35f, 0.7f, 1f), "집기");
            SetRef(drop, "storage", storage);
            SetRef(pick, "storage", storage);
        }

        static List<ShelfSlot> BuildShelfArea(Transform site, float size)
        {
            var center = CellCenter(1, 3, size);
            var area = new GameObject("ShelfArea_A").transform;
            area.SetParent(site, false);
            area.position = center;

            var offsets = new[]
            {
                new Vector3(-2.4f, 0f, 3.2f), new Vector3(-2.4f, 0f, 0f), new Vector3(-2.4f, 0f, -3.2f), new Vector3(2.4f, 0f, 3.2f), // Lv.1
                new Vector3(2.4f, 0f, 0f), new Vector3(2.4f, 0f, -3.2f), // Lv.5
            };
            var slots = new List<ShelfSlot>();
            for (var i = 0; i < offsets.Length; i++)
            {
                var slotGo = new GameObject($"ShelfSlot_{i + 1}");
                slotGo.transform.SetParent(area, false);
                slotGo.transform.localPosition = offsets[i];
                var slot = slotGo.AddComponent<ShelfSlot>();
                SetInt(slot, "unlockLevel", i < 4 ? 1 : 5);
                slots.Add(slot);
            }
            return slots;
        }

        static ContractPC BuildOffice(Transform site, float size)
        {
            var center = CellCenter(2, 1, size);
            var pc = Zone<ContractPC>(site, "ContractPC", center + new Vector3(-2f, 0f, 2f), new Vector2(2.2f, 2.2f),
                new Color(0.75f, 0.68f, 0.5f), new Color(1f, 0.85f, 0.3f), "계약 PC");
            var desk = center + new Vector3(-2f, 0f, 3.6f);
            Cube("Desk", pc.transform, desk + new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 0.7f), Mat("M_Desk", new Color(0.55f, 0.4f, 0.3f)));
            Cube("Monitor", pc.transform, desk + new Vector3(0f, 1.1f, 0.1f), new Vector3(0.9f, 0.55f, 0.08f), Mat("M_Monitor", new Color(0.15f, 0.2f, 0.3f)));
            GaugeRing(pc, pc.transform);
            return pc;
        }

        static void MoveTestZones(float size)
        {
            var names = new[] { "Test_Source", "Test_SinkAll", "Test_SinkRed", "Test_GaugePad" };
            for (var i = 0; i < names.Length; i++)
            {
                var zone = GameObject.Find(names[i]);
                if (zone == null) continue;
                zone.transform.SetPositionAndRotation(new Vector3(i * size, 0f, -2.4f * size), Quaternion.identity);
                var label = zone.transform.Find("Label");
                if (label != null) label.rotation = CameraFacing;
            }
        }

        static void PlacePlayer(Vector3 position)
        {
            var player = GameObject.Find("Player");
            if (player != null) player.transform.position = position;
        }

        // ── 프리팹 ──────────────────────────────────────────────────────

        static Shelf BuildShelfPrefab()
        {
            var path = $"{PrefabRoot}/Shelf.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Shelf>(path);
            if (existing != null) return existing;

            var root = new GameObject("Shelf");
            var metal = Mat("M_Shelf", new Color(0.35f, 0.45f, 0.6f));
            foreach (var x in new[] { -0.37f, 0.37f })
            foreach (var z in new[] { -1.32f, 1.32f })
                Cube("Post", root.transform, new Vector3(x, 0.85f, z), new Vector3(0.06f, 1.7f, 0.06f), metal);
            foreach (var y in new[] { 0.1f, 0.9f, 1.7f })
                Cube("Board", root.transform, new Vector3(0f, y, 0f), new Vector3(0.8f, 0.05f, 2.7f), metal);
            var icon = Cube("ItemIcon", root.transform, new Vector3(0f, 1.76f, 0f), new Vector3(0.6f, 0.06f, 0.6f), Mat("M_Tape", Color.white));
            var boxRoot = new GameObject("Boxes").transform;
            boxRoot.SetParent(root.transform, false);
            var label = BillboardLabel(root.transform, "0/10", new Vector3(0f, 2.4f, 0f), 6f);

            var zone = Zone<ShelfLoadZone>(root.transform, "LoadZone", new Vector3(-1.25f, 0f, 0f), new Vector2(1.6f, 2.6f),
                new Color(0.5f, 0.62f, 0.8f), new Color(0.35f, 0.7f, 1f), null);
            Cube("PickZone (M4)", root.transform, new Vector3(1.25f, 0.01f, 0f), new Vector3(1.6f, 0.02f, 2.6f), Mat("M_ZoneFloor", Color.white));

            var shelf = root.AddComponent<Shelf>();
            SetRef(shelf, "boxRoot", boxRoot);
            SetRef(shelf, "iconRenderer", icon.GetComponent<Renderer>());
            SetRef(shelf, "countLabel", label);
            SetRef(zone, "shelf", shelf);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<Shelf>(path);
        }

        static InboundTruck BuildTruckPrefab()
        {
            var path = $"{PrefabRoot}/InboundTruck.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<InboundTruck>(path);
            if (existing != null) return existing;

            var root = new GameObject("InboundTruck");
            Cube("Cargo", root.transform, new Vector3(0f, 1.5f, -0.5f), new Vector3(2.2f, 2.2f, 3.4f), Mat("M_TruckBody", new Color(0.92f, 0.92f, 0.9f)));
            Cube("Cab", root.transform, new Vector3(0f, 1.15f, 1.9f), new Vector3(2.2f, 1.8f, 1.4f), Mat("M_TruckCab", new Color(0.2f, 0.5f, 0.85f)));
            var exit = new GameObject("CargoExit").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(1.4f, 1f, -0.5f); // 진행 방향 오른쪽 = 정차 시 부지 쪽
            var label = BillboardLabel(root.transform, "8", new Vector3(0f, 3.5f, -0.5f), 11f);

            var truck = root.AddComponent<InboundTruck>();
            SetRef(truck, "cargoExit", exit);
            SetRef(truck, "countLabel", label);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<InboundTruck>(path);
        }

        // ── 계약 팝업 UI ────────────────────────────────────────────────

        static ContractPopup BuildContractPopup()
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
                new Color(0.97f, 0.96f, 0.93f), Sprite("UI/Skin/UISprite.psd"));
            panel.type = Image.Type.Sliced;
            var panelRect = panel.rectTransform;
            UIText("Title", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 90f), "계약", 60f, titleFont, new Color(0.25f, 0.22f, 0.2f));

            var close = UIImage("Close", panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(90f, 90f),
                new Color(0.85f, 0.4f, 0.35f), Sprite("UI/Skin/UISprite.psd"));
            close.type = Image.Type.Sliced;
            close.raycastTarget = true; // 버튼은 클릭을 받아야 한다
            var closeButton = close.gameObject.AddComponent<Button>();
            UIText("X", close.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f), "X", 48f, titleFont, Color.white);

            var cards = UIRect("Cards", panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(960f, 440f));
            var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cardImage = UIImage("CardTemplate", cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 420f),
                Color.white, Sprite("UI/Skin/UISprite.psd"));
            cardImage.type = Image.Type.Sliced;
            var card = cardImage.rectTransform;
            var nameLabel = UIText("Name", card, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(380f, 70f), "회사", 46f, titleFont, new Color(0.25f, 0.22f, 0.2f));
            var iconRow = UIRect("Icons", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(380f, 70f));
            var iconLayout = iconRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            iconLayout.spacing = 14f;
            iconLayout.childAlignment = TextAnchor.MiddleCenter;
            iconLayout.childControlWidth = false;
            iconLayout.childControlHeight = false;
            iconLayout.childForceExpandWidth = false;
            iconLayout.childForceExpandHeight = false;
            var icon = UIImage("IconTemplate", iconRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f),
                Color.white, Sprite("UI/Skin/UISprite.psd"));
            var infoLabel = UIText("Info", card, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(380f, 60f), "입고 트럭 8개", 34f, bodyFont, new Color(0.35f, 0.33f, 0.3f));
            var buttonImage = UIImage("Button", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(300f, 96f),
                new Color(0.3f, 0.75f, 0.4f), Sprite("UI/Skin/UISprite.psd"));
            buttonImage.type = Image.Type.Sliced;
            buttonImage.raycastTarget = true;
            var button = buttonImage.gameObject.AddComponent<Button>();
            var buttonLabel = UIText("Label", buttonImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 96f), "계약", 40f, titleFont, Color.white);

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
            return popup;
        }

        // ── 공용 헬퍼 ────────────────────────────────────────────────────

        // 모든 헬퍼는 월드 좌표를 받는다. 프리팹은 원점에서 만들므로 프리팹 안에서는 월드 = 로컬이다.

        static T Zone<T>(Transform parent, string name, Vector3 position, Vector2 zoneSize, Color idle, Color active, string label) where T : InteractionZone
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var floor = Cube("Floor", go.transform, go.transform.position + new Vector3(0f, 0.01f, 0f), new Vector3(zoneSize.x, 0.02f, zoneSize.y), Mat("M_ZoneFloor", Color.white));
            var zone = go.AddComponent<T>();
            var so = new SerializedObject(zone);
            so.FindProperty("size").vector2Value = zoneSize;
            so.FindProperty("floor").objectReferenceValue = floor.GetComponent<Renderer>();
            so.FindProperty("idleColor").colorValue = idle;
            so.FindProperty("activeColor").colorValue = active;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!string.IsNullOrEmpty(label))
                FlatLabel(go.transform, label, go.transform.position + new Vector3(0f, 0.03f, -zoneSize.y * 0.5f - 0.7f), 3.6f, Color.white);
            return zone;
        }

        static void GaugeRing(GaugeZone zone, Transform parent)
        {
            var canvasGo = new GameObject("GaugeRing", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(180f, 180f);
            rect.position = parent.position + new Vector3(0f, 0.04f, 0f);
            rect.rotation = Quaternion.Euler(90f, 0f, 0f);
            rect.localScale = Vector3.one * 0.01f;
            UIImage("Back", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.25f), Sprite("UI/Skin/Knob.psd"));
            var fill = UIImage("Fill", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.95f, 0.4f, 0.9f), Sprite("UI/Skin/Knob.psd"));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillAmount = 0f;
            var view = parent.gameObject.AddComponent<GaugeRingView>();
            SetRef(view, "zone", zone);
            SetRef(view, "fill", fill);
        }

        static DOTweenPath Path(Transform parent, string name, Vector3 start, Vector3 end)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = start;
            var path = go.AddComponent<DOTweenPath>();
            path.autoPlay = false;
            path.pathType = PathType.Linear;
            path.wps = new List<Vector3> { end };
            return path;
        }

        static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            if (!keepCollider) Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        static TMP_Text BillboardLabel(Transform parent, string text, Vector3 position, float fontSize)
        {
            var go = new GameObject("CountLabel", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, CameraFacing);
            go.AddComponent<Billboard>();
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = titleFont;
            tmp.fontSharedMaterial = OutlineMaterial(titleFont);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            return tmp;
        }

        /// <summary>
        /// 외곽선이 켜진 공유 재질 에셋. tmp.outlineWidth를 코드에서 바꾸면 재질 복사본이 생겨
        /// 프리팹에 저장되지 않으므로, 재질 에셋을 하나 만들어 같이 쓴다.
        /// </summary>
        static Material OutlineMaterial(TMP_FontAsset font)
        {
            var path = $"Assets/Fonts/{font.name} Outline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(font.material) { name = $"{font.name} Outline" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(40, 40, 40, 255));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void FlatLabel(Transform parent, string text, Vector3 position, float fontSize, Color color)
        {
            var go = new GameObject("FloorLabel", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, FlatOnFloor);
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = bodyFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 3f);
        }

        static string ShortName(string content)
        {
            var paren = content.IndexOf('(');
            return paren > 0 ? content[..paren].Trim() : content;
        }

        static Material Mat(string name, Color color)
        {
            var path = $"{MaterialRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Sprite Sprite(string builtinPath) => AssetDatabase.GetBuiltinExtraResource<Sprite>(builtinPath);

        static RectTransform UIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image UIImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            var rect = UIRect(name, parent, anchorMin, anchorMax, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false; // 장식용 기본값. 버튼 · 입력 차단용은 만든 뒤 true로 바꾼다
            return image;
        }

        static TMP_Text UIText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, float fontSize, TMP_FontAsset font, Color color)
        {
            var rect = UIRect(name, parent, anchor, anchor, position, size);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void DestroyIfExists(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetList(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var list = so.FindProperty(property);
            list.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
