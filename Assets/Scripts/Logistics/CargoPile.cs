using System;
using System.Collections.Generic;
using APC.Core;
using APC.Data;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 시설에 쌓인 화물 목록 + 배치 연출. 공용 보관소 · 선반 등이 쓴다.
    /// 들어올 때는 포물선으로 자기 칸에 착지하고, 중간이 빠지면 뒤 화물들이 앞 칸으로 당겨진다.
    /// </summary>
    public sealed class CargoPile
    {
        readonly Transform root;
        readonly Func<int, Vector3> slotPosition;
        readonly List<Cargo> items = new();

        public CargoPile(Transform root, Func<int, Vector3> slotPosition)
        {
            this.root = root;
            this.slotPosition = slotPosition;
        }

        public IReadOnlyList<Cargo> Items => items;
        public int Count => items.Count;

        static CarryConfig Config => GameManager.Instance.Database.Carry;

        public void Add(Cargo cargo)
        {
            items.Add(cargo);
            var config = Config;
            var t = cargo.transform;
            var visual = cargo.Visual;
            t.DOKill();
            t.SetParent(root, true);
            t.DOLocalJump(slotPosition(items.Count - 1), config.JumpPower, 1, config.FlyDuration)
                .OnComplete(() => visual.DOPunchScale(Vector3.one * config.LandPunch, config.LandPunchDuration, 6, 0.5f));
            t.DOLocalRotateQuaternion(Quaternion.identity, config.FlyDuration);
        }

        /// <summary>빼낸 화물은 받는 쪽이 옮긴다.</summary>
        public Cargo RemoveAt(int index)
        {
            var cargo = items[index];
            items.RemoveAt(index);
            cargo.transform.DOKill();

            var duration = Config.ReflowDuration;
            for (var i = index; i < items.Count; i++)
            {
                var t = items[i].transform;
                t.DOKill();
                t.localRotation = Quaternion.identity;
                t.DOLocalMove(slotPosition(i), duration).SetEase(Ease.OutQuad);
            }
            return cargo;
        }
    }
}
