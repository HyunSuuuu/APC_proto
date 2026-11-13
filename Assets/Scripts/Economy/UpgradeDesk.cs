using APC.Core;
using APC.Interaction;
using APC.UI;

namespace APC.Economy
{
    /// <summary>사무실 업그레이드 책상 (GDD 5.1 · 14.2). 계약 PC처럼 서 있으면 게이지가 차고, 다 차면 업그레이드 팝업이 열린다. 플레이어만 쓴다 (알바는 CargoZone만 찾음).</summary>
    public sealed class UpgradeDesk : GaugeZone
    {
        void Start() => Duration = GameManager.Instance.Database.Logistics.OfficeGaugeDuration;

        protected override void OnCompleted(Interactor who)
        {
            if (UpgradePopup.Instance != null) UpgradePopup.Instance.Open();
        }

        protected override void OnExit(Interactor who) => ResetGauge();
    }
}
