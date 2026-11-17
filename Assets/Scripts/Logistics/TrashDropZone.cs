using APC.Interaction;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 쓰레기장 쓰레기통 앞 존 (GDD 7.9, M13-11). 쓰레기를 든 채 들어오면 **멈추지 않아도** 간격마다 하나씩
    /// 쓰레기통(`bin`) 위로 날아가 사라진다. 쓰레기장은 가득 차지 않는다. 보상 없음.
    /// </summary>
    public sealed class TrashDropZone : CargoZone
    {
        [SerializeField, Tooltip("쓰레기가 날아가 들어갈 곳 (쓰레기통 모델 위)")] Transform bin;
        [SerializeField, Min(0.05f)] float flyDuration = 0.35f;

        public override bool TicksWhileMoving => true;

        protected override bool CanReceive(Cargo cargo, Interactor who) => cargo.State == CargoState.Trash;

        protected override void Receive(Cargo cargo, Interactor who)
        {
            var t = cargo.transform;
            t.SetParent(null, true);
            var target = bin != null ? bin.position : transform.position;
            t.DOJump(target, 0.8f, 1, flyDuration).OnComplete(cargo.Release);
            t.DOScale(0.3f, flyDuration).SetEase(Ease.InQuad);
        }
    }
}
