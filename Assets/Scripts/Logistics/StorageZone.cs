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

        /// <summary>반품 선반에서 하나도 못 넣었을 때 (플레이어만, GDD 5.3): 반품 상자인데 꽉 찼으면 '가득', 그 밖의 상자면 '넣을 수 없음'. 선반에서 꺼낸 상자는 세지 않는다.</summary>
        protected override void OnNothingDropped(Interactor who)
        {
            if (storage.Kind != StorageKind.Returns) return;
            var returns = false;
            var other = false;
            foreach (var cargo in who.Stack.Items)
            {
                if (cargo.Origin == CargoOrigin.Shelf) continue;
                if (cargo.State == CargoState.Return) returns = true;
                else other = true;
            }
            if (returns || other) Notice(who, storage.CountLabel, LabelPosition, returns ? "가득" : "넣을 수 없음");
        }

        protected override CargoOrigin GiveOrigin => CargoOrigin.Storage;

        public override bool HasCargoFor(Interactor who) => storage.HasCargoForShelves();

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = storage.TakeForShelves();
            // 하차 칸 공간에 상자는 있는데 그 색 선반이 다 차서 하나도 못 집음 → '선반 가득' (플레이어만, GDD 7.3)
            if (cargo == null && storage.Kind == StorageKind.Pallet && storage.Count.CurrentValue > 0)
                Notice(who, storage.CountLabel, LabelPosition, "선반 가득");
            return cargo != null;
        }

        Vector3 LabelPosition => (storage.CountLabel != null ? storage.CountLabel.transform.position : transform.position) + Vector3.up * 0.5f;
    }
}
