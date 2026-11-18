using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 벽 반투명 (2026-11-19). 카메라 기준으로 플레이어를 가린 벽 조각 · 주차장 지붕(WallFadeTarget)을 부드럽게 반투명하게 한다 (SiteWalls).
    /// 카메라 → 플레이어의 확인 높이마다 선을 긋고, 그 선이 오브젝트 경계 상자(여유만큼 넓힘)를 지나면 가린 것.
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Data/Wall Fade Config", fileName = "WallFadeConfig")]
    public sealed class WallFadeConfig : ScriptableObject
    {
        [SerializeField, Tooltip("끄면 반투명 없음")] bool enabled = true;
        [SerializeField, Tooltip("반투명 재질 (URP Lit 투명, M_PropFade). 가린 동안 원래 재질 대신 쓴다")] Material fadeMaterial;

        [Header("모양")]
        [SerializeField, Range(0f, 1f), Tooltip("가렸을 때 알파 (0 = 안 보임, 1 = 불투명)")] float fadedAlpha = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("흐려지는 시간 (초, 실제 시간)")] float fadeOutDuration = 0.2f;
        [SerializeField, Min(0.01f), Tooltip("다시 불투명해지는 시간 (초, 실제 시간)")] float fadeInDuration = 0.35f;

        [Header("범위")]
        [SerializeField, Min(0f), Tooltip("오브젝트 경계를 가로(x · z)로 이만큼 넓혀 판정 (m) — 클수록 옆 조각까지 같이 흐려짐")] float horizontalMargin = 0.7f;
        [SerializeField, Min(0f), Tooltip("오브젝트 경계를 위아래로 넓히는 양 (m)")] float verticalMargin = 0f;
        [SerializeField, Tooltip("플레이어 발에서 잰 확인 높이들 (m). 이 높이 중 하나라도 가려지면 흐려짐")] float[] checkHeights = { 0.3f, 1.6f };
        [SerializeField, Min(0f), Tooltip("플레이어에서 이 거리 안의 가림은 무시 (m) — 바로 옆 · 발밑 오브젝트")] float ignoreNear = 0.3f;
        [SerializeField, Min(0.02f), Tooltip("가림 확인 간격 (초, 실제 시간). 페이드 자체는 매 프레임")] float checkInterval = 0.1f;

        public bool Enabled => enabled && fadeMaterial != null;
        public Material FadeMaterial => fadeMaterial;
        public float FadedAlpha => fadedAlpha;
        public float FadeOutDuration => fadeOutDuration;
        public float FadeInDuration => fadeInDuration;
        public float HorizontalMargin => horizontalMargin;
        public float VerticalMargin => verticalMargin;
        public float[] CheckHeights => checkHeights;
        public float IgnoreNear => ignoreNear;
        public float CheckInterval => checkInterval;
    }
}
