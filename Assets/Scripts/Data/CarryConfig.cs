using UnityEngine;

namespace APC.Data
{
    /// <summary>운반 연출 수치 (GDD 5.3, 체크리스트 M2-5/M2-6). 플레이어와 알바가 함께 쓴다. 손맛 테스트에서 조정한다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Carry Config", fileName = "CarryConfig")]
    public sealed class CarryConfig : ScriptableObject
    {
        [Header("상자")]
        [SerializeField, Min(0.05f), Tooltip("상자 한 칸 높이 = 색 상자 모델 높이 0.3 × 배율 1.667 = 0.5 (M15-2). 화물 HeightScale = 모델 높이 / 이 값")] float boxHeight = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("쓰레기 모델(Trash) 크기 배율 (2026-11-19 '크게')")] float trashModelScale = 2.5f;

        [Header("집기 / 내려놓기 비행 (DOJump)")]
        [SerializeField, Min(0.01f)] float flyDuration = 0.25f;
        [SerializeField, Min(0f), Tooltip("포물선 높이 (m)")] float jumpPower = 1f;
        [SerializeField, Min(0f), Tooltip("착지 시 상자 크기 펀치")] float landPunch = 0.2f;
        [SerializeField, Min(0.01f)] float landPunchDuration = 0.15f;
        [SerializeField, Min(0.01f), Tooltip("중간 상자가 빠진 뒤 위 상자들이 내려앉는 시간")] float reflowDuration = 0.12f;

        [Header("스택 흔들림 — 관성 기울기")]
        [SerializeField, Min(0f), Tooltip("이동 속도(m/s)당 한 층의 기울기 (m)")] float leanPerSpeed = 0.012f;
        [SerializeField, Min(0f), Tooltip("한 층의 최대 기울기 (m). 위층일수록 층 수만큼 더 기운다")] float maxLeanPerLevel = 0.06f;
        [SerializeField, Min(0f), Tooltip("스프링 강도 — 클수록 빨리 따라감")] float springStiffness = 150f;
        [SerializeField, Min(0f), Tooltip("스프링 감쇠 — 작을수록 더 출렁임")] float springDamping = 10f;

        [Header("스택 휘어짐 — 많이 쌓을수록 곡선으로 더 기움 (2026-11-19)")]
        [SerializeField, Tooltip("층 → 기울기 배율. x = 층 / curveLevels (0 = 맨 아래, 1 = curveLevels층), y = 그 층의 배율 (1이면 옛 직선). 끝보다 높은 층은 끝 값")]
        AnimationCurve leanCurve = new(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(1f, 2f, 2f, 0f));
        [SerializeField, Min(1f), Tooltip("leanCurve x = 1이 되는 층 수")] float curveLevels = 15f;
        [SerializeField, Tooltip("상자가 휘어진 줄을 따라 기운다 (회전)")] bool tiltAlongCurve = true;

        [Header("스택 흔들림 — 출렁 (상자가 들어오거나 빠질 때)")]
        [SerializeField] Vector3 stackBounce = new(0.06f, -0.08f, 0.06f);
        [SerializeField, Min(0.01f)] float stackBounceDuration = 0.2f;

        [Header("MAX 표시")]
        [SerializeField, Min(1f)] float maxPulseScale = 1.1f;
        [SerializeField, Min(0.05f), Tooltip("커졌다 작아지는 한 주기 (초)")] float maxPulsePeriod = 0.6f;

        [Header("'넣을 수 없음' 그림 (떠올랐다 사라짐)")]
        [SerializeField, Tooltip("Art/Temp/candoit")] Sprite cannotDropIcon;
        [SerializeField, Min(0.1f), Tooltip("그림 높이 (m)")] float cannotDropIconSize = 1.2f;

        public float BoxHeight => boxHeight;
        public float FlyDuration => flyDuration;
        public float JumpPower => jumpPower;
        public float LandPunch => landPunch;
        public float LandPunchDuration => landPunchDuration;
        public float ReflowDuration => reflowDuration;
        public float LeanPerSpeed => leanPerSpeed;
        public float MaxLeanPerLevel => maxLeanPerLevel;
        public float SpringStiffness => springStiffness;
        public float SpringDamping => springDamping;
        public float TrashModelScale => trashModelScale;
        public bool TiltAlongCurve => tiltAlongCurve;

        /// <summary>층 i(0 = 맨 아래)의 기울기 합 배수 = (i + 1) × leanCurve(i / curveLevels). 곡선이 1이면 옛 직선과 같다.</summary>
        public float LeanFactor(int level) => (level + 1) * leanCurve.Evaluate(Mathf.Min(1f, level / curveLevels));
        public Vector3 StackBounce => stackBounce;
        public float StackBounceDuration => stackBounceDuration;
        public float MaxPulseScale => maxPulseScale;
        public float MaxPulsePeriod => maxPulsePeriod;
        public Sprite CannotDropIcon => cannotDropIcon;
        public float CannotDropIconSize => cannotDropIconSize;
    }
}
