using APC.Interaction;
using APC.Orders;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 선반 수거 존 (뒤, GDD 7.4). **수요만큼만** 집는다 — 정차한 트럭 주문의 남은 수량 − 이미 들고 있는 수 (GDD 8.4).
    /// 그래서 트럭이 없으면 집지 않고, 필요 이상으로 꺼내 들고 다니는 일이 없다. 받지는 않는다.
    /// </summary>
    public sealed class ShelfPickZone : CargoZone
    {
        [SerializeField] Shelf shelf;

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            var orders = OrderService.Instance;
            if (orders == null || shelf.Item == null || orders.GetDemand(shelf.Item) <= 0) return false;

            cargo = shelf.TakeOne(); // 출처는 Shelf 그대로 → 상차 존이 받는다
            return cargo != null;
        }
    }
}
