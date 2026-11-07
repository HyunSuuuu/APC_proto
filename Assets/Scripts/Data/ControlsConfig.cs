using UnityEngine;

namespace APC.Data
{
    /// <summary>
    /// 조이스틱 · 카메라 (GDD 3.1, 3.2). 카메라 값은 Play 중에도 바로 적용되고,
    /// Play 전 에디터에서도 Game 뷰에 미리 보인다 (CameraRig).
    /// </summary>
    [CreateAssetMenu(menuName = "APC/Data/Controls Config", fileName = "ControlsConfig")]
    public sealed class ControlsConfig : ScriptableObject
    {
        [Header("조이스틱")]
        [SerializeField, Min(10f), Tooltip("최대 드래그 반경 (HUD 기준 해상도 1920×1080의 px)")] float joystickRadius = 80f;

        [Header("카메라 — 시점")]
        [SerializeField, Range(10f, 89f), Tooltip("내려다보는 각도. 90에 가까울수록 위에서 수직으로 봄")] float pitch = 45f;
        [SerializeField, Range(-180f, 180f), Tooltip("45 = 부지가 마름모(아이소메트릭)로 보임 (GDD 4.2). 바닥 글자 방향은 Build Site를 다시 실행해야 맞춰짐")] float yaw = 45f;
        [SerializeField, Tooltip("카메라가 바라보는 높이 (플레이어 발 기준 m). 올리면 플레이어가 화면 아래로 내려감")] float focusHeight = 0f;
        [SerializeField, Min(0.01f), Tooltip("추적 감쇠 (초)")] float followDamping = 0.15f;

        [Header("카메라 — 렌즈")]
        [SerializeField, Tooltip("켜면 원근감 없는 직교 투영 (아이소메트릭 게임에서 흔함)")] bool orthographic = false;
        [SerializeField, Range(10f, 90f), Tooltip("원근 투영의 세로 시야각")] float fieldOfView = 60f;
        [SerializeField, Min(1f), Tooltip("직교 투영일 때 화면 세로 절반 크기 (m). 기본 거리 기준이며 줌하면 비례해서 바뀜")] float orthographicSize = 9f;

        [Header("카메라 — 줌 (카메라 거리)")]
        [SerializeField, Min(1f), Tooltip("추적 모드 거리")] float defaultDistance = 18f;
        [SerializeField, Min(1f)] float minDistance = 12f;
        [SerializeField, Min(1f)] float maxDistance = 30f;
        [SerializeField, Min(0.1f), Tooltip("에디터/PC 마우스 휠 한 칸당 거리 변화")] float scrollZoomStep = 2f;

        [Header("카메라 — 자유 이동")]
        [SerializeField, Min(0.01f), Tooltip("자유 모드 → 추적 복귀 시간 (초)")] float returnDuration = 0.4f;

        public float JoystickRadius => joystickRadius;
        public float Pitch => pitch;
        public float Yaw => yaw;
        public float FocusHeight => focusHeight;
        public float FollowDamping => followDamping;
        public bool Orthographic => orthographic;
        public float FieldOfView => fieldOfView;
        public float OrthographicSize => orthographicSize;
        public float DefaultDistance => defaultDistance;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float ScrollZoomStep => scrollZoomStep;
        public float ReturnDuration => returnDuration;

        public float ClampDistance(float distance) => Mathf.Clamp(distance, minDistance, maxDistance);

#if UNITY_EDITOR
        /// <summary>인스펙터에서 값을 바꾸면 알린다 (Play 전 카메라 미리보기 갱신용).</summary>
        public static event System.Action EditorValuesChanged;

        void OnValidate() => EditorValuesChanged?.Invoke();
#endif
    }
}
