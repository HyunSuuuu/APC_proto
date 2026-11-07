using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>선반 적재 존 (앞). 공용 보관소에서 집은 그 품목 상자만 받는다 (GDD 7.3).</summary>
    public sealed class ShelfLoadZone : CargoZone
    {
        [SerializeField] Shelf shelf;

        protected override bool CanReceive(Cargo cargo, Interactor who)
            => cargo.Item == shelf.Item
               && cargo.Origin == CargoOrigin.Storage
               && cargo.State == CargoState.Normal
               && shelf.HasSpace;

        protected override void Receive(Cargo cargo, Interactor who)
        {
            cargo.Origin = CargoOrigin.Shelf;
            shelf.Store(cargo);
        }
    }
}
