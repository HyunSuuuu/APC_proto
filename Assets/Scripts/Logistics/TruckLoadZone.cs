using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 출고 트럭 뒷문 앞 상차 존 (GDD 7.6). 선반에서 꺼낸 화물 중 **정차한 트럭 주문에 남은 품목만**, 남은 수량만큼 받는다.
    /// 받기만 하고 주지는 않는다.
    /// </summary>
    public sealed class TruckLoadZone : CargoZone
    {
        [SerializeField] OutboundBay bay;

        protected override bool CanReceive(Cargo cargo, Interactor who)
        {
            var truck = bay.DockedTruck;
            return truck != null
                   && cargo.Origin == CargoOrigin.Shelf
                   && cargo.State == CargoState.Normal
                   && truck.Order.GetRemaining(cargo.Item) > 0;
        }

        protected override void Receive(Cargo cargo, Interactor who) => bay.DockedTruck.Receive(cargo);
    }
}
