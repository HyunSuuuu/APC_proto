using APC.Interaction;
using APC.Orders;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 정사각형 선반 둘레 존 (GDD 7.3, 7.4). 근처에서 멈추면 내려놓기를 먼저 하고(그 품목 · 공용 보관소에서 집은 화물),
    /// 내려놓을 것이 없으면 **주문 수요만큼만** 집는다 (정차 트럭 남은 수 − 이미 들고 있는 수, GDD 8.4).
    /// </summary>
    public sealed class ShelfZone : CargoZone
    {
        [SerializeField] Shelf shelf;

        public Shelf Shelf => shelf;

        public override bool HasCargoFor(Interactor who)
        {
            var orders = OrderService.Instance;
            return orders != null && shelf.Item != null && shelf.Count.CurrentValue > 0 && orders.GetDemand(shelf.Item) > 0;
        }

        protected override bool CanReceive(Cargo cargo, Interactor who)
            => cargo.Item == shelf.Item
               && cargo.Origin == CargoOrigin.Storage
               && cargo.State == CargoState.Normal
               && shelf.HasSpace;

        protected override void Receive(Cargo cargo, Interactor who)
        {
            cargo.Origin = CargoOrigin.Shelf;
            shelf.Store(cargo);
            if (cargo.Repacked && Warehouse.Instance != null) Warehouse.Instance.NotifyRepackedShelved();
        }

        protected override CargoOrigin GiveOrigin => CargoOrigin.Shelf;

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            var orders = OrderService.Instance;
            if (orders == null || shelf.Item == null || orders.GetDemand(shelf.Item) <= 0) return false;

            cargo = shelf.TakeOne(); // 출처는 Shelf 그대로 → 상차 존만 받는다
            return cargo != null;
        }
    }
}
