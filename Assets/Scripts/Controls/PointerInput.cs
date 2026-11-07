using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace APC.Controls
{
    /// <summary>이번 프레임의 손가락(또는 마우스) 1개.</summary>
    public readonly struct PointerSample
    {
        public readonly int Id;
        public readonly Vector2 Position;
        public readonly Vector2 Delta;
        public readonly TouchPhase Phase;

        public PointerSample(int id, Vector2 position, Vector2 delta, TouchPhase phase)
        {
            Id = id;
            Position = position;
            Delta = delta;
            Phase = phase;
        }

        public Vector2 PreviousPosition => Position - Delta;
        public bool IsEnding => Phase is TouchPhase.Ended or TouchPhase.Canceled;
    }

    /// <summary>
    /// 터치와 마우스를 하나의 목록으로 읽는다. 터치스크린이 있으면 터치만(실기기, Device Simulator),
    /// 없으면 마우스 왼쪽 버튼을 손가락 1개로 취급한다(에디터 Game 뷰, PC).
    /// Input System의 TouchSimulation은 에디터 사용자 설정에 따라 꺼지므로 쓰지 않는다.
    /// </summary>
    public static class PointerInput
    {
        public const int MouseId = -1;

        static readonly List<PointerSample> Samples = new();
        static int cachedFrame = -1;

        /// <summary>이번 프레임의 포인터 목록 (끝난 포인터도 Ended 상태로 한 번 포함).</summary>
        public static IReadOnlyList<PointerSample> Active
        {
            get
            {
                if (cachedFrame != Time.frameCount) Refresh();
                return Samples;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            cachedFrame = -1;
        }

        public static bool TryGet(int id, out PointerSample sample)
        {
            foreach (var s in Active)
            {
                if (s.Id != id) continue;
                sample = s;
                return true;
            }
            sample = default;
            return false;
        }

        static void Refresh()
        {
            cachedFrame = Time.frameCount;
            Samples.Clear();

            if (Touchscreen.current != null)
            {
                foreach (var touch in Touch.activeTouches)
                    Samples.Add(new PointerSample(touch.finger.index, touch.screenPosition, touch.delta, touch.phase));
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            var button = mouse.leftButton;
            var position = mouse.position.ReadValue();
            var delta = mouse.delta.ReadValue();
            if (button.wasPressedThisFrame) Samples.Add(new PointerSample(MouseId, position, Vector2.zero, TouchPhase.Began));
            else if (button.wasReleasedThisFrame) Samples.Add(new PointerSample(MouseId, position, delta, TouchPhase.Ended));
            else if (button.isPressed)
                Samples.Add(new PointerSample(MouseId, position, delta, delta == Vector2.zero ? TouchPhase.Stationary : TouchPhase.Moved));
        }
    }
}
