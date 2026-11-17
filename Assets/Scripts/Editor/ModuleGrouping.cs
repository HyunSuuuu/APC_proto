using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// 모듈 프리팹 자식 묶기 (M14-5): `Content` 아래 흩어진 자식을 기능 묶음마다 빈 부모 하나로 모은다 (월드 위치 그대로).
    /// 부모 하나만 옮기면 묶음 전체가 같이 간다. 이미 묶였으면 건너뛴다 — 여러 번 실행해도 같다.
    /// 런타임 코드는 자식을 이름 경로로 찾지 않으므로 묶어도 참조가 깨지지 않는다.
    /// </summary>
    public static class ModuleGrouping
    {
        /// <summary>모듈 프리팹 → (묶음 이름 → 그 묶음에 넣을 Content 바로 아래 자식 이름들).</summary>
        static readonly Dictionary<string, (string group, string[] children)[]> Rules = new()
        {
            ["Module_LotInbound"] = Lot(true),
            ["Module_LotInbound_M"] = Lot(true),
            ["Module_LotOutbound"] = Lot(false),
            ["Module_LotOutbound_M"] = Lot(false),
            ["Module_Returns"] = new[]
            {
                ("ReturnShelf", new[] { "ReturnRack", "ReturnBin", "ReturnBinZone" }),
                ("RepackOutputArea", new[] { "OutputPallet", "RepackOutput", "RepackOutputZone" }),
            },
        };

        /// <summary>하차 = 상자 공간(UnloadSpace) + 트럭 자리(TruckDock), 상차 = 트럭 자리만 (돈 존 CashZone은 이미 한 덩어리).</summary>
        static (string, string[])[] Lot(bool inbound)
        {
            var dock = new List<string> { "Asphalt", "Line", "DoorPost", "DoorLintel" };
            dock.AddRange(inbound ? new[] { "InboundBay", "PickupZone" } : new[] { "OutboundBay", "LoadZone" });
            var groups = new List<(string, string[])> { ("TruckDock", dock.ToArray()) };
            if (inbound) groups.Add(("UnloadSpace", new[] { "Zone", "Pile", "Pallet", "CountLabel" }));
            return groups.ToArray();
        }

        [MenuItem("APC/Art/Group Module Children (모듈 자식 기능별로 묶기)")]
        static void GroupAll()
        {
            var log = new System.Text.StringBuilder("[ModuleGrouping]\n");
            foreach (var pair in Rules)
            {
                var path = $"{ModuleFactory.ModuleFolder}/{pair.Key}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    log.AppendLine($"{pair.Key}: 없음");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                var content = ModuleFactory.Content(root);
                var moved = 0;
                foreach (var (group, names) in pair.Value)
                {
                    var parent = content.Find(group);
                    if (parent == null)
                    {
                        parent = new GameObject(group).transform;
                        parent.SetParent(content, false);
                    }
                    // 같은 이름이 여러 개(Line · DoorPost)여도 모두 옮긴다
                    var targets = new List<Transform>();
                    foreach (Transform child in content)
                    {
                        if (System.Array.IndexOf(names, child.name) >= 0) targets.Add(child);
                    }
                    foreach (var child in targets) child.SetParent(parent, worldPositionStays: true);
                    moved += targets.Count;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                log.AppendLine($"{pair.Key}: {moved}개 옮김");
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }
    }
}
