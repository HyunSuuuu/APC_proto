using System.Collections.Generic;
using APC.Data;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>입고 트럭 1대 (GDD 7.1). 실은 화물 목록과 남은 수량 표시만 가진다. 이동은 InboundBay가 한다.</summary>
    public sealed class InboundTruck : MonoBehaviour
    {
        [SerializeField, Tooltip("화물이 튀어나오는 위치 (픽업 존 쪽 옆면)")] Transform cargoExit;
        [SerializeField] TMP_Text countLabel;

        readonly List<ItemDefinition> load = new();
        readonly ReactiveProperty<int> remaining = new(0);

        public CompanyDefinition Company { get; private set; }
        public bool HasCargo => load.Count > 0;
        public ReadOnlyReactiveProperty<int> Remaining => remaining;

        void Awake() => remaining.Subscribe(n => countLabel.text = n.ToString()).AddTo(this);

        void OnDestroy() => remaining.Dispose();

        public void Load(CompanyDefinition company, IEnumerable<ItemDefinition> items)
        {
            Company = company;
            load.Clear();
            load.AddRange(items);
            remaining.Value = load.Count;
        }

        /// <summary>화물 1개를 꺼내 옆면에 만든다. 받는 쪽(운반 스택)이 날려 보낸다.</summary>
        public Cargo TakeCargo()
        {
            if (!HasCargo) return null;

            var item = load[^1];
            load.RemoveAt(load.Count - 1);
            remaining.Value = load.Count;

            countLabel.transform.DOKill(true);
            countLabel.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 6, 0.5f);

            var cargo = CargoPool.Instance.Spawn(item, CargoState.Normal, null, cargoExit.position);
            cargo.Origin = CargoOrigin.Truck;
            return cargo;
        }
    }
}
