using APC.Contracts;
using APC.Data;
using APC.Interaction;
using APC.Orders;

namespace APC.Logistics
{
    /// <summary>
    /// 그 파트에 아직 할 일이 남았는지 (남은 시간 보너스, GDD 9.5 · M17-3). `PartService`가 `idleCheckInterval`마다 부른다.
    /// false가 되면 남은 시간을 보너스로 바꾼다 — 1파트엔 선반이 줄지 않고 2파트엔 재고가 늘지 않아 다시 true가 되지 않는다고 본다.
    /// </summary>
    public static class WorkCheck
    {
        /// <summary>
        /// 1파트(하차): 하차 칸 하나라도 진행할 수 있으면 true. 선반 먼저 — 하차 칸 공간의 상자를 선반(반품 팔레트)에 더 옮길 수 있으면 일이 있고,
        /// 그다음 하차 — 정차 트럭에서 공간에 더 꺼낼 수 있거나(빈 칸이면 트럭이 들어와 내릴 수 있음), D 트럭을 팔레트 랙에 바로 넣을 수 있으면 일이 있다.
        /// </summary>
        public static bool CanUnload()
        {
            var any = false;
            foreach (var zone in InteractionZone.Active)
            {
                if (zone is not TruckPickupZone pickup || pickup.Bay == null) continue;
                any = true;
                if (BayHasWork(pickup)) return true;
            }
            return !any; // 하차 칸이 없으면 판단하지 않음
        }

        static bool BayHasWork(TruckPickupZone pickup)
        {
            var bay = pickup.Bay;
            if (bay.Storage != null && bay.Storage.HasCargoForShelves()) return true;
            if (bay.Storage != null && bay.Storage.CanScan) return true; // 스캔하면 옮길 상자가 생김 (GDD 7.2a)
            var truck = bay.DockedTruck;
            if (truck == null || !truck.HasCargo) return pickup.Room > 0;
            if (truck.Golden) return true; // 금 쏟는 중 (잠깐)
            var item = truck.Company != null ? truck.Company.Items[0] : null;
            if (item != null && Warehouse.IsLarge(item)) return HasForklift() && Warehouse.Instance.LargeRoom(item) > 0;
            return pickup.Room > 0;
        }

        static bool HasForklift()
        {
            foreach (var who in Interactor.All)
            {
                if (who.Forklift) return true;
            }
            return false;
        }

        /// <summary>
        /// 2파트(상차): 오는 중인 트럭이 있거나, 정차 트럭이 원하는 색이 선반 · 하차 칸 · 누군가의 손에 남아 있거나, 빈 상차 칸에 새 트럭이 올 수 있거나,
        /// 하차 칸 상자를 선반에 더 올릴 수 있으면(새 트럭이 올 수 있게 됨) true.
        /// </summary>
        public static bool CanLoad()
        {
            var any = false;
            var orders = OrderService.Instance;
            foreach (var bay in OutboundBay.All)
            {
                any = true;
                if (bay.Arriving) return true;
                var truck = bay.DockedTruck;
                if (truck != null && truck.Order != null && !truck.Order.IsFulfilled)
                {
                    foreach (var line in truck.Order.Lines)
                    {
                        if (line.Remaining > 0 && Available(line.Item) > 0) return true;
                    }
                }
                else if (truck == null && orders != null && orders.CanCreateForTruck()) return true;
            }
            if (!any) return true; // 상차 칸이 없으면 판단하지 않음

            var contracts = ContractService.Instance;
            if (contracts == null || Warehouse.Instance == null) return false;
            foreach (var company in contracts.Contracted)
            {
                var item = company.Items[0];
                if (StoredCount(item) > 0 && SharedStorage.ShelfAllowance(item) > 0) return true;
            }
            return false;
        }

        /// <summary>그 색이 선반 + 하차 칸 · 재포장 팔레트 + 누군가의 손에 있는 수.</summary>
        static int Available(ItemDefinition item)
        {
            var count = Warehouse.Instance != null ? Warehouse.Instance.GetShelfCount(item) : 0;
            count += StoredCount(item);
            count += Warehouse.CountCarried(item, CargoOrigin.Shelf) + Warehouse.CountCarried(item, CargoOrigin.Storage) + Warehouse.CountCarried(item, CargoOrigin.Truck);
            return count;
        }

        static int StoredCount(ItemDefinition item)
        {
            var count = 0;
            foreach (var storage in SharedStorage.All)
            {
                if (storage.Kind is StorageKind.Pallet or StorageKind.Repacked) count += storage.CountOf(item);
            }
            return count;
        }
    }
}
