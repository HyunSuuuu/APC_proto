using APC.Interaction;
using UnityEngine;

namespace APC.Events
{
    /// <summary>
    /// 사무실 벽 분전반 (GDD 14.3). 정전 중에만 게이지(`duration` 2초 임시)가 차고, 다 차면 정전이 끝난다. 벗어나면 게이지를 되돌린다.
    /// 알바는 정전 중 멈춰 있으므로 플레이어만 고친다.
    /// </summary>
    public sealed class BreakerPanel : GaugeZone
    {
        [SerializeField, Min(0.05f), Tooltip("고치는 시간 (초)")] float repairDuration = 2f;

        void Start()
        {
            Duration = repairDuration;
            if (Outage.Instance != null) Outage.Instance.Breaker = transform;
        }

        protected override bool CanProgress(Interactor who) => Outage.Active;

        protected override void OnCompleted(Interactor who)
        {
            ResetGauge();
            if (Outage.Instance != null) Outage.Instance.End();
        }

        protected override void OnExit(Interactor who) => ResetGauge();
    }
}
