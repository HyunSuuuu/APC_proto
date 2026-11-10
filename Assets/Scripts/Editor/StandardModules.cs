using System;
using System.Collections.Generic;
using APC.Contracts;
using APC.Logistics;
using APC.Site;
using UnityEditor;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// 기본 모듈 프리팹들 (Assets/Prefabs/Modules). 없을 때만 만들고, 디자이너가 고친 프리팹은 덮어쓰지 않는다.
    /// 다시 만들려면 메뉴 APC > Module > Rebuild Standard Modules.
    /// 모든 좌표는 모듈 중심 기준 (1칸 = −5 ~ +5).
    /// </summary>
    public static class StandardModules
    {
        const string PrefabRoot = "Assets/Prefabs";
        static readonly Color LabelColor = new(0.35f, 0.35f, 0.35f);

        /// <summary>
        /// SiteLayout id → 기본 모듈 (처음 배치할 때 config에 채운다). 같은 모듈을 여러 칸에 재사용한다.
        /// 하차 · 상차장은 회전으로 주차장 방향을 정한다 (로컬 −x = 바깥). 같은 모서리를 향하는 두 주차장은
        /// 트럭이 겹치지 않게 주차 칸을 반대로 비킨 모듈(_M)을 쓴다 (4 · 2 → 1번 쪽, 8 · 6 → 9번 쪽).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (string asset, int rotation)> DefaultAssignments =
            new Dictionary<string, (string, int)>
            {
                ["R1C1"] = ("Module_Office", 0),            // 7
                ["R1C2"] = ("Module_LotOutbound_M", 180),   // 8 — 트럭은 오른쪽에서
                ["R2C1"] = ("Module_LotInbound", 270),      // 4 — 트럭은 아래에서
                ["R2C2"] = ("Module_ShelfArea_A", 0),       // 5
                ["R2C3"] = ("Module_LotOutbound", 90),      // 6 — 트럭은 위에서
                ["R3C2"] = ("Module_LotInbound_M", 0),      // 2 — 트럭은 왼쪽에서
                ["E1"] = ("Module_ShelfArea_B", 0),         // 3
                ["E2"] = ("Module_LotInbound", 270),        // 4번 왼쪽
                ["E3"] = ("Module_LotOutbound", 90),        // 6번 오른쪽
                ["E4"] = ("Module_Returns", 0),             // 3번 오른쪽
            };

        static readonly (string asset, Action<GameObject> build)[] Builders =
        {
            ("Module_LotInbound", root => BuildLot(root, inbound: true, parkSide: 1f)),
            ("Module_LotInbound_M", root => BuildLot(root, inbound: true, parkSide: -1f)),
            ("Module_LotOutbound", root => BuildLot(root, inbound: false, parkSide: 1f)),
            ("Module_LotOutbound_M", root => BuildLot(root, inbound: false, parkSide: -1f)),
            ("Module_ShelfArea_A", root => BuildShelfArea(root, "선반 구역 A")),
            ("Module_ShelfArea_B", root => BuildShelfArea(root, "선반 구역 B")),
            ("Module_Office", BuildOffice),
            ("Module_Returns", root => Label(root, "반품 구역")),
            // 빈 타일 (바닥만). 다른 배치와 같이 해금되게 묶어 ㄱ자 같은 구역을 만든다 (SiteLayout.unlockWith).
            ("Module_Empty", _ => { }),
        };

        /// <summary>v0.3에서 쓰지 않게 된 프리팹 (Apply Layout v0.3이 지운다).</summary>
        public static readonly string[] ObsoleteAssets =
        {
            $"{ModuleFactory.ModuleFolder}/Module_Storage.prefab", $"{ModuleFactory.ModuleFolder}/Module_OutboundArea.prefab",
            $"{ModuleFactory.ModuleFolder}/Module_Packing.prefab", $"{ModuleFactory.ModuleFolder}/Module_ShelfArea.prefab",
            $"{PrefabRoot}/ExpansionPad.prefab",
        };

        [MenuItem("APC/Module/Rebuild Standard Modules (덮어쓰기)")]
        static void RebuildAll()
        {
            if (!EditorUtility.DisplayDialog("기본 모듈 다시 만들기", "기본 모듈 프리팹을 모두 덮어씁니다. 프리팹에서 고친 내용은 사라집니다.", "덮어쓰기", "취소")) return;
            EnsureAll(overwrite: true);
        }

        public static void EnsureAll(bool overwrite)
        {
            foreach (var (asset, _) in Builders)
            {
                if (!overwrite && ModuleFactory.Load(asset) != null) continue;
                Rebuild(asset);
            }
        }

        /// <summary>기본 모듈 하나를 코드 기준으로 다시 만든다 (같은 경로에 덮어써서 참조는 유지된다).</summary>
        public static void Rebuild(string asset)
        {
            foreach (var (name, build) in Builders)
            {
                if (name != asset) continue;
                var root = ModuleFactory.CreateEmpty(asset, Vector2Int.one, BuildKit.Database.Site.CellSize);
                build(root);
                ModuleFactory.Save(root, asset);
                return;
            }
            Debug.LogWarning($"[StandardModules] 기본 모듈 '{asset}'이 없습니다.");
        }

        // ── 모듈 ────────────────────────────────────────────────────────

        /// <summary>
        /// 하차장 · 상차장 (1칸, 가장자리용, GDD 4.1). 주차장은 **칸 바깥 가장자리(−x)** 에 붙어 있고,
        /// 바깥에 나타난 트럭이 후진으로 곧장 들어와 뒷문을 가장자리(슬라이드 도어 자리)에 댄다.
        /// 트럭 뒷문 앞(칸 안 가장자리)에 하차 · 상차 존. 하차장은 그 안쪽에 팔레트(공용 보관소)가 있고, 상차장은 상차 존 옆에 현금 존이 있다.
        /// parkSide(±1): 주차 칸을 가장자리를 따라 비킬 방향 — 같은 모서리를 향하는 두 주차장이 겹치지 않게.
        /// </summary>
        static void BuildLot(GameObject root, bool inbound, float parkSide)
        {
            var content = ModuleFactory.Content(root);
            const float edge = -5f;
            var lane = 2.5f * parkSide;                       // 주차 칸 중심 (가장자리를 따라)
            // 트럭 루트 기준 뒷면 −2.2 → 뒷면을 가장자리에 대려면 루트는 가장자리에서 2.2 바깥
            var park = new Vector3(edge - 2.2f, 0f, lane);
            var dock = new Vector3(-3.6f, 0f, lane);

            // 바깥 주차 칸 (칸 판정 밖이라 걸어 들어갈 수 없다)
            BuildKit.Cube("Asphalt", content, new Vector3(edge - 4f, 0.006f, lane), new Vector3(8f, 0.012f, 3.4f), BuildKit.Asphalt);
            foreach (var side in new[] { -1.75f, 1.75f })
                BuildKit.Cube("Line", content, new Vector3(edge - 4f, 0.014f, lane + side), new Vector3(8f, 0.012f, 0.12f), BuildKit.Mat("M_Line", Color.white));
            // 슬라이드 도어 자리 (나중에 모델로 교체)
            var frame = BuildKit.Mat("M_DoorFrame", new Color(0.45f, 0.47f, 0.5f));
            foreach (var side in new[] { -1.55f, 1.55f })
                BuildKit.Cube("DoorPost", content, new Vector3(edge, 1.3f, lane + side), new Vector3(0.2f, 2.6f, 0.2f), frame);
            BuildKit.Cube("DoorLintel", content, new Vector3(edge, 2.7f, lane), new Vector3(0.2f, 0.25f, 3.3f), frame);

            var bayGo = new GameObject(inbound ? "InboundBay" : "OutboundBay");
            bayGo.transform.SetParent(content, false);
            var parkPoint = new GameObject("ParkPoint").transform;
            parkPoint.SetParent(bayGo.transform, false);
            parkPoint.SetPositionAndRotation(park, Quaternion.LookRotation(Vector3.left));
            var spawnPoint = new GameObject("SpawnPoint").transform;
            spawnPoint.SetParent(bayGo.transform, false);
            spawnPoint.SetPositionAndRotation(park + new Vector3(-8f, 0f, 0f), Quaternion.LookRotation(Vector3.left));

            if (inbound)
            {
                var bay = bayGo.AddComponent<InboundBay>();
                BuildKit.SetRef(bay, "truckPrefab", TruckPrefab());
                BuildKit.SetRef(bay, "parkPoint", parkPoint);
                BuildKit.SetRef(bay, "spawnPoint", spawnPoint);
                var zone = BuildKit.Zone<TruckPickupZone>(content, "PickupZone", dock, new Vector2(2f, 2.6f),
                    new Color(0.85f, 0.75f, 0.4f), new Color(1f, 0.85f, 0.3f), "하차");
                BuildKit.SetRef(zone, "bay", bay);
                BuildPallet(content);
                Label(root, "하차장");
            }
            else
            {
                var bay = bayGo.AddComponent<OutboundBay>();
                BuildKit.SetRef(bay, "truckPrefab", OutboundTruckPrefab());
                BuildKit.SetRef(bay, "parkPoint", parkPoint);
                BuildKit.SetRef(bay, "spawnPoint", spawnPoint);
                var zone = BuildKit.Zone<TruckLoadZone>(content, "LoadZone", dock, new Vector2(2f, 2.6f),
                    new Color(0.85f, 0.65f, 0.45f), new Color(1f, 0.6f, 0.25f), "상차");
                BuildKit.SetRef(zone, "bay", bay);
                // 현금 존: 상차 존 바로 옆 (GDD 7.6 확정), 주차 칸 반대쪽
                BuildKit.SetRef(bay, "cashZone", BuildCashZone(content, new Vector3(-3.6f, 0f, -3.3f * parkSide)));
                Label(root, "상차장");
            }
        }

        /// <summary>하차장 안의 팔레트(공용 보관소) + 둘레 존 하나 (내려놓기 우선, 없으면 선반 빈자리만큼 집기).</summary>
        static void BuildPallet(Transform content)
        {
            var storage = content.gameObject.AddComponent<SharedStorage>();
            var pallet = new Vector3(1.4f, 0f, 0f);
            BuildKit.Cube("Pallet", content, pallet + new Vector3(0f, 0.05f, 0f), new Vector3(2.8f, 0.1f, 2.4f), BuildKit.Mat("M_Pallet", new Color(0.6f, 0.45f, 0.28f)));
            var pileRoot = new GameObject("Pile").transform;
            pileRoot.SetParent(content, false);
            pileRoot.position = pallet + new Vector3(0f, 0.1f, 0f);
            BuildKit.SetRef(storage, "pileRoot", pileRoot);
            BuildKit.SetRef(storage, "countLabel", BuildKit.BillboardLabel(content, "0/20", pallet + new Vector3(0f, 2.2f, 0f), 7f));

            // 팔레트(2.8 × 2.4)를 0.7m씩 두른 존. 하차 존(x −4.6 ~ −2.6)과 겹치지 않는다.
            var zone = BuildKit.Zone<StorageZone>(content, "Zone", pallet, new Vector2(4.2f, 3.8f),
                new Color(0.5f, 0.72f, 0.5f), new Color(0.35f, 0.95f, 0.35f), null);
            BuildKit.SetRef(zone, "storage", storage);
        }

        /// <summary>현금 존: 바닥 + 뭉치가 쌓이는 자리. 흡수 반경은 EconomyConfig.</summary>
        static CashZone BuildCashZone(Transform parent, Vector3 position)
        {
            var go = new GameObject("CashZone");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            BuildKit.Cube("Floor", go.transform, position + new Vector3(0f, 0.01f, 0f), new Vector3(2f, 0.02f, 1.8f), BuildKit.Mat("M_CashFloor", new Color(0.55f, 0.8f, 0.55f)));
            var pile = new GameObject("Pile").transform;
            pile.SetParent(go.transform, false);
            pile.position = position + new Vector3(0f, 0.02f, 0f);
            BuildKit.FlatLabel(go.transform, "현금", position + new Vector3(0f, 0.03f, 1.6f), 3.6f, Color.white);

            var zone = go.AddComponent<CashZone>();
            BuildKit.SetRef(zone, "pileRoot", pile);
            BuildKit.SetRef(zone, "bundlePrefab", CashBundlePrefab());
            return zone;
        }

        /// <summary>정사각형 선반 자리 4개 (2 × 2). 회사 하나의 선반(품목당 1개)이 들어간다. 선반 사이는 통로.</summary>
        static void BuildShelfArea(GameObject root, string title)
        {
            var content = ModuleFactory.Content(root);
            var offsets = new[]
            {
                new Vector3(-2.4f, 0f, 2.4f), new Vector3(2.4f, 0f, 2.4f), new Vector3(-2.4f, 0f, -2.4f), new Vector3(2.4f, 0f, -2.4f),
            };
            for (var i = 0; i < offsets.Length; i++)
            {
                var slotGo = new GameObject($"ShelfSlot_{i + 1}");
                slotGo.transform.SetParent(content, false);
                slotGo.transform.localPosition = offsets[i];
                var slot = slotGo.AddComponent<ShelfSlot>();
                BuildKit.SetInt(slot, "unlockLevel", 1);
                BuildKit.SetInt(slot, "order", i);
            }
            Label(root, title);
        }

        /// <summary>사무실 (7번). 계약 PC는 오른쪽 위.</summary>
        static void BuildOffice(GameObject root)
        {
            var content = ModuleFactory.Content(root);
            var pc = BuildKit.Zone<ContractPC>(content, "ContractPC", new Vector3(2f, 0f, 2f), new Vector2(2.2f, 2.2f),
                new Color(0.75f, 0.68f, 0.5f), new Color(1f, 0.85f, 0.3f), "계약 PC");
            var desk = new Vector3(2f, 0f, 3.6f);
            BuildKit.Cube("Desk", pc.transform, desk + new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 0.7f), BuildKit.Mat("M_Desk", new Color(0.55f, 0.4f, 0.3f)));
            BuildKit.Cube("Monitor", pc.transform, desk + new Vector3(0f, 1.1f, 0.1f), new Vector3(0.9f, 0.55f, 0.08f), BuildKit.Mat("M_Monitor", new Color(0.15f, 0.2f, 0.3f)));
            BuildKit.GaugeRing(pc, pc.transform);
            Label(root, "사무실");
        }

        static void Label(GameObject root, string text)
            => BuildKit.FlatLabel(ModuleFactory.Content(root), text, new Vector3(-2.5f, 0.01f, -3.5f), 5f, LabelColor);

        // ── 시설 프리팹 ─────────────────────────────────────────────────

        public static Shelf ShelfPrefab(bool overwrite = false)
        {
            var path = $"{PrefabRoot}/Shelf.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Shelf>(path);
            if (existing != null && !overwrite) return existing;

            // 정사각형 선반 1.6m (한 단 3 × 3칸, 3단) + 둘레 존 3.2m 하나 (GDD 7.3)
            var root = new GameObject("Shelf");
            var metal = BuildKit.Mat("M_Shelf", new Color(0.35f, 0.45f, 0.6f));
            foreach (var x in new[] { -0.77f, 0.77f })
            foreach (var z in new[] { -0.77f, 0.77f })
                BuildKit.Cube("Post", root.transform, new Vector3(x, 0.85f, z), new Vector3(0.06f, 1.7f, 0.06f), metal);
            foreach (var y in new[] { 0.1f, 0.9f, 1.7f })
                BuildKit.Cube("Board", root.transform, new Vector3(0f, y, 0f), new Vector3(1.6f, 0.05f, 1.6f), metal);
            var icon = BuildKit.Cube("ItemIcon", root.transform, new Vector3(0f, 2.35f, 0f), new Vector3(0.6f, 0.06f, 0.6f), BuildKit.Mat("M_Tape", Color.white));
            var boxRoot = new GameObject("Boxes").transform;
            boxRoot.SetParent(root.transform, false);
            var label = BuildKit.BillboardLabel(root.transform, "0/10", new Vector3(0f, 2.9f, 0f), 6f);

            var zone = BuildKit.Zone<ShelfZone>(root.transform, "Zone", Vector3.zero, new Vector2(3.2f, 3.2f),
                new Color(0.5f, 0.62f, 0.8f), new Color(0.35f, 0.7f, 1f), null);

            var shelf = root.AddComponent<Shelf>();
            BuildKit.SetRef(shelf, "boxRoot", boxRoot);
            BuildKit.SetRef(shelf, "iconRenderer", icon.GetComponent<Renderer>());
            BuildKit.SetRef(shelf, "countLabel", label);
            BuildKit.SetRef(zone, "shelf", shelf);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<Shelf>(path);
        }

        public static InboundTruck TruckPrefab()
        {
            var path = $"{PrefabRoot}/InboundTruck.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<InboundTruck>(path);
            if (existing != null) return existing;

            var root = new GameObject("InboundTruck");
            BuildKit.Cube("Cargo", root.transform, new Vector3(0f, 1.5f, -0.5f), new Vector3(2.2f, 2.2f, 3.4f), BuildKit.Mat("M_TruckBody", new Color(0.92f, 0.92f, 0.9f)));
            BuildKit.Cube("Cab", root.transform, new Vector3(0f, 1.15f, 1.9f), new Vector3(2.2f, 1.8f, 1.4f), BuildKit.Mat("M_TruckCab", new Color(0.2f, 0.5f, 0.85f)));
            var exit = new GameObject("CargoExit").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(0f, 1f, -2.5f); // 뒷문 (후진 주차하면 물류센터 쪽)
            var label = BuildKit.BillboardLabel(root.transform, "8", new Vector3(0f, 3.5f, -0.5f), 11f);

            var truck = root.AddComponent<InboundTruck>();
            BuildKit.SetRef(truck, "cargoExit", exit);
            BuildKit.SetRef(truck, "countLabel", label);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<InboundTruck>(path);
        }

        /// <summary>출고 트럭: 입고 트럭과 같은 크기, 주황 운전석. 위에 주문 카드(품목 실은 수/필요 수).</summary>
        public static OutboundTruck OutboundTruckPrefab(bool overwrite = false)
        {
            var path = $"{PrefabRoot}/OutboundTruck.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<OutboundTruck>(path);
            if (existing != null && !overwrite) return existing;

            var root = new GameObject("OutboundTruck");
            BuildKit.Cube("Cargo", root.transform, new Vector3(0f, 1.5f, -0.5f), new Vector3(2.2f, 2.2f, 3.4f), BuildKit.Mat("M_TruckBody", new Color(0.92f, 0.92f, 0.9f)));
            BuildKit.Cube("Cab", root.transform, new Vector3(0f, 1.15f, 1.9f), new Vector3(2.2f, 1.8f, 1.4f), BuildKit.Mat("M_TruckCabOut", new Color(0.95f, 0.55f, 0.2f)));
            var entry = new GameObject("CargoEntry").transform;
            entry.SetParent(root.transform, false);
            entry.localPosition = new Vector3(0f, 1.2f, -2.3f); // 뒷문 (후진 주차하면 물류센터 쪽)
            var label = BuildKit.BillboardLabel(root.transform, "A1 0/3", new Vector3(0f, 4.4f, -0.5f), 8f);
            label.alignment = TMPro.TextAlignmentOptions.Bottom; // 줄이 늘면 위로 쌓인다
            label.rectTransform.sizeDelta = new Vector2(5f, 3f);
            label.name = "OrderLabel";

            var truck = root.AddComponent<OutboundTruck>();
            BuildKit.SetRef(truck, "cargoEntry", entry);
            BuildKit.SetRef(truck, "orderLabel", label);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<OutboundTruck>(path);
        }

        /// <summary>
        /// 공사장 가설 울타리 (칸 1개, GDD 4.1 · 9.3): 칸 둘레에 주황 · 흰 줄무늬 패널 + 기둥, 가운데 위에 `잠금해제 : LV N` 표지.
        /// SiteUnlocker가 다음 해금 구역에 놓는다.
        /// </summary>
        public static ConstructionFence ConstructionFencePrefab(bool overwrite = false)
        {
            var path = $"{PrefabRoot}/ConstructionFence.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<ConstructionFence>(path);
            if (existing != null && !overwrite) return existing;

            var root = new GameObject("ConstructionFence");
            var orange = BuildKit.Mat("M_FenceOrange", new Color(0.95f, 0.55f, 0.15f));
            var white = BuildKit.Mat("M_FenceWhite", new Color(0.95f, 0.95f, 0.92f));
            var post = BuildKit.Mat("M_FencePost", new Color(0.4f, 0.4f, 0.42f));
            const float half = 4.6f;   // 칸 가장자리에서 0.4m 안쪽
            const int panels = 5;      // 한 변 패널 수
            var length = half * 2f / panels;

            for (var side = 0; side < 4; side++)
            {
                var rotation = Quaternion.Euler(0f, side * 90f, 0f);
                for (var i = 0; i < panels; i++)
                {
                    var along = -half + length * (i + 0.5f);
                    var center = rotation * new Vector3(along, 0f, half);
                    var panel = BuildKit.Cube("Panel", root.transform, center + new Vector3(0f, 0.65f, 0f), new Vector3(length - 0.1f, 0.9f, 0.06f), i % 2 == 0 ? orange : white);
                    panel.transform.rotation = rotation;
                    var cornerPos = rotation * new Vector3(-half + length * i, 0f, half);
                    BuildKit.Cube("Post", root.transform, cornerPos + new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), post);
                }
            }

            var sign = BuildKit.BillboardLabel(root.transform, "잠금해제 : LV 3", new Vector3(0f, 2.2f, 0f), 9f);
            sign.name = "Sign";
            sign.rectTransform.sizeDelta = new Vector2(8f, 1.6f);
            var fence = root.AddComponent<ConstructionFence>();
            BuildKit.SetRef(fence, "label", sign);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<ConstructionFence>(path);
        }

        /// <summary>
        /// v0.3 배치 적용: SiteLayout · 레벨 표 · 회사 선반 구역을 기본값으로, 기본 모듈 · 선반 · 울타리 프리팹을 다시 만들고,
        /// 안 쓰는 프리팹을 지운 뒤 Build Site. config를 손으로 고쳤다면 그 값은 사라진다.
        /// </summary>
        [MenuItem("APC/Setup/Apply Layout v0.3 (config · 프리팹 덮어쓰기)")]
        static void ApplyLayoutV3()
        {
            if (!EditorUtility.DisplayDialog("v0.3 배치 적용", "SiteLayout · 레벨 표 · 회사 선반 구역을 기본값으로 되돌리고 기본 프리팹을 덮어씁니다.", "적용", "취소")) return;
            ApplyLayoutV3Internal();
        }

        public static void ApplyLayoutV3Internal()
        {
            var database = BuildKit.Database;
            database.Site.ResetModules();
            EditorUtility.SetDirty(database.Site);

            database.Levels.ResetLevels();
            var levels = new SerializedObject(database.Levels);
            levels.FindProperty("overflowBaseXp").intValue = 350;
            levels.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database.Levels);

            foreach (var company in database.Companies)
            {
                var so = new SerializedObject(company);
                var area = so.FindProperty("shelfAreaId");
                area.stringValue = company.Id == "A" ? "R2C2" : company.Id == "B" ? "E1" : area.stringValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(company);
            }

            foreach (var obsolete in ObsoleteAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(obsolete) != null) AssetDatabase.DeleteAsset(obsolete);
            }
            EnsureAll(overwrite: true);
            ShelfPrefab(overwrite: true);
            ConstructionFencePrefab(overwrite: true);
            AssetDatabase.SaveAssets();

            SiteBuilder.BuildInternal();
        }

        /// <summary>현금 뭉치 1개 (초록 지폐 다발 + 띠).</summary>
        public static Transform CashBundlePrefab()
        {
            var path = $"{PrefabRoot}/CashBundle.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Transform>(path);
            if (existing != null) return existing;

            var root = new GameObject("CashBundle");
            BuildKit.Cube("Bills", root.transform, new Vector3(0f, 0.07f, 0f), new Vector3(0.5f, 0.14f, 0.28f), BuildKit.Mat("M_Cash", new Color(0.35f, 0.72f, 0.38f)));
            BuildKit.Cube("Band", root.transform, new Vector3(0f, 0.07f, 0f), new Vector3(0.1f, 0.15f, 0.29f), BuildKit.Mat("M_CashBand", new Color(0.95f, 0.9f, 0.6f)));

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<Transform>(path);
        }
    }
}
