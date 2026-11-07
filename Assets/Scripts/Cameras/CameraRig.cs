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
    /// Main Camera에 붙인다.
    /// </summary>
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

        public ReadOnlyReactiveProperty<CameraMode> Mode => mode;

        /// <summary>카메라 Yaw. 조이스틱 입력을 화면 기준 방향으로 바꿀 때 쓴다.</summary>
        public float Yaw => config != null ? config.Yaw : transform.eulerAngles.y;

        void Awake() => cam = GetComponent<Camera>();

        void Start()
        {
            config = GameManager.Instance.Database.Controls;
            distance = config.DefaultDistance;
            focus = target != null ? target.position : Vector3.zero;
            ApplyTransform();
        }

        void OnDestroy() => mode.Dispose();

        void LateUpdate()
        {
            if (config == null) return;

            if (mode.Value == CameraMode.Free) HandleFreeInput();
            else if (returnTween == null && target != null)
                focus = Vector3.SmoothDamp(focus, target.position, ref focusVelocity, config.FollowDamping);

            ApplyTransform();
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
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    returnTween = null;
                    focusVelocity = Vector3.zero;
                });
        }

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

        void ApplyTransform()
        {
            var rotation = Quaternion.Euler(config != null ? config.Pitch : 45f, Yaw, 0f);
            transform.SetPositionAndRotation(focus + rotation * new Vector3(0f, 0f, -distance), rotation);
        }
    }
}
