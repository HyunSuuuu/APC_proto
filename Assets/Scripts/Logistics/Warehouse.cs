using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>창고 전체의 선반 목록과 재고 집계. 씬에 하나 둔다.</summary>
    public sealed class Warehouse : MonoBehaviour
    {
        public static Warehouse Instance { get; private set; }

        [SerializeField] Shelf shelfPrefab;
        [SerializeField, Tooltip("선반을 세울 순서대로")] List<ShelfSlot> shelfSlots = new();

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

        /// <summary>열린 빈 자리 중 첫 자리에 선반을 세운다 (팝 연출). 자리가 없으면 null.</summary>
        public Shelf BuildShelf(ItemDefinition item)
        {
            var level = GameManager.Instance.Level.Level.CurrentValue;
            foreach (var slot in shelfSlots)
            {
                if (slot.Occupant != null || slot.UnlockLevel > level) continue;

                var shelf = Instantiate(shelfPrefab, slot.transform.position, slot.transform.rotation, slot.transform);
                shelf.name = $"Shelf_{item.Id}";
                shelf.Setup(item, GameManager.Instance.Database.Logistics.ShelfCapacity);
                slot.Occupant = shelf;
                shelves.Add(shelf);
                FacilityPop.Play(shelf.transform);
                return shelf;
            }

            Debug.LogWarning($"[Warehouse] {item.Id} 선반을 세울 빈 자리가 없습니다.", this);
            return null;
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
