using UnityEngine;

namespace APC.Data
{
    /// <summary>조이스틱 · 카메라 (GDD 3.1, 3.2).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Controls Config", fileName = "ControlsConfig")]
    public sealed class ControlsConfig : ScriptableObject
    {
        [Header("조이스틱")]
        [SerializeField, Min(10f), Tooltip("최대 드래그 반경 (HUD 기준 해상도 1920×1080의 px)")] float joystickRadius = 80f;

        [Header("카메라 — 추적")]
        [SerializeField, Range(10f, 89f)] float pitch = 45f;
        [SerializeField, Range(-180f, 180f)] float yaw = 45f;
        [SerializeField, Min(0.01f), Tooltip("추적 감쇠 (초)")] float followDamping = 0.15f;

        [Header("카메라 — 줌 (카메라 거리)")]
        [SerializeField, Min(1f)] float defaultDistance = 18f;
        [SerializeField, Min(1f)] float minDistance = 12f;
        [SerializeField, Min(1f)] float maxDistance = 30f;
        [SerializeField, Min(0.1f), Tooltip("에디터/PC 마우스 휠 한 칸당 거리 변화")] float scrollZoomStep = 2f;

        [Header("카메라 — 자유 이동")]
        [SerializeField, Min(0.01f), Tooltip("자유 모드 → 추적 복귀 시간 (초)")] float returnDuration = 0.4f;

        public float JoystickRadius => joystickRadius;
        public float Pitch => pitch;
        public float Yaw => yaw;
        public float FollowDamping => followDamping;
        public float DefaultDistance => defaultDistance;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float ScrollZoomStep => scrollZoomStep;
        public float ReturnDuration => returnDuration;

        public float ClampDistance(float distance) => Mathf.Clamp(distance, minDistance, maxDistance);
    }
}
