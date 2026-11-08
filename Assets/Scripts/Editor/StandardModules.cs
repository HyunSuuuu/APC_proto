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

        /// <summary>SiteLayout id → 기본 모듈 (처음 배치할 때 config에 채운다). 같은 모듈을 여러 칸에 재사용한다.</summary>
        public static readonly IReadOnlyDictionary<string, (string asset, int rotation)> DefaultAssignments =
            new Dictionary<string, (string, int)>
            {
                ["R1C0"] = ("Module_LotInbound", 0),
                ["R1C1"] = ("Module_Storage", 0),
                ["R1C2"] = ("Module_ShelfArea_A", 0),
                ["R1C3"] = ("Module_OutboundArea", 0),
                ["R1C4"] = ("Module_LotOutbound", 180),
                ["R2C1"] = ("Module_Office", 0),
                ["E1"] = ("Module_LotInbound", 0),
                ["E5"] = ("Module_LotOutbound", 180),
                ["E3"] = ("Module_Packing", 0),
                ["E2"] = ("Module_ShelfArea", 0),
                ["E4"] = ("Module_ShelfArea", 0),
                ["E6"] = ("Module_Returns", 0),
            };

        static readonly (string asset, Action<GameObject> build)[] Builders =
        {
            ("Module_LotInbound", root => BuildLot(root, inbound: true)),
            ("Module_LotOutbound", root => BuildLot(root, inbound: false)),
            ("Module_Storage", BuildStorage),
            ("Module_ShelfArea_A", root => BuildShelfArea(root, "선반 구역 A")),
            ("Module_ShelfArea", root => BuildShelfArea(root, "선반 구역")),
            ("Module_Office", BuildOffice),
            ("Module_OutboundArea", root => Label(root, "출고 구역 (통로)")),
            ("Module_Packing", root => Label(root, "포장 구역 (M7)")),
            ("Module_Returns", root => Label(root, "반품 구역 (M9)")),
            // 빈 타일 (바닥만). 다른 배치와 같이 해금되게 묶어 ㄱ자 같은 구역을 만든다 (SiteLayout.unlockWith).
            ("Module_Empty", _ => { }),
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
        /// 주차장 (1칸). 바깥쪽(−x)에서 나타난 트럭이 후진으로 곧장 주차하고, 뒷문 앞(+x)에 하차 · 상차 존이 있다.
        /// 상차 주차장은 같은 모양을 180° 돌려 붙인다.
        /// </summary>
        static void BuildLot(GameObject root, bool inbound)
        {
            var content = ModuleFactory.Content(root);
            // 트럭 루트 기준 앞 +2.6 / 뒤 −2.2 → 주차 x −1.0이면 앞 −3.6, 뒷문 +1.2
            var park = new Vector3(-1f, 0f, 0f);
            var dock = new Vector3(3.6f, 0f, 0f);

            BuildKit.Cube("Asphalt", content, new Vector3(-1.7f, 0.006f, 0f), new Vector3(6.6f, 0.012f, 3.6f), BuildKit.Asphalt);
            foreach (var side in new[] { -1.85f, 1.85f })
                BuildKit.Cube("Line", content, new Vector3(-1.7f, 0.014f, side), new Vector3(6.6f, 0.012f, 0.12f), BuildKit.Mat("M_Line", Color.white));

            var bayGo = new GameObject(inbound ? "InboundBay" : "OutboundBay");
            bayGo.transform.SetParent(content, false);
            var parkPoint = new GameObject("ParkPoint").transform;
            parkPoint.SetParent(bayGo.transform, false);
            parkPoint.SetPositionAndRotation(park, Quaternion.LookRotation(Vector3.left));
            var spawnPoint = new GameObject("SpawnPoint").transform;
            spawnPoint.SetParent(bayGo.transform, false);
            spawnPoint.SetPositionAndRotation(new Vector3(-12f, 0f, 0f), Quaternion.LookRotation(Vector3.left));

            if (inbound)
            {
                var bay = bayGo.AddComponent<InboundBay>();
                BuildKit.SetRef(bay, "truckPrefab", TruckPrefab());
                BuildKit.SetRef(bay, "parkPoint", parkPoint);
                BuildKit.SetRef(bay, "spawnPoint", spawnPoint);
                var zone = BuildKit.Zone<TruckPickupZone>(content, "PickupZone", dock, new Vector2(2.2f, 3f),
                    new Color(0.85f, 0.75f, 0.4f), new Color(1f, 0.85f, 0.3f), "하차");
                BuildKit.SetRef(zone, "bay", bay);
                Label(root, "하차 주차장");
            }
            else
            {
                var bay = bayGo.AddComponent<OutboundBay>();
                BuildKit.SetRef(bay, "truckPrefab", OutboundTruckPrefab());
                BuildKit.SetRef(bay, "parkPoint", parkPoint);
                BuildKit.SetRef(bay, "spawnPoint", spawnPoint);
                var zone = BuildKit.Zone<TruckLoadZone>(content, "LoadZone", dock, new Vector2(2.2f, 3f),
                    new Color(0.85f, 0.65f, 0.45f), new Color(1f, 0.6f, 0.25f), "상차");
                BuildKit.SetRef(zone, "bay", bay);
                // 현금 존: 상차 존 바로 옆 (GDD 7.6 확정). 상차 존 글자(−z)의 반대쪽.
                BuildKit.SetRef(bay, "cashZone", BuildCashZone(content, dock + new Vector3(0f, 0f, 3.3f)));
                Label(root, "상차 주차장");
            }
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

        /// <summary>공용 보관소: [내려놓기] [팔레트] [집기 (선반 쪽, +x)].</summary>
        static void BuildStorage(GameObject root)
        {
            var content = ModuleFactory.Content(root);
            var storage = content.gameObject.AddComponent<SharedStorage>();
            var pallet = new Vector3(0.3f, 0f, 0f);
            BuildKit.Cube("Pallet", content, pallet + new Vector3(0f, 0.05f, 0f), new Vector3(3f, 0.1f, 2.6f), BuildKit.Mat("M_Pallet", new Color(0.6f, 0.45f, 0.28f)));
            var pileRoot = new GameObject("Pile").transform;
            pileRoot.SetParent(content, false);
            pileRoot.position = pallet + new Vector3(0f, 0.1f, 0f);
            BuildKit.SetRef(storage, "pileRoot", pileRoot);
            BuildKit.SetRef(storage, "countLabel", BuildKit.BillboardLabel(content, "0/20", pallet + new Vector3(0f, 2.2f, 0f), 7f));

            var zoneSize = new Vector2(2f, 2.8f);
            var drop = BuildKit.Zone<StorageDropZone>(content, "DropZone", pallet + new Vector3(-3.3f, 0f, 0f), zoneSize,
                new Color(0.5f, 0.72f, 0.5f), new Color(0.35f, 0.95f, 0.35f), "내려놓기");
            var pick = BuildKit.Zone<StoragePickZone>(content, "PickZone", pallet + new Vector3(3.3f, 0f, 0f), zoneSize,
                new Color(0.5f, 0.62f, 0.8f), new Color(0.35f, 0.7f, 1f), "집기");
            BuildKit.SetRef(drop, "storage", storage);
            BuildKit.SetRef(pick, "storage", storage);
            Label(root, "공용 보관소");
        }

        /// <summary>선반 자리 6개 (2열 × 3행). 앞(−x)이 적재 존, 뒤(+x)가 수거 존. 회사 하나의 선반(품목당 1개)이 들어간다.</summary>
        static void BuildShelfArea(GameObject root, string title)
        {
            var content = ModuleFactory.Content(root);
            var offsets = new[]
            {
                new Vector3(-2.4f, 0f, 3.2f), new Vector3(-2.4f, 0f, 0f), new Vector3(-2.4f, 0f, -3.2f), new Vector3(2.4f, 0f, 3.2f),
                new Vector3(2.4f, 0f, 0f), new Vector3(2.4f, 0f, -3.2f),
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

        /// <summary>사무실. 왼쪽 가장자리는 아래 칸 하차 베이(E1)의 하차 존과 가까우므로 시설은 오른쪽에 둔다.</summary>
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

            var root = new GameObject("Shelf");
            var metal = BuildKit.Mat("M_Shelf", new Color(0.35f, 0.45f, 0.6f));
            foreach (var x in new[] { -0.37f, 0.37f })
            foreach (var z in new[] { -1.32f, 1.32f })
                BuildKit.Cube("Post", root.transform, new Vector3(x, 0.85f, z), new Vector3(0.06f, 1.7f, 0.06f), metal);
            foreach (var y in new[] { 0.1f, 0.9f, 1.7f })
                BuildKit.Cube("Board", root.transform, new Vector3(0f, y, 0f), new Vector3(0.8f, 0.05f, 2.7f), metal);
            var icon = BuildKit.Cube("ItemIcon", root.transform, new Vector3(0f, 1.76f, 0f), new Vector3(0.6f, 0.06f, 0.6f), BuildKit.Mat("M_Tape", Color.white));
            var boxRoot = new GameObject("Boxes").transform;
            boxRoot.SetParent(root.transform, false);
            var label = BuildKit.BillboardLabel(root.transform, "0/10", new Vector3(0f, 2.4f, 0f), 6f);

            var zone = BuildKit.Zone<ShelfLoadZone>(root.transform, "LoadZone", new Vector3(-1.25f, 0f, 0f), new Vector2(1.6f, 2.6f),
                new Color(0.5f, 0.62f, 0.8f), new Color(0.35f, 0.7f, 1f), null);
            var pick = BuildKit.Zone<ShelfPickZone>(root.transform, "PickZone", new Vector3(1.25f, 0f, 0f), new Vector2(1.6f, 2.6f),
                new Color(0.85f, 0.65f, 0.45f), new Color(1f, 0.6f, 0.25f), null);

            var shelf = root.AddComponent<Shelf>();
            BuildKit.SetRef(shelf, "boxRoot", boxRoot);
            BuildKit.SetRef(shelf, "iconRenderer", icon.GetComponent<Renderer>());
            BuildKit.SetRef(shelf, "countLabel", label);
            BuildKit.SetRef(zone, "shelf", shelf);
            BuildKit.SetRef(pick, "shelf", shelf);

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
