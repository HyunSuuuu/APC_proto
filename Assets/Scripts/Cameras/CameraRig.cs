using System.Collections.Generic;
using APC.Controls;
using APC.Core;
using APC.Data;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace APC.Cameras
{
    public enum CameraMode
    {
        /// <summary>쿼터뷰 고정 각도로 플레이어를 따라간다.</summary>
        Follow,

        /// <summary>드래그로 맵 이동, 핀치로 줌. 조이스틱은 꺼진다.</summary>
        Free,
    }

    /// <summary>
    /// 쿼터뷰 카메라 (GDD 3.2). 바닥 위의 초점(focus)을 기준으로 고정 각도·거리에 카메라를 둔다.
    /// 각도 · 거리 · 렌즈는 모두 ControlsConfig에서 매 프레임 읽으므로 Play 중 수정이 바로 반영된다.
    /// Play 전 에디터에서도 config대로 플레이어를 비춰 미리 볼 수 있다. Main Camera에 붙인다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;

        readonly ReactiveProperty<CameraMode> mode = new(CameraMode.Follow);
        readonly HashSet<int> uiPointers = new();
        readonly List<PointerSample> dragPointers = new();

        Camera cam;
        ControlsConfig config;
        Vector3 focus;
        Vector3 focusVelocity;
        float distance;
        Tween returnTween;
        bool spotlight; // 튜토리얼 강조 중: 플레이어를 따라가지 않는다
        bool topDown;   // 벨트 설치 상태 (GDD 7.10): 따라가지 않고 바깥(BeltPlacer)이 이동 · 줌
        float topDownBlend; // 0 = config 각도, 1 = 위에서 수직
        float topDownMaxDistance;

        public ReadOnlyReactiveProperty<CameraMode> Mode => mode;

        /// <summary>카메라 Yaw. 조이스틱 입력을 화면 기준 방향으로 바꿀 때 쓴다.</summary>
        public float Yaw => config != null ? config.Yaw : transform.eulerAngles.y;

        void Awake() => cam = GetComponent<Camera>();

        void Start()
        {
            if (!Application.isPlaying) return;
            config = GameManager.Instance.Database.Controls;
            distance = config.DefaultDistance;
            focus = target != null ? target.position : Vector3.zero;
            ApplyCamera();
        }

        void OnDestroy() => mode.Dispose();

#if UNITY_EDITOR
        void OnEnable() => ControlsConfig.EditorValuesChanged += OnConfigEdited;

        void OnDisable() => ControlsConfig.EditorValuesChanged -= OnConfigEdited;

        void OnConfigEdited()
        {
            if (Application.isPlaying || this == null) return;
            PreviewInEditor();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                PreviewInEditor();
                return;
            }
            if (config == null) return;

            if (mode.Value == CameraMode.Free)
            {
                HandleFreeInput();
            }
            else if (returnTween == null && !spotlight && !topDown)
            {
                distance = config.DefaultDistance;
                if (target != null) focus = Vector3.SmoothDamp(focus, target.position, ref focusVelocity, config.FollowDamping);
            }

            ApplyCamera();
        }

        /// <summary>에디터(Play 전): config대로 플레이어를 비춘다. 값이 같으면 건드리지 않아 씬이 바뀐 것으로 표시되지 않는다.</summary>
        void PreviewInEditor()
        {
            if (target == null) return;
            var manager = FindFirstObjectByType<GameManager>();
            var controls = manager != null && manager.Database != null ? manager.Database.Controls : null;
            if (controls == null) return;

            if (cam == null) cam = GetComponent<Camera>();
            config = controls;
            focus = target.position;
            distance = controls.DefaultDistance;
            ApplyCamera();
        }

        /// <summary>HUD 카메라 버튼. 추적 ↔ 자유 전환.</summary>
        public void ToggleMode()
        {
            if (mode.Value == CameraMode.Follow) EnterFree();
            else ReturnToFollow();
        }

        void EnterFree()
        {
            returnTween?.Kill();
            returnTween = null;
            uiPointers.Clear();
            mode.Value = CameraMode.Free;
        }

        /// <summary>플레이어 위치 · 기본 거리로 부드럽게 돌아온다 (0.4초).</summary>
        void ReturnToFollow()
        {
            mode.Value = CameraMode.Follow;
            var startFocus = focus;
            var startDistance = distance;
            returnTween = DOVirtual.Float(0f, 1f, config.ReturnDuration, t =>
                {
                    var goal = target != null ? target.position : startFocus;
                    focus = Vector3.Lerp(startFocus, goal, t);
                    distance = Mathf.Lerp(startDistance, config.DefaultDistance, t);
                })
                .SetEase(Ease.OutCubic)
                .SetUpdate(true) // 튜토리얼 강조 뒤 멈춘 시간에서도 돌아온다
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    returnTween = null;
                    focusVelocity = Vector3.zero;
                });
        }

        /// <summary>
        /// 튜토리얼 강조 (GDD 13): 카메라를 `point`로 옮기고 기본 거리 × `zoom`으로 확대한다. 시간이 멈춰 있어도 움직인다 (unscaled).
        /// 끝나면 EndSpotlight로 플레이어에게 돌아온다.
        /// </summary>
        public Tween Spotlight(Vector3 point, float zoom, float duration)
        {
            returnTween?.Kill();
            returnTween = null;
            mode.Value = CameraMode.Follow;
            spotlight = true;
            var startFocus = focus;
            var startDistance = distance;
            var goalDistance = config.DefaultDistance * zoom;
            return DOVirtual.Float(0f, 1f, duration, t =>
                {
                    focus = Vector3.Lerp(startFocus, point, t);
                    distance = Mathf.Lerp(startDistance, goalDistance, t);
                })
                .SetEase(Ease.InOutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void EndSpotlight()
        {
            spotlight = false;
            ReturnToFollow();
        }

        /// <summary>
        /// 벨트 설치 상태 (GDD 7.10): 위에서 수직으로 내려다보며(Yaw 0 = 격자가 화면과 나란히) area 전체가 보이게 줌아웃한다. 시간이 멈춰도 움직인다.
        /// 이동 · 줌은 PanTopDown · ZoomTopDown, 끝나면 EndTopDown으로 플레이어에게 돌아온다.
        /// </summary>
        public void BeginTopDown(Bounds area, float duration)
        {
            returnTween?.Kill();
            returnTween = null;
            mode.Value = CameraMode.Follow;
            topDown = true;
            // 직교: 화면 반 높이 = OrthographicSize × 거리 / 기본 거리 → area가 들어가는 거리
            var halfHeight = Mathf.Max(area.extents.z, area.extents.x / Mathf.Max(0.1f, cam.aspect)) * 1.1f;
            var goal = config.Orthographic
                ? config.DefaultDistance * halfHeight / config.OrthographicSize
                : halfHeight / Mathf.Tan(config.FieldOfView * 0.5f * Mathf.Deg2Rad);
            topDownMaxDistance = Mathf.Max(goal, config.MaxDistance) * 1.5f;
            var startFocus = focus;
            var startDistance = distance;
            var startBlend = topDownBlend;
            var center = new Vector3(area.center.x, 0f, area.center.z);
            returnTween = DOVirtual.Float(0f, 1f, duration, t =>
                {
                    focus = Vector3.Lerp(startFocus, center, t);
                    distance = Mathf.Lerp(startDistance, goal, t);
                    topDownBlend = Mathf.Lerp(startBlend, 1f, t);
                })
                .SetEase(Ease.InOutCubic)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => returnTween = null);
        }

        /// <summary>설치 상태: 화면의 from 아래 바닥 지점이 to로 따라오게 옮긴다.</summary>
        public void PanTopDown(Vector2 from, Vector2 to)
        {
            if (topDown && TryGroundPoint(from, out var a) && TryGroundPoint(to, out var b)) focus += a - b;
        }

        /// <summary>설치 상태: 거리 × factor (1보다 작으면 확대).</summary>
        public void ZoomTopDown(float factor)
        {
            if (topDown) distance = Mathf.Clamp(distance * factor, config.MinDistance, topDownMaxDistance);
        }

        public void EndTopDown()
        {
            if (!topDown) return;
            topDown = false;
            returnTween?.Kill();
            var startBlend = topDownBlend;
            DOVirtual.Float(startBlend, 0f, config.ReturnDuration, t => topDownBlend = t).SetUpdate(true).SetLink(gameObject);
            ReturnToFollow();
        }

        /// <summary>화면 위치 → 바닥(y = 0) 지점.</summary>
        public bool ScreenToGround(Vector2 screenPosition, out Vector3 point) => TryGroundPoint(screenPosition, out point);

        void HandleFreeInput()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.IsGameplayInputBlocked) return;

            CollectDragPointers();

            if (dragPointers.Count == 1)
            {
                // 손가락 아래의 바닥 지점이 손가락을 따라오도록 초점을 옮긴다.
                var pointer = dragPointers[0];
                if (pointer.Delta.sqrMagnitude > 0f &&
                    TryGroundPoint(pointer.PreviousPosition, out var from) &&
                    TryGroundPoint(pointer.Position, out var to))
                {
                    focus += from - to;
                }
            }
            else if (dragPointers.Count >= 2)
            {
                var a = dragPointers[0];
                var b = dragPointers[1];
                var before = Vector2.Distance(a.PreviousPosition, b.PreviousPosition);
                var now = Vector2.Distance(a.Position, b.Position);
                if (before > 1f && now > 1f) distance = config.ClampDistance(distance * before / now);
            }

            // 에디터 · PC: 마우스 휠 줌
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                    distance = config.ClampDistance(distance - Mathf.Sign(scroll) * config.ScrollZoomStep);
            }
        }

        /// <summary>UI 위에서 시작하지 않은 포인터만 모은다.</summary>
        void CollectDragPointers()
        {
            dragPointers.Clear();
            foreach (var pointer in PointerInput.Active)
            {
                if (pointer.Phase == TouchPhase.Began && UiHitTest.IsOverUi(pointer.Position)) uiPointers.Add(pointer.Id);

                if (pointer.IsEnding)
                {
                    uiPointers.Remove(pointer.Id);
                    continue;
                }
                if (!uiPointers.Contains(pointer.Id)) dragPointers.Add(pointer);
            }
        }

        bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        void ApplyCamera()
        {
            var rotation = Quaternion.Euler(Mathf.Lerp(config.Pitch, 90f, topDownBlend), Mathf.LerpAngle(config.Yaw, 0f, topDownBlend), 0f);
            var position = focus + Vector3.up * config.FocusHeight + rotation * new Vector3(0f, 0f, -distance);
            if (transform.position != position || transform.rotation != rotation)
                transform.SetPositionAndRotation(position, rotation);

            if (cam.orthographic != config.Orthographic) cam.orthographic = config.Orthographic;
            if (config.Orthographic)
            {
                // 직교 투영은 거리로 크기가 안 바뀌므로, 줌(거리)에 비례해 화면 크기를 바꾼다.
                var size = config.OrthographicSize * distance / config.DefaultDistance;
                if (!Mathf.Approximately(cam.orthographicSize, size)) cam.orthographicSize = size;
            }
            else if (!Mathf.Approximately(cam.fieldOfView, config.FieldOfView))
            {
                cam.fieldOfView = config.FieldOfView;
            }
        }
    }
}
