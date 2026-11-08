using APC.Core;
using APC.Interaction;
using APC.UI;

namespace APC.Contracts
{
    /// <summary>
    /// 사무실 계약 PC (GDD 6.3). 게이지가 차면 계약 팝업을 연다. 벗어나면 게이지를 되돌린다.
    /// 사무실 모듈 프리팹 안에 있으므로 팝업은 씬에서 찾는다 (ContractPopup.Instance).
    /// </summary>
    public sealed class ContractPC : GaugeZone
    {
        void Start() => Duration = GameManager.Instance.Database.Logistics.OfficeGaugeDuration;

        protected override void OnCompleted(Interactor who)
        {
            if (ContractPopup.Instance != null) ContractPopup.Instance.Open();
        }

        protected override void OnExit(Interactor who) => ResetGauge();
    }
}
