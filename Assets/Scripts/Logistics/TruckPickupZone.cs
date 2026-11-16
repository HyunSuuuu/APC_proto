using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 입고 트럭 옆 픽업 존 (GDD 7.1). 정차한 트럭에서 화물을 하나씩 꺼낸다. 받는 화물은 없다.
    /// 이 하차 칸 공간이 차면 (공간 수 + 이 칸 트럭에서 꺼내 든 수 + 알바 예약 = 용량) 플레이어도 알바도 못 꺼낸다 (GDD 7.2).
    /// </summary>
    public sealed class TruckPickupZone : CargoZone
    {
        [SerializeField] InboundBay bay;

        protected override CargoOrigin GiveOrigin => CargoOrigin.Truck;

        public InboundBay Bay => bay;

        /// <summary>이 칸 공간에 더 꺼내 올 수 있는 수 (다른 알바의 트럭 예약까지 뺌).</summary>
        public int Room
        {
            get
            {
                var storage = bay.Storage;
                if (storage == null) return 0;
                var reserved = bay.DockedTruck != null ? bay.DockedTruck.Reserved : 0;
                return storage.Capacity - storage.Count.CurrentValue - CountHeadingTo(storage) - reserved;
            }
        }

        /// <summary>예약하지 않은 화물이 있고 공간에 자리가 있는가 (알바용, 다른 알바의 예약은 뺀다).</summary>
        public override bool HasCargoFor(Interactor who) => bay.DockedTruck != null && bay.DockedTruck.Available > 0 && Room > 0;

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            var truck = bay.DockedTruck;
            if (truck == null || !truck.HasCargo) return false;
            if (Room <= 0)
            {
                Notice(who, bay.Storage.CountLabel, truck.transform.position + Vector3.up * 3.5f, "공간 가득");
                return false;
            }

            cargo = truck.TakeCargo();
            if (cargo != null) cargo.Home = bay.Storage;
            return cargo != null;
        }

        /// <summary>트럭에서 꺼내 그 공간으로 들고 가는 중인 화물 수 (플레이어 · 알바).</summary>
        static int CountHeadingTo(SharedStorage storage)
        {
            var n = 0;
            foreach (var who in Interactor.All)
            {
                if (who.Stack == null) continue;
                foreach (var cargo in who.Stack.Items)
                {
                    if (cargo.Origin == CargoOrigin.Truck && cargo.Home == storage) n++;
                }
            }
            return n;
        }
    }
}
