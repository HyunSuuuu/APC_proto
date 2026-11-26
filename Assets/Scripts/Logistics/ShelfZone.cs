using APC.Interaction;
using APC.Orders;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 정사각형 선반 둘레 존 (GDD 7.3, 7.4). 근처에서 멈추면 내려놓기를 먼저 하고(그 품목 · 공용 보관소에서 집은 화물),
    /// 내려놓을 것이 없으면 **주문 수요만큼만** 집는다 (정차 트럭 남은 수 − 이미 들고 있는 수, GDD 8.4).
    /// 상차 시간이 아니면(2파트가 끝나 회차한 뒤) 선반에서 꺼내 들고 있던 상자도 되돌려 받는다 (M17-1, GDD 7.6). 그 자리는 들고 있는 동안 잡혀 있다(`SharedStorage.ShelfAllowance`).
    /// </summary>
    public sealed class ShelfZone : CargoZone
    {
        [SerializeField] Shelf shelf;

        public Shelf Shelf => shelf;

        public override bool HasCargoFor(Interactor who)
        {
            var orders = OrderService.Instance;
            return orders != null && shelf.Item != null && Warehouse.CanHandle(who, shelf.Item) && shelf.Count.CurrentValue > 0 && orders.GetDemand(shelf.Item) + who.Reserved(this, shelf.Item) > 0;
        }

        protected override bool CanReceive(Cargo cargo, Interactor who)
            => cargo.Item == shelf.Item
               && (cargo.Origin == (who.Forklift ? CargoOrigin.Truck : CargoOrigin.Storage) // 지게차는 트럭에서 바로 팔레트 랙으로 (M11-8)
                   || (cargo.Origin == CargoOrigin.Shelf && !Economy.PartService.OutboundOpen)) // 상차 시간이 아니면 꺼낸 상자를 되돌림 (M17-1, GDD 7.6)
               && cargo.State == CargoState.Normal
               && shelf.HasSpace;

        protected override void Receive(Cargo cargo, Interactor who)
        {
            cargo.Origin = CargoOrigin.Shelf;
            shelf.Store(cargo);
            if (cargo.Repacked && Warehouse.Instance != null) Warehouse.Instance.NotifyRepackedShelved();
        }

        /// <summary>
        /// 하나도 못 넣었을 때 (플레이어만, GDD 7.3): 이 색 상자를 들었는데 선반이 꽉 찼으면 '가득', 그 밖의 상자면 '넣을 수 없음'.
        /// 선반에서 꺼낸 상자(상차로 가는 중)는 세지 않는다.
        /// </summary>
        protected override void OnNothingDropped(Interactor who)
        {
            var sameColor = false;
            var other = false;
            foreach (var cargo in who.Stack.Items)
            {
                if (cargo.Origin == CargoOrigin.Shelf) continue;
                if (cargo.Item == shelf.Item && cargo.Origin == CargoOrigin.Storage && cargo.State == CargoState.Normal) sameColor = true;
                else other = true;
            }
            if (!sameColor && !other) return;
            var anchor = shelf.CountLabel != null ? shelf.CountLabel.transform.position : shelf.transform.position;
            if (sameColor) Notice(who, shelf.CountLabel, anchor + Vector3.up * 0.5f, "가득");
            else NoticeCannotDrop(who, anchor + Vector3.up * 0.5f);
        }

        protected override CargoOrigin GiveOrigin => CargoOrigin.Shelf;

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            var orders = OrderService.Instance;
            if (orders == null || shelf.Item == null || !Warehouse.CanHandle(who, shelf.Item) || orders.GetDemand(shelf.Item) + who.Reserved(this, shelf.Item) <= 0) return false;

            cargo = shelf.TakeOne(); // 출처는 Shelf 그대로 → 상차 존만 받는다
            return cargo != null;
        }
    }
}
