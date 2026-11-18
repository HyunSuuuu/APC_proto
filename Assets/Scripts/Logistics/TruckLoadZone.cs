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

        public OutboundBay Bay => bay;

        protected override bool CanReceive(Cargo cargo, Interactor who)
        {
            var truck = bay.DockedTruck;
            return truck != null
                   && (cargo.Origin == CargoOrigin.Shelf || (who.Forklift && cargo.Origin == CargoOrigin.Truck && CanLoadDirect(cargo.Item))) // 예외: 랙이 비면 바로 상차 (M11-8)
                   && cargo.State == CargoState.Normal
                   && truck.Order.GetRemaining(cargo.Item) > 0;
        }

        /// <summary>
        /// 지게차가 트럭에서 꺼낸 D를 바로 실어도 되는가: 랙이 비어 있거나(예외), 랙에 빈자리가 없을 때.
        /// 뒤 조건 (2026-11-19 버그): 랙이 비어 4개를 꺼내 와 3개를 랙에 넣으면 '랙이 빔'이 꺼져 남은 1개를 어디에도 못 넣고 멈췄다.
        /// </summary>
        static bool CanLoadDirect(Data.ItemDefinition item)
            => Warehouse.Instance.DirectLoad(item) || Warehouse.Instance.GetShelfFreeSpace(item) <= 0;

        protected override void Receive(Cargo cargo, Interactor who) => bay.DockedTruck.Receive(cargo);
    }
}
