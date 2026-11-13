using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 주운 현금 → HUD 저금통 달러 아이콘 연출 값 (GDD 7.6). `Assets/Data/CashFlyConfig.asset` — 인스펙터에서 바로 조절한다.
    /// Build Site가 HUD의 CashFlyEffect에 연결한다. 그래프의 가로축은 모두 비행 진행(0 = 출발, 1 = 도착).
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Cash Fly Config", fileName = "CashFlyConfig")]
    public sealed class CashFlyConfig : ScriptableObject
    {
        [Header("비행")]
        [SerializeField, Min(0.05f), Tooltip("플레이어에게서 HUD 저금통까지 날아가는 시간 (초). 도착할 때 저금통에 더한다")] float duration = 0.6f;
        [SerializeField, Tooltip("속도 그래프: 시간(0~1) → 경로 진행(0~1). 기울기가 클수록 빠르다. 기본 = 천천히 출발해 빨라짐")]
        AnimationCurve moveCurve = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));
        [SerializeField, Tooltip("포물선 높이 (HUD 기준 px, 위로 +, 아래로 −)")] float arcHeight = 220f;
        [SerializeField, Min(0f), Tooltip("출발 위치 랜덤 반경 (HUD 기준 px). 플레이어 화면 위치 중심의 원 안 무작위 지점에서 출발. 0 = 항상 중앙")] float startRadius = 60f;

        [Header("크기")]
        [SerializeField, Min(1f), Tooltip("아이콘 기본 크기 (HUD 기준 px)")] float size = 64f;
        [SerializeField, Tooltip("크기 그래프: 시간(0~1) → 기본 크기에 곱할 배율. 기본 = 1에서 0.5로 줄어듦")]
        AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.5f);

        [Header("도착")]
        [SerializeField, Min(0f), Tooltip("도착할 때 저금통 아이콘이 튀는 정도")] float targetPunch = 0.35f;
        [SerializeField, Min(0f)] float targetPunchDuration = 0.2f;

        public float Duration => duration;
        public float ArcHeight => arcHeight;
        public float StartRadius => startRadius;
        public float Size => size;
        public float TargetPunch => targetPunch;
        public float TargetPunchDuration => targetPunchDuration;

        /// <summary>경로 진행 (0~1 밖으로 넘치게 그리면 지나쳤다 돌아오는 연출도 된다).</summary>
        public float EvaluateMove(float t) => moveCurve != null && moveCurve.length > 0 ? moveCurve.Evaluate(t) : t;

        public float EvaluateScale(float t) => scaleCurve != null && scaleCurve.length > 0 ? scaleCurve.Evaluate(t) : 1f;

        static CashFlyConfig fallback;

        /// <summary>에셋이 연결되지 않았으면 기본값.</summary>
        public static CashFlyConfig OrDefault(CashFlyConfig config)
        {
            if (config != null) return config;
            if (fallback == null) fallback = CreateInstance<CashFlyConfig>();
            return fallback;
        }
    }
}
