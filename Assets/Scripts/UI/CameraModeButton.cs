using APC.Cameras;
using APC.Controls;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// HUD 우하단 카메라 버튼 (GDD 3.2, 12.1). 추적 ↔ 자유 모드를 바꾸고, 자유 모드에서는 조이스틱을 끈다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class CameraModeButton : MonoBehaviour
    {
        [SerializeField] CameraRig cameraRig;
        [SerializeField] FloatingJoystick joystick;
        [SerializeField] Text label;
        [SerializeField] string followLabel = "CAM";
        [SerializeField] string freeLabel = "BACK";

        void Start()
        {
            GetComponent<Button>().onClick.AddListener(cameraRig.ToggleMode);

            cameraRig.Mode
                .Subscribe(mode =>
                {
                    var free = mode == CameraMode.Free;
                    joystick.InputEnabled = !free;
                    if (label != null) label.text = free ? freeLabel : followLabel;
                })
                .AddTo(this);
        }
    }
}
