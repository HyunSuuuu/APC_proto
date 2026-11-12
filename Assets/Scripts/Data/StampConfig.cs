using DG.Tweening;
using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 계약 PC 도장 연출 값 (GDD 6.3 · 11.1). `Assets/Data/StampConfig.asset` — 인스펙터에서 바로 조절한다.
    /// 도장(Stamp) · 도장 카드(StampCard) · 계약 팝업(ContractPopup)이 Build Site에서 이 에셋을 참조한다. 시간은 모두 실시간(초).
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Stamp Config", fileName = "StampConfig")]
    public sealed class StampConfig : ScriptableObject
    {
        [Header("도장 누르기 (성공)")]
        [SerializeField, Min(1f), Tooltip("꾹 누를 때 커지는 배율")] float pressScale = 1.25f;
        [SerializeField, Min(0f), Tooltip("커지는 시간")] float pressInDuration = 0.1f;
        [SerializeField, Min(0f), Tooltip("원래 크기로 돌아오는 시간")] float pressOutDuration = 0.15f;
        [SerializeField, Min(0f), Tooltip("누른 뒤 카드 위에 머무는 시간")] float holdDuration = 0.35f;

        [Header("도장 제자리로")]
        [SerializeField, Min(0f)] float returnDuration = 0.35f;
        [SerializeField] Ease returnEase = Ease.OutBack;

        [Header("카드 도장 자국")]
        [SerializeField, Min(1f), Tooltip("자국이 처음 나타나는 배율 (여기서 1로 내려앉음)")] float markStartScale = 1.8f;
        [SerializeField, Min(0f)] float markDropDuration = 0.22f;
        [SerializeField, Min(0f), Tooltip("자국이 내려앉을 때 카드가 튀는 정도")] float cardPunch = 0.06f;
        [SerializeField, Min(0f)] float cardPunchDuration = 0.2f;
        [SerializeField, Min(0f), Tooltip("회색 필터가 덮이는 시간")] float filterFadeDuration = 0.3f;

        [Header("찍을 수 없음 (흔들림 + 사유)")]
        [SerializeField, Min(0f)] float shakeDuration = 0.35f;
        [SerializeField, Min(0f)] float shakeStrength = 18f;
        [SerializeField, Min(0f), Tooltip("사유 글자를 보여 주는 시간")] float reasonDuration = 1.2f;

        [Header("계약 탭")]
        [SerializeField, Min(0f), Tooltip("계약 도장을 찍은 뒤 팝업이 닫히기까지")] float contractCloseDelay = 1.2f;

        public float PressScale => pressScale;
        public float PressInDuration => pressInDuration;
        public float PressOutDuration => pressOutDuration;
        public float HoldDuration => holdDuration;
        public float ReturnDuration => returnDuration;
        public Ease ReturnEase => returnEase;
        public float MarkStartScale => markStartScale;
        public float MarkDropDuration => markDropDuration;
        public float CardPunch => cardPunch;
        public float CardPunchDuration => cardPunchDuration;
        public float FilterFadeDuration => filterFadeDuration;
        public float ShakeDuration => shakeDuration;
        public float ShakeStrength => shakeStrength;
        public float ReasonDuration => reasonDuration;
        public float ContractCloseDelay => contractCloseDelay;

        static StampConfig fallback;

        /// <summary>에셋이 연결되지 않았으면 기본값 (경고 없이 동작).</summary>
        public static StampConfig OrDefault(StampConfig config)
        {
            if (config != null) return config;
            if (fallback == null) fallback = CreateInstance<StampConfig>();
            return fallback;
        }
    }
}
