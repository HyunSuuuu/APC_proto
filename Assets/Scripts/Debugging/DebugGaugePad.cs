using APC.Core;
using APC.Interaction;
using DG.Tweening;
using UnityEngine;

namespace APC.Debugging
{
    /// <summary>게이지 존 테스트용 패드 (체크리스트 M2-4). 다 차면 현금을 주고, 벗어나면 게이지를 되돌린다. 이후 삭제.</summary>
    public sealed class DebugGaugePad : GaugeZone
    {
        [SerializeField, Min(0)] int reward = 100;
        [SerializeField, Tooltip("완료 시 펀치할 표시물 (존 루트는 크기 1 유지)")] Transform punchTarget;

        protected override void OnCompleted(Interactor who)
        {
            GameManager.Instance.Wallet.Add(reward);
            if (punchTarget == null) return;
            punchTarget.DOKill(true);
            punchTarget.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f);
        }

        protected override void OnExit(Interactor who)
        {
            if (IsComplete) ResetGauge();
        }
    }
}
