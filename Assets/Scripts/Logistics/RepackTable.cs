using APC.Core;
using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 반품 재포장 작업대 (GDD 7.7, M13-10 — 검수 삭제). 작업 위치(존)에 서 있고 반품 팔레트에 찌그러진 상자가 있으면
    /// 게이지(`LogisticsConfig.repackDuration`)가 차고, 다 차면 상자 1개가 **같은 색 일반 상자**가 되어 즉시 재포장 출구에 쌓인다.
    /// 출구가 차면 멈춘다. 자리를 떠나도 게이지는 남는다 (GaugeZone). 지금은 플레이어 전용 — 반품 알바는 M13-12.
    /// </summary>
    public sealed class RepackTable : GaugeZone
    {
        [SerializeField, Tooltip("반품 팔레트 (StorageKind.Returns)")] SharedStorage returnBin;
        [SerializeField, Tooltip("재포장 출구 (StorageKind.Repacked)")] SharedStorage output;
        [SerializeField, Tooltip("재포장 중인 상자를 올려 둘 곳 (작업대 위)")] Transform workPoint;

        void Start() => Duration = GameManager.Instance.Database.Logistics.RepackDuration;

        protected override bool CanProgress(Interactor who) => returnBin.HasReturn && output.HasSpace;

        protected override void OnCompleted(Interactor who)
        {
            ResetGauge();
            var cargo = returnBin.TakeReturn();
            if (cargo == null) return;

            cargo.Repack();
            if (workPoint != null) cargo.transform.position = workPoint.position;
            output.Store(cargo); // 작업대 위에서 출구로 날아간다 (재포장 XP 없음, GDD 10.1)
        }
    }
}
