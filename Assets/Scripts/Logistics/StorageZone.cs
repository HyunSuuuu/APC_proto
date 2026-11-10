using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 하차장 팔레트(공용 보관소) 둘레 존 (GDD 7.2). 근처에서 멈추면 **트럭 화물 내려놓기를 먼저** 하고,
    /// 내려놓을 것이 없으면 선반에 빈자리가 있는 품목만 집는다 (선반 빈자리 − 이미 들고 있는 수).
    /// </summary>
    public sealed class StorageZone : CargoZone
    {
        [SerializeField] SharedStorage storage;

        public SharedStorage Storage => storage;

        protected override bool CanReceive(Cargo cargo, Interactor who) => storage.CanAccept(cargo);

        protected override void Receive(Cargo cargo, Interactor who) => storage.Store(cargo);

        protected override CargoOrigin GiveOrigin => CargoOrigin.Storage;

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = storage.TakeForShelves();
            return cargo != null;
        }
    }
}
