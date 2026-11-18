using APC.Logistics;
using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 황금 트럭 · 금 (GDD 6.4) 확률 · 값 · 연출. `Assets/Data/GoldenTruckConfig.asset` — 인스펙터에서 바로 조절한다 (Play 중 바꾼 값은
    /// 다음 황금 트럭부터 적용, 몸체 · 빛 · 반짝임은 그 트럭이 도착할 때 만든다). 시험할 때는 디버그 메뉴 `Golden truck`.
    /// GameDatabase가 참조한다. 비어 있으면 기본값.
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Data/Golden Truck Config", fileName = "GoldenTruckConfig")]
    public sealed class GoldenTruckConfig : ScriptableObject
    {
        [Header("확률 · 값")]
        [SerializeField, Range(0f, 1f), Tooltip("하차 트럭이 황금 트럭일 확률 (0.0005 = 0.05%, GDD 6.4 임시). 금 = 상자 수만큼")] float chance = 0.0005f;
        [SerializeField, Min(0f), Tooltip("금 1덩이 = 지폐 다발 × 이 배율 (GDD 6.4)")] float valueMultiplier = 2f;

        [Header("몸체 색")]
        [SerializeField, Tooltip("켜면 무늬(팔레트 텍스처)를 지우고 단색 금으로 칠한다. 끄면 원래 무늬에 색을 곱한다 (빨간 트럭은 주황빛)")] bool solidColor = true;
        [SerializeField, Tooltip("몸체 기본 색")] Color bodyColor = new(1f, 0.82f, 0.25f);
        [SerializeField, Tooltip("깜빡일 때 바뀌는 색 (기본 색 ↔ 이 색)")] Color pulseColor = new(1f, 0.97f, 0.7f);
        [SerializeField, Min(0f), Tooltip("몸체 깜빡임 한 주기 (초). 0 = 깜빡이지 않음")] float pulsePeriod = 1.2f;

        [Header("빛 (트럭 위 점광원)")]
        [SerializeField] bool useLight = true;
        [SerializeField] Color lightColor = new(1f, 0.8f, 0.3f);
        [SerializeField, Min(0f)] float lightIntensity = 4f;
        [SerializeField, Min(0f), Tooltip("빛이 닿는 거리 (m)")] float lightRange = 9f;
        [SerializeField, Tooltip("트럭 기준 빛 위치 (m)")] Vector3 lightOffset = new(0f, 3f, 0f);
        [SerializeField, Range(0f, 1f), Tooltip("빛 세기 흔들림 (0 = 고정, 1 = 0까지 떨어짐). 몸체 깜빡임 주기를 따른다")] float lightFlicker = 0.3f;

        [Header("반짝임 (파티클)")]
        [SerializeField] bool useSparkle = true;
        [SerializeField, Tooltip("반짝임 재질 (URP Particles). 비면 반짝임 없음")] Material sparkleMaterial;
        [SerializeField] Color sparkleColor = new(1f, 0.95f, 0.55f);
        [SerializeField, Min(0f), Tooltip("초당 개수")] float sparkleRate = 20f;
        [SerializeField, Min(0.05f), Tooltip("한 알 수명 (초)")] float sparkleLifetime = 0.9f;
        [SerializeField, Min(0.01f), Tooltip("한 알 크기 (m)")] float sparkleSize = 0.7f;
        [SerializeField, Tooltip("위로 떠오르는 속도 (m/초)")] float sparkleRise = 0.8f;
        [SerializeField, Tooltip("나오는 상자 범위 (트럭 기준, m)")] Vector3 sparkleArea = new(2.6f, 3f, 6.5f);
        [SerializeField, Tooltip("상자 범위 중심 (트럭 기준, m)")] Vector3 sparkleCenter = new(0f, 1.8f, 0f);

        [Header("쏟기")]
        [SerializeField, Min(0f), Tooltip("주차한 뒤 쏟기 시작까지 (초)")] float dumpDelay = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("금 1덩이씩 쏟는 간격 (초)")] float dumpInterval = 0.08f;
        [SerializeField, Min(0f), Tooltip("쏟을 때 튀어 오르는 높이 (m)")] float dumpJumpPower = 1.5f;
        [SerializeField, Min(0.05f), Tooltip("트럭에서 바닥까지 날아가는 시간 (초)")] float dumpFlyDuration = 0.4f;
        [SerializeField, Tooltip("픽업 존 바닥에 쌓는 모양 (열 · 행 · 간격 · 층 높이)")] CargoGridLayout pileLayout = new(3, 2, 0.6f, 0.5f);
        [SerializeField, Min(0f), Tooltip("다 쏟은 뒤 트럭이 떠나기까지 (초)")] float leaveDelay = 0f;

        [Header("흡수")]
        [SerializeField, Min(0f), Tooltip("흡수 반경 = 현금 줍기 반경 + 이 값 (m)")] float absorbRadiusBonus = 1f;
        [SerializeField, Min(0.01f), Tooltip("1덩이씩 빨려 가는 간격 (초)")] float absorbInterval = 0.08f;
        [SerializeField, Min(0.05f), Tooltip("사람에게 날아가는 시간 (초)")] float absorbFlyDuration = 0.35f;
        [SerializeField, Tooltip("날아가는 포물선 높이 (m)")] float absorbArcHeight = 1.2f;
        [SerializeField, Tooltip("날아가며 도는 속도 (도/초, Y축)")] float absorbSpin = 720f;
        [SerializeField, Tooltip("날아가며 크기 배율 (시간 0~1 → 배율)")] AnimationCurve absorbScale = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);

        public float Chance => chance;
        public float ValueMultiplier => valueMultiplier;
        public bool SolidColor => solidColor;
        public Color BodyColor => bodyColor;
        public Color PulseColor => pulseColor;
        public float PulsePeriod => pulsePeriod;
        public bool UseLight => useLight;
        public Color LightColor => lightColor;
        public float LightIntensity => lightIntensity;
        public float LightRange => lightRange;
        public Vector3 LightOffset => lightOffset;
        public float LightFlicker => lightFlicker;
        public bool UseSparkle => useSparkle && sparkleMaterial != null;
        public Material SparkleMaterial => sparkleMaterial;
        public Color SparkleColor => sparkleColor;
        public float SparkleRate => sparkleRate;
        public float SparkleLifetime => sparkleLifetime;
        public float SparkleSize => sparkleSize;
        public float SparkleRise => sparkleRise;
        public Vector3 SparkleArea => sparkleArea;
        public Vector3 SparkleCenter => sparkleCenter;
        public float DumpDelay => dumpDelay;
        public float DumpInterval => dumpInterval;
        public float DumpJumpPower => dumpJumpPower;
        public float DumpFlyDuration => dumpFlyDuration;
        public CargoGridLayout PileLayout => pileLayout;
        public float LeaveDelay => leaveDelay;
        public float AbsorbRadiusBonus => absorbRadiusBonus;
        public float AbsorbInterval => absorbInterval;
        public float AbsorbFlyDuration => absorbFlyDuration;
        public float AbsorbArcHeight => absorbArcHeight;
        public float AbsorbSpin => absorbSpin;

        public float EvaluateAbsorbScale(float t) => absorbScale != null && absorbScale.length > 0 ? absorbScale.Evaluate(t) : 1f;

        /// <summary>깜빡임 0~1 (주기 0이면 0).</summary>
        public float Pulse(float time) => pulsePeriod <= 0f ? 0f : 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / pulsePeriod);

        static GoldenTruckConfig fallback;

        /// <summary>에셋이 연결되지 않았으면 기본값.</summary>
        public static GoldenTruckConfig OrDefault(GoldenTruckConfig config)
        {
            if (config != null) return config;
            if (fallback == null) fallback = CreateInstance<GoldenTruckConfig>();
            return fallback;
        }
    }
}
