using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>입고 트럭 옆 픽업 존 (GDD 7.1). 정차한 트럭에서 화물을 하나씩 꺼낸다. 받는 화물은 없다.</summary>
    public sealed class TruckPickupZone : CargoZone
    {
        [SerializeField] InboundBay bay;

        protected override CargoOrigin GiveOrigin => CargoOrigin.Truck;

        public InboundBay Bay => bay;

        /// <summary>예약하지 않은 화물이 있는가 (알바용, 다른 알바의 예약은 뺀다).</summary>
        public override bool HasCargoFor(Interactor who) => bay.DockedTruck != null && bay.DockedTruck.Available > 0;

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            var truck = bay.DockedTruck;
            cargo = truck != null ? truck.TakeCargo() : null;
            return cargo != null;
        }
    }
}
