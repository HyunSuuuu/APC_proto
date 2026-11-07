using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>공용 보관소 내려놓기 존 (트럭 쪽). 트럭 화물을 받기만 하고 집지는 않는다.</summary>
    public sealed class StorageDropZone : CargoZone
    {
        [SerializeField] SharedStorage storage;

        protected override bool CanReceive(Cargo cargo, Interactor who) => storage.CanAccept(cargo);

        protected override void Receive(Cargo cargo, Interactor who) => storage.Store(cargo);
    }
}
