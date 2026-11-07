using APC.Core;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace APC.Controls
{
    /// <summary>
    /// 플로팅 조이스틱 (GDD 3.1). UI가 아닌 곳을 터치하면 그 자리에 나타나고, 손을 떼면 사라진다.
    /// 출력 Value는 크기 0~1 (최대 반경에서 1).
    /// </summary>
    public sealed class FloatingJoystick : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField, Tooltip("조이스틱 바탕. 처음엔 꺼 둔다")] RectTransform baseRect;
        [SerializeField] RectTransform knobRect;

        const int NoPointer = int.MinValue;

        int pointerId = NoPointer;
        Vector2 origin;
        float radius;

        /// <summary>이동 입력 (크기 0~1). 조이스틱이 없으면 zero.</summary>
        public Vector2 Value { get; private set; }

        public bool IsActive => pointerId != NoPointer;

        /// <summary>자유 카메라 모드 등에서 끈다 (GDD 3.2).</summary>
        public bool InputEnabled { get; set; } = true;

        void Start()
        {
            radius = GameManager.Instance.Database.Controls.JoystickRadius;
            baseRect.gameObject.SetActive(false);
        }

        void OnDisable() => Release();

        void Update()
        {
            var gm = GameManager.Instance;
            if (!InputEnabled || gm == null || gm.IsGameplayInputBlocked)
            {
                Release();
                return;
            }

            if (!IsActive) TryCapture();
            if (IsActive) Track();
        }

        void TryCapture()
        {
            foreach (var pointer in PointerInput.Active)
            {
                if (pointer.Phase != TouchPhase.Began || UiHitTest.IsOverUi(pointer.Position)) continue;

                pointerId = pointer.Id;
                origin = pointer.Position;
                baseRect.position = origin; // Screen Space Overlay: 월드 좌표 = 화면 좌표
                knobRect.anchoredPosition = Vector2.zero;
                baseRect.gameObject.SetActive(true);
                return;
            }
        }

        void Track()
        {
            if (!PointerInput.TryGet(pointerId, out var pointer) || pointer.IsEnding)
            {
                Release();
                return;
            }

            var scale = canvas.scaleFactor;
            var radiusPx = radius * scale;
            var offset = Vector2.ClampMagnitude(pointer.Position - origin, radiusPx);
            Value = offset / radiusPx;
            knobRect.anchoredPosition = offset / scale;
        }

        void Release()
        {
            pointerId = NoPointer;
            Value = Vector2.zero;
            if (baseRect != null) baseRect.gameObject.SetActive(false);
        }
    }
}
