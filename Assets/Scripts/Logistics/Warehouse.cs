using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Interaction;
using APC.Site;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>창고 전체의 선반 목록과 재고 집계. 씬에 하나 둔다.</summary>
    public sealed class Warehouse : MonoBehaviour
    {
        public static Warehouse Instance { get; private set; }

        [SerializeField] Shelf shelfPrefab;

        readonly List<Shelf> shelves = new();

        public IReadOnlyList<Shelf> Shelves => shelves;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[Warehouse] 씬에 Warehouse가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool HasShelf(ItemDefinition item) => FindShelf(item) != null;

        public Shelf FindShelf(ItemDefinition item)
        {
            foreach (var shelf in shelves)
            {
                if (shelf.Item == item) return shelf;
            }
            return null;
        }

        /// <summary>
        /// 선반 구역(areaId = SiteLayout 배치 id)의 열린 빈 자리 중 순서가 가장 빠른 자리에 선반을 세운다 (팝 연출).
        /// areaId가 비어 있으면 아무 구역이나. 자리가 없으면 null.
        /// 품목당 선반은 1개뿐이다 — 이미 있으면 새로 만들지 않고 그 선반을 돌려준다.
        /// </summary>
        public Shelf BuildShelf(ItemDefinition item, string areaId)
        {
            var existing = FindShelf(item);
            if (existing != null)
            {
                Debug.LogWarning($"[Warehouse] {item.Id} 선반이 이미 있습니다 (품목당 1개).", this);
                return existing;
            }

            var slot = FindFreeSlot(areaId);
            if (slot != null)
            {
                var shelf = Instantiate(shelfPrefab, slot.transform.position, slot.transform.rotation, slot.transform);
                shelf.name = $"Shelf_{item.Id}";
                shelf.Setup(item, GameManager.Instance.Database.Logistics.ShelfCapacity);
                slot.Occupant = shelf;
                shelves.Add(shelf);
                FacilityPop.Play(shelf.transform);
                return shelf;
            }

            Debug.LogWarning($"[Warehouse] {item.Id} 선반을 세울 빈 자리가 '{areaId}'에 없습니다.", this);
            return null;
        }

        /// <summary>그 선반 구역의 지금 쓸 수 있는 빈 자리 수 (계약 가능 여부 확인용).</summary>
        public int CountFreeSlots(string areaId)
        {
            var level = GameManager.Instance.Level.Level.CurrentValue;
            var count = 0;
            foreach (var slot in ShelfSlot.All)
            {
                if (slot.Occupant == null && slot.UnlockLevel <= level && InArea(slot, areaId)) count++;
            }
            return count;
        }

        ShelfSlot FindFreeSlot(string areaId)
        {
            var level = GameManager.Instance.Level.Level.CurrentValue;
            ShelfSlot best = null;
            foreach (var candidate in ShelfSlot.All)
            {
                if (candidate.Occupant != null || candidate.UnlockLevel > level || !InArea(candidate, areaId)) continue;
                if (best == null || candidate.Order < best.Order) best = candidate; // 같은 순서면 먼저 붙은 모듈
            }
            return best;
        }

        /// <summary>자리가 그 배치(모듈) 안에 있는가. areaId가 비어 있으면 어디든 된다.</summary>
        static bool InArea(ShelfSlot slot, string areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return true;
            var grid = SiteGrid.Instance;
            if (grid == null) return false;
            foreach (var entry in grid.Entries)
            {
                if (entry.id == areaId) return entry.instance != null && slot.transform.IsChildOf(entry.instance.transform);
            }
            return false;
        }

        /// <summary>그 품목 선반들의 빈자리 합.</summary>
        public int GetShelfFreeSpace(ItemDefinition item)
        {
            var free = 0;
            foreach (var shelf in shelves)
            {
                if (shelf.Item == item) free += shelf.FreeSpace;
            }
            return free;
        }

        /// <summary>재고 = 선반 + 공용 보관소에 있는 그 품목 수 (교착 방지 가중치용, GDD 7.8).</summary>
        public int GetStock(ItemDefinition item)
        {
            var stock = 0;
            foreach (var shelf in shelves)
            {
                if (shelf.Item == item) stock += shelf.Count.CurrentValue;
            }
            foreach (var storage in SharedStorage.All) stock += storage.CountOf(item);
            return stock;
        }

        /// <summary>플레이어 · 알바가 지금 들고 있는 화물 중 조건에 맞는 수.</summary>
        public static int CountCarried(ItemDefinition item, CargoOrigin origin)
        {
            var total = 0;
            foreach (var who in Interactor.All)
            {
                if (who.Stack == null) continue;
                foreach (var cargo in who.Stack.Items)
                {
                    if (cargo.Item == item && cargo.Origin == origin) total++;
                }
            }
            return total;
        }
    }
}
