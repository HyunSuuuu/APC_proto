using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Interaction;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>창고 전체의 선반 목록과 재고 집계. 씬에 하나 둔다.</summary>
    public sealed class Warehouse : MonoBehaviour
    {
        public static Warehouse Instance { get; private set; }

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

        /// <summary>그 품목 선반 중 빈자리가 있고 from에서 가장 가까운 것 (같은 색 선반이 여럿, GDD 7.3). 없으면 null.</summary>
        public Shelf NearestShelfWithSpace(ItemDefinition item, Vector3 from)
        {
            Shelf best = null;
            var bestDistance = float.MaxValue;
            foreach (var shelf in shelves)
            {
                if (shelf.Item != item || !shelf.HasSpace) continue;
                var d = (shelf.transform.position - from).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = shelf;
            }
            return best;
        }

        /// <summary>그 품목 선반이 하나라도 있나 (= 그 색 보관 구역이 지어졌나, GDD 6.3).</summary>
        public bool HasShelf(ItemDefinition item)
        {
            foreach (var shelf in shelves)
            {
                if (shelf.Item == item) return true;
            }
            return false;
        }

        /// <summary>보관 구역 보관함(선반 모델 4개 통합, M20-2)을 자리의 품목 · 용량으로 설정하고 등록한다 (팝 연출). ShelfSlot.Start가 부른다.</summary>
        public Shelf SetupShelf(ShelfSlot slot, Shelf shelf)
        {
            shelf.name = $"Shelf_{slot.Item.Id}";
            shelf.Setup(slot.Item, slot.Capacity > 0 ? slot.Capacity : GameManager.Instance.Database.Logistics.ShelfCapacity);
            slot.Occupant = shelf;
            shelves.Add(shelf);
            FacilityPop.Play(shelf.transform);
            return shelf;
        }

        // ── 선반 단계 (GDD 7.3, M16-3 · M20-2): 보관 모듈마다 1 → 2 → 3. 용량 = Shelf.CapacityAt → 구역 24 · 32 · 40, 랙 16 · 24 · 32
        readonly Dictionary<APC.Site.SiteModule, int> shelfStages = new();

        public int GetShelfStage(APC.Site.SiteModule module) => shelfStages.TryGetValue(module, out var stage) ? stage : 1;

        /// <summary>선반이 세워진 보관 모듈 (B책상 설비 탭 줄).</summary>
        public List<APC.Site.SiteModule> ShelfModules()
        {
            var modules = new List<APC.Site.SiteModule>();
            foreach (var shelf in shelves)
            {
                var module = shelf.GetComponentInParent<APC.Site.SiteModule>();
                if (module != null && !modules.Contains(module)) modules.Add(module);
            }
            return modules;
        }

        /// <summary>그 모듈의 첫 선반 (품목 · 기본 용량 표시용).</summary>
        public Shelf FirstShelfOf(APC.Site.SiteModule module)
        {
            foreach (var shelf in shelves)
            {
                if (shelf.GetComponentInParent<APC.Site.SiteModule>() == module) return shelf;
            }
            return null;
        }

        /// <summary>그 모듈 선반을 다음 단계로 (돈 · 최대 확인은 부르는 쪽). 그 모듈 선반 전부 용량이 바뀐다.</summary>
        public void RaiseShelfStage(APC.Site.SiteModule module)
        {
            var stage = GetShelfStage(module) + 1;
            shelfStages[module] = stage;
            foreach (var shelf in shelves)
            {
                if (shelf.GetComponentInParent<APC.Site.SiteModule>() != module) continue;
                shelf.SetCapacity(shelf.CapacityAt(stage));
                // 크기 0에서 키우는 FacilityPop은 쓰지 않는다 — 팝업으로 멈춘 동안 0에 머물러 그때 들어온 상자가 크기 0으로 붙음
                shelf.transform.DOKill(true);
                shelf.transform.DOPunchScale(Vector3.one * 0.12f, 0.35f, 6, 0.5f).SetLink(shelf.gameObject);
            }
        }

        readonly Dictionary<ItemDefinition, int> reservedShelfSpace = new();

        /// <summary>분류 알바가 채우러 오는 중인 선반 빈자리 (GDD 8.4 예약). 공용 보관소가 그만큼 덜 내준다.</summary>
        public int GetReservedShelfSpace(ItemDefinition item) => reservedShelfSpace.TryGetValue(item, out var n) ? n : 0;

        public void ReserveShelfSpace(ItemDefinition item, int count) => reservedShelfSpace[item] = GetReservedShelfSpace(item) + count;

        public void UnreserveShelfSpace(ItemDefinition item, int count) => reservedShelfSpace[item] = Mathf.Max(0, GetReservedShelfSpace(item) - count);

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
        /// <summary>선반(팔레트 랙 포함)에 올라가 있는 그 품목 수만 (상차 트럭 조건, 2026-11-19).</summary>
        public int GetShelfCount(ItemDefinition item)
        {
            var count = 0;
            foreach (var shelf in shelves)
            {
                if (shelf.Item == item) count += shelf.Count.CurrentValue;
            }
            return count;
        }

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

        /// <summary>재포장한 반품이 선반에 들어간 수. 1이 되는 순간 프로토타입 목표 달성 (GDD 1.3, 완료 표시는 2순위).</summary>
        public int RepackedShelved { get; private set; }

        /// <summary>선반 둘레 존이 재포장 상자를 넣었을 때 부른다.</summary>
        public void NotifyRepackedShelved()
        {
            RepackedShelved++;
            if (RepackedShelved == 1) Debug.Log("[Warehouse] 재포장한 반품이 처음으로 선반에 들어감 — 프로토타입 목표 달성 (GDD 1.3)");
        }

        /// <summary>D 가구(대형) 품목인가 (CompanyDefinition.IsLarge, GDD 5.1).</summary>
        public static bool IsLarge(ItemDefinition item)
        {
            var company = item != null && GameManager.Instance != null ? GameManager.Instance.Database.GetCompanyOf(item) : null;
            return company != null && company.IsLarge;
        }

        /// <summary>그 사람이 그 품목을 다룰 수 있는가: 지게차 알바는 D만, 나머지(플레이어 포함)는 D 말고만 (GDD 11.3, M11-8).</summary>
        public static bool CanHandle(Interactor who, ItemDefinition item) => who != null && IsLarge(item) == who.Forklift;

        /// <summary>D 하차 트럭에서 더 꺼낼 수 있는 수 = 팔레트 랙 빈자리 − 트럭에서 꺼내 든 수. 예외(바로 상차)면 상차 수요까지 (GDD 11.3).</summary>
        public int LargeRoom(ItemDefinition item)
        {
            var room = GetShelfFreeSpace(item) - CountCarried(item, CargoOrigin.Truck) - CountCarried(item, CargoOrigin.Shelf); // 꺼내 든 상차용 D는 되돌릴 자리 (M17-1)
            if (DirectLoad(item)) room = Mathf.Max(room, Orders.OrderService.Instance.GetDemand(item) - CountCarried(item, CargoOrigin.Truck));
            return room;
        }

        /// <summary>예외: 팔레트 랙에 D가 없고 상차 트럭이 D를 요구 중 → 하차 트럭에서 꺼낸 D를 랙을 거치지 않고 바로 상차 (GDD 11.3).</summary>
        public bool DirectLoad(ItemDefinition item)
        {
            var orders = Orders.OrderService.Instance;
            return orders != null && GetStock(item) == 0 && orders.GetDemand(item) > 0;
        }

        /// <summary>플레이어 · 알바가 지금 들고 있는 화물 + 벨트 위 화물(GDD 7.10) 중 조건에 맞는 수.</summary>
        public static int CountCarried(ItemDefinition item, CargoOrigin origin)
        {
            // 인덱스로 돈다: IReadOnlyList foreach는 열거자 박싱 — 빈 상차 칸마다 매 프레임 불려 GC (2026-11-25)
            return ConveyorBelt.CountOnBelts(item, origin) + CarriedIndex.Count(item, origin); // 손에 든 수는 한 번 세어 둔 값 (2026-11-26)
        }
    }
}
