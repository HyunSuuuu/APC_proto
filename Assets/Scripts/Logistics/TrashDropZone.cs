using APC.Interaction;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 쓰레기장 쓰레기통 앞 존 (GDD 7.9, M13-11). 쓰레기를 든 채 들어오면 **멈추지 않아도** 간격마다 하나씩
    /// 쓰레기통 위로 날아가 사라진다. 쓰레기장은 가득 차지 않는다. 보상 없음.
    /// 2026-11-19 알바 상호작용 개편: 존 하나가 통 여러 개(`bins`) 앞을 덮고, 버린 사람에게 **가장 가까운 통**으로 날아간다.
    /// </summary>
    public sealed class TrashDropZone : CargoZone, IBeltEnd
    {
        [SerializeField, Tooltip("쓰레기가 날아가 들어갈 곳들 (쓰레기통 모델 위). 버린 사람에게 가장 가까운 곳으로 간다. 비우면 존 중심")] Transform[] bins;
        [SerializeField, Min(0.05f)] float flyDuration = 0.35f;

        public override bool TicksWhileMoving => true;

        protected override bool CanReceive(Cargo cargo, Interactor who) => cargo.State == CargoState.Trash;

        protected override void Receive(Cargo cargo, Interactor who) => Dispose(cargo, who.transform.position);

        // 벨트 End (GDD 7.10): 쓰레기장은 가득 차지 않는다
        public bool Wants(Cargo cargo) => cargo.State == CargoState.Trash;

        public bool CanTakeFromBelt(Cargo cargo) => true;

        public void ReceiveFromBelt(Cargo cargo) => Dispose(cargo, cargo.transform.position);

        void Dispose(Cargo cargo, Vector3 from)
        {
            var t = cargo.transform;
            t.SetParent(null, true);
            var target = NearestBin(from);
            t.DOJump(target, 0.8f, 1, flyDuration).OnComplete(cargo.Release);
            t.DOScale(0.3f, flyDuration).SetEase(Ease.InQuad);
        }

        /// <summary>바닥(XZ) 거리로 가장 가까운 통 위 위치. 통이 없으면 존 중심.</summary>
        Vector3 NearestBin(Vector3 from)
        {
            var best = transform.position;
            var bestDistance = float.MaxValue;
            if (bins == null) return best;
            foreach (var bin in bins)
            {
                if (bin == null) continue;
                var d = bin.position - from;
                d.y = 0f;
                if (d.sqrMagnitude >= bestDistance) continue;
                bestDistance = d.sqrMagnitude;
                best = bin.position;
            }
            return best;
        }
    }
}
