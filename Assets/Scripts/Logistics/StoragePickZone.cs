using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>공용 보관소 집기 존 (선반 쪽). 선반에 넣을 수 있는 화물만 집고, 받지는 않는다.</summary>
    public sealed class StoragePickZone : CargoZone
    {
        [SerializeField] SharedStorage storage;

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = storage.TakeForShelves();
            return cargo != null;
        }
    }
}
