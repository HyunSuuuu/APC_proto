using System.Collections.Generic;
using APC.Core;
using APC.Data;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 공용 보관소 본체 (GDD 7.2). 트럭에서 꺼낸 화물만 받고, 품목을 섞어 바닥 팔레트에 쌓는다.
    /// 존은 따로 있다 — 내려놓기 존(트럭 쪽) / 집기 존(선반 쪽).
    /// </summary>
    public sealed class SharedStorage : MonoBehaviour
    {
        [SerializeField] Transform pileRoot;
        [SerializeField] CargoGridLayout layout = new(5, 4, 0.6f, 0.5f);
        [SerializeField] TMP_Text countLabel;

        static readonly List<SharedStorage> Registered = new();

        readonly ReactiveProperty<int> count = new(0);
        CargoPile pile;
        int capacity;

        /// <summary>붙어 있는 모든 공용 보관소 (재고 집계용). 모듈 안에 있으므로 스스로 등록한다.</summary>
        public static IReadOnlyList<SharedStorage> All => Registered;

        public ReadOnlyReactiveProperty<int> Count => count;
        public int Capacity => capacity;
        public IReadOnlyList<Cargo> Items => pile.Items;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        void Awake() => pile = new CargoPile(pileRoot, layout.GetLocalPosition);

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);

        /// <summary>그 품목 보관 수.</summary>
        public int CountOf(ItemDefinition item)
        {
            var n = 0;
            foreach (var cargo in pile.Items)
            {
                if (cargo.Item == item) n++;
            }
            return n;
        }

        void Start()
        {
            capacity = GameManager.Instance.Database.Logistics.SharedStorageCapacity;
            count.Subscribe(n => countLabel.text = $"{n}/{capacity}").AddTo(this);
        }

        void OnDestroy() => count.Dispose();

        /// <summary>트럭에서 꺼낸 화물이고 빈자리가 있으면 받는다.</summary>
        public bool CanAccept(Cargo cargo) => cargo.Origin == CargoOrigin.Truck && pile.Count < capacity;

        public void Store(Cargo cargo)
        {
            pile.Add(cargo);
            count.Value = pile.Count;
        }

        /// <summary>
        /// 선반에 빈자리가 남은 품목만 꺼낸다 (선반 빈자리 − 이미 들고 가는 수, GDD 7.2 확정 규칙).
        /// 최근에 들어온 것부터 찾는다. 없으면 null.
        /// </summary>
        public Cargo TakeForShelves()
        {
            var i = FindForShelves();
            if (i < 0) return null;

            var cargo = pile.RemoveAt(i);
            cargo.Origin = CargoOrigin.Storage;
            count.Value = pile.Count;
            return cargo;
        }

        /// <summary>TakeForShelves로 꺼낼 화물이 있는가 (꺼내지 않음, 분류 알바용).</summary>
        public bool HasCargoForShelves() => FindForShelves() >= 0;

        /// <summary>빈자리가 남은 공용 보관소가 있는가 (하차 알바가 트럭에서 집을 가치가 있나).</summary>
        public static bool AnyHasSpace()
        {
            foreach (var storage in Registered)
            {
                if (storage.pile.Count < storage.capacity) return true;
            }
            return false;
        }

        /// <summary>
        /// 분류 알바 예약용: 지금 집으면 선반에 갈 화물을 품목별로 최대 max개까지 센다 (꺼내지 않음).
        /// TakeForShelves와 같은 순서(최근 것부터) · 같은 조건.
        /// </summary>
        public Dictionary<ItemDefinition, int> PlanForShelves(int max)
        {
            var plan = new Dictionary<ItemDefinition, int>();
            var total = 0;
            for (var i = pile.Count - 1; i >= 0 && total < max; i--)
            {
                var item = pile.Items[i].Item;
                if (pile.Items[i].State != CargoState.Normal) continue;
                plan.TryGetValue(item, out var taken);
                if (ShelfAllowance(item) - taken <= 0) continue;
                plan[item] = taken + 1;
                total++;
            }
            return plan;
        }

        int FindForShelves()
        {
            for (var i = pile.Count - 1; i >= 0; i--)
            {
                var candidate = pile.Items[i];
                if (candidate.State == CargoState.Normal && ShelfAllowance(candidate.Item) > 0) return i;
            }
            return -1;
        }

        /// <summary>그 품목을 선반에 더 보낼 수 있는 수 = 선반 빈자리 − 이미 들고 가는 수 − 분류 알바 예약.</summary>
        static int ShelfAllowance(ItemDefinition item)
        {
            var warehouse = Warehouse.Instance;
            return warehouse.GetShelfFreeSpace(item) - Warehouse.CountCarried(item, CargoOrigin.Storage) - warehouse.GetReservedShelfSpace(item);
        }
    }
}
