using System.Collections.Generic;
using APC.Core;
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

        readonly ReactiveProperty<int> count = new(0);
        CargoPile pile;
        int capacity;

        public ReadOnlyReactiveProperty<int> Count => count;
        public int Capacity => capacity;
        public IReadOnlyList<Cargo> Items => pile.Items;

        void Awake() => pile = new CargoPile(pileRoot, layout.GetLocalPosition);

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
            var warehouse = Warehouse.Instance;
            for (var i = pile.Count - 1; i >= 0; i--)
            {
                var candidate = pile.Items[i];
                if (candidate.State != CargoState.Normal) continue;
                if (warehouse.GetShelfFreeSpace(candidate.Item) - Warehouse.CountCarried(candidate.Item, CargoOrigin.Storage) <= 0) continue;

                var cargo = pile.RemoveAt(i);
                cargo.Origin = CargoOrigin.Storage;
                count.Value = pile.Count;
                return cargo;
            }
            return null;
        }
    }
}
