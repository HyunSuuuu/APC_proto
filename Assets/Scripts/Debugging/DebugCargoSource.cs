using System.Collections.Generic;
using APC.Data;
using APC.Interaction;
using APC.Logistics;
using DG.Tweening;
using UnityEngine;

namespace APC.Debugging
{
    /// <summary>손맛 테스트용 무한 화물 더미 (체크리스트 M2-7). 하나를 꺼내면 빈자리에 새 상자가 바로 채워진다. 이후 삭제.</summary>
    public sealed class DebugCargoSource : CargoZone
    {
        [SerializeField, Tooltip("무작위로 나올 품목")] List<ItemDefinition> items = new();
        [SerializeField] Transform pileRoot;
        [SerializeField, Min(1)] int displayCount = 8;
        [SerializeField] CargoGridLayout layout = new(2, 2, 0.6f, 0.5f);

        readonly List<Cargo> pile = new();

        void Start()
        {
            for (var i = 0; i < displayCount; i++) pile.Add(SpawnAt(i));
        }

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            if (pile.Count == 0 || items.Count == 0)
            {
                cargo = null;
                return false;
            }

            cargo = pile[^1];
            pile.RemoveAt(pile.Count - 1);

            var refill = SpawnAt(pile.Count);
            refill.transform.localScale = Vector3.zero;
            refill.transform.DOScale(1f, 0.2f).SetDelay(0.1f).SetEase(Ease.OutBack);
            pile.Add(refill);
            return true;
        }

        Cargo SpawnAt(int index)
            => CargoPool.Instance.Spawn(items[Random.Range(0, items.Count)], CargoState.Normal, pileRoot, layout.GetLocalPosition(index));
    }
}
