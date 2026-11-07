using APC.Core;
using APC.Interaction;
using APC.UI;
using UnityEngine;

namespace APC.Contracts
{
    /// <summary>사무실 계약 PC (GDD 6.3). 게이지가 차면 계약 팝업을 연다. 벗어나면 게이지를 되돌린다.</summary>
    public sealed class ContractPC : GaugeZone
    {
        [SerializeField] ContractPopup popup;

        void Start() => Duration = GameManager.Instance.Database.Logistics.OfficeGaugeDuration;

        protected override void OnCompleted(Interactor who) => popup.Open();

        protected override void OnExit(Interactor who) => ResetGauge();
    }
}
