using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 정산 저금통 깨기 연출 값 (R&amp;D, BrokeCashScene). `Assets/Data/BrokeCashConfig.asset` — 인스펙터에서 바로 조절한다.
    /// 번 돈 ÷ 기준 금액(PiggyFullAmount) = 비율 → 0 ~ maxRatio를 amountCurve로 t(0~1)로 바꾸고, 아래 (최소, 최대) 값을 t로 보간한다.
    /// 거리 · 크기는 월드 유닛 (저금통 이미지 1024 px = 10.24).
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Broke Cash Config", fileName = "BrokeCashConfig")]
    public sealed class BrokeCashConfig : ScriptableObject
    {
        [Header("금액 → 양")]
        [SerializeField, Min(0.1f), Tooltip("이 비율 이상은 모두 최대")] float maxRatio = 2f;
        [SerializeField, Tooltip("비율(0~1로 맞춘 값) → t. 기본 = OutQuad")] AnimationCurve amountCurve = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField, Tooltip("흘러내리는 돈 조각 수 (최소, 최대)")] Vector2Int pieceCount = new(8, 120);
        [SerializeField, Tooltip("흘러내리는 시간 (초)")] Vector2 pourSeconds = new(0.9f, 2.8f);
        [SerializeField, Tooltip("바닥 더미 반경")] Vector2 pileRadius = new(0.8f, 2f);
        [SerializeField, Tooltip("바닥 더미 높이")] Vector2 pileHeight = new(0.1f, 1f);
        [SerializeField, Tooltip("매달린 채 출렁이는 각도 (도)")] Vector2 swayDegrees = new(1.5f, 4.5f);
        [SerializeField, Tooltip("깨질 때 카메라 흔들림 세기")] Vector2 cameraShake = new(0.12f, 0.45f);
        [SerializeField, Tooltip("저금통 안 돈 높이 (몸통 높이 비율). 비율 0~1에 맞춰 찬다")] Vector2 fill = new(0.2f, 0.92f);
        [SerializeField, Range(0f, 1f), Tooltip("동전만 → 동전 + 지폐가 되는 비율")] float billRatio = 0.3f;
        [SerializeField, Min(0f), Tooltip("돈다발까지 섞이는 비율")] float bundleRatio = 1f;

        [Header("타이밍")]
        [SerializeField, Min(0.1f), Tooltip("사슬에 매달려 내려오는 시간")] float lowerSeconds = 0.75f;
        [SerializeField, Min(0f), Tooltip("멈춘 뒤 쩌적까지")] float crackDelay = 0.35f;
        [SerializeField, Min(0f), Tooltip("쩌적 뒤 깨질 때까지")] float breakDelay = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("금이 퍼지는 시간")] float crackGrowSeconds = 0.09f;
        [SerializeField, Min(0.1f), Tooltip("출렁 한 번 왕복 (초)")] float swayPeriod = 1.15f;

        [Header("흘러내림 · 날아감")]
        [SerializeField, Tooltip("구멍에서 바닥까지 튀는 높이")] Vector2 jumpPower = new(0.09f, 0.5f);
        [SerializeField, Tooltip("구멍에서 바닥까지 시간")] Vector2 jumpSeconds = new(0.45f, 0.62f);
        [SerializeField, Tooltip("착지 뒤 날아가기까지 (착지하자마자 모드)")] Vector2 holdSeconds = new(0.2f, 0.45f);
        [SerializeField, Min(0f), Tooltip("다 쏟아진 뒤 모드: 조각 사이 간격 최대")] float afterGap = 0.035f;
        [SerializeField, Min(0.1f), Tooltip("다 쏟아진 뒤 모드: 전체가 날아가기 시작하는 데 걸리는 시간 최대")] float afterSpan = 1.4f;

        [Header("금액 나누기 (가중치)")]
        [SerializeField, Min(0.01f)] float coinWeight = 1f;
        [SerializeField, Min(0.01f)] float billWeight = 3f;
        [SerializeField, Min(0.01f)] float bundleWeight = 8f;

        public float MaxRatio => maxRatio;
        public Vector2Int PieceCount => pieceCount;
        public Vector2 PourSeconds => pourSeconds;
        public Vector2 PileRadius => pileRadius;
        public Vector2 PileHeight => pileHeight;
        public Vector2 SwayDegrees => swayDegrees;
        public Vector2 CameraShake => cameraShake;
        public Vector2 Fill => fill;
        public float BillRatio => billRatio;
        public float BundleRatio => bundleRatio;
        public float LowerSeconds => lowerSeconds;
        public float CrackDelay => crackDelay;
        public float BreakDelay => breakDelay;
        public float CrackGrowSeconds => crackGrowSeconds;
        public float SwayPeriod => swayPeriod;
        public Vector2 JumpPower => jumpPower;
        public Vector2 JumpSeconds => jumpSeconds;
        public Vector2 HoldSeconds => holdSeconds;
        public float AfterGap => afterGap;
        public float AfterSpan => afterSpan;
        public float CoinWeight => coinWeight;
        public float BillWeight => billWeight;
        public float BundleWeight => bundleWeight;

        public float EvaluateAmount(float x) => amountCurve != null && amountCurve.length > 0 ? Mathf.Clamp01(amountCurve.Evaluate(x)) : x;

        static BrokeCashConfig fallback;

        /// <summary>에셋이 연결되지 않았으면 기본값.</summary>
        public static BrokeCashConfig OrDefault(BrokeCashConfig config)
        {
            if (config != null) return config;
            if (fallback == null) fallback = CreateInstance<BrokeCashConfig>();
            return fallback;
        }
    }
}
