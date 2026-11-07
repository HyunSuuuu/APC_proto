using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Interaction;
using APC.Logistics;
using DG.Tweening;
using UnityEngine;

namespace APC.Debugging
{
    /// <summary>손맛 테스트용 수거함 (체크리스트 M2-7). 받은 화물은 날아 들어가 사라진다. 이후 삭제.</summary>
    public sealed class DebugCargoSink : CargoZone
    {
        [SerializeField, Tooltip("비우면 모든 품목을 받는다")] List<ItemDefinition> acceptItems = new();
        [SerializeField] Transform binPoint;

        CarryConfig config;

        public int ReceivedCount { get; private set; }

        void Start() => config = GameManager.Instance.Database.Carry;

        protected override bool CanReceive(Cargo cargo, Interactor who)
            => acceptItems.Count == 0 || acceptItems.Contains(cargo.Item);

        protected override void Receive(Cargo cargo, Interactor who)
        {
            ReceivedCount++;
            var t = cargo.transform;
            t.SetParent(binPoint, true);
            DOTween.Sequence()
                .Append(t.DOLocalJump(Vector3.zero, config.JumpPower, 1, config.FlyDuration))
                .Join(t.DOLocalRotateQuaternion(Quaternion.identity, config.FlyDuration))
                .Append(t.DOScale(0f, 0.15f).SetEase(Ease.InBack))
                .SetTarget(t)
                .OnComplete(cargo.Release);
        }
    }
}
