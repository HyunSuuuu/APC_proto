using R3;
using UnityEngine;

namespace APC.Interaction
{
    /// <summary>
    /// 게이지 존 (GDD 5.2, 체크리스트 M2-4). 존 안에서 멈추는 순간부터 게이지가 차고(따로 기다리지 않음), 다 차면 OnCompleted.
    /// 움직이거나 벗어나면 멈추고 진행도는 유지된다. 구매 패드 · 계약 PC · 고용 데스크 등이 상속한다.
    /// </summary>
    public abstract class GaugeZone : InteractionZone
    {
        public override bool WaitsForReadyDelay => false;

        protected override float FloorProgress(Interactor who) => who.IsStill ? 1f : 0f;

        [SerializeField, Min(0.05f), Tooltip("게이지가 다 차는 시간 (초)")] float duration = 1f;

        readonly ReactiveProperty<float> progress = new(0f);
        int lastTickFrame = -1;

        /// <summary>0~1.</summary>
        public ReadOnlyReactiveProperty<float> Progress => progress;

        public bool IsComplete => progress.Value >= 1f;

        /// <summary>이번 · 지난 프레임에 누군가 게이지를 채웠다 (작업 연출 켜기용).</summary>
        public bool IsTicking => Time.frameCount - lastTickFrame <= 1;

        protected float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0.05f, value);
        }

        public sealed override void Tick(Interactor who, float deltaTime)
        {
            if (IsComplete || !CanProgress(who)) return;
            if (lastTickFrame == Time.frameCount) return; // 여럿이 서 있어도 1배 (GDD 11.3)
            lastTickFrame = Time.frameCount;

            progress.Value = Mathf.Clamp01(Advance(who, progress.Value, deltaTime));
            if (IsComplete) OnCompleted(who);
        }

        public void ResetGauge() => progress.Value = 0f;

        /// <summary>다음 진행도를 계산한다. 구매 패드는 재화를 넣은 만큼만 진행하도록 덮어쓴다 (GDD 9.2).</summary>
        protected virtual float Advance(Interactor who, float current, float deltaTime) => current + deltaTime / duration;

        protected virtual bool CanProgress(Interactor who) => true;

        protected abstract void OnCompleted(Interactor who);

        protected virtual void OnDestroy() => progress.Dispose();
    }
}
