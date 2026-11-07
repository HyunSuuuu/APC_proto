using UnityEngine;

namespace APC.UI
{
    /// <summary>월드 글자(수량, 이름 등)가 항상 카메라를 향하게 한다.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        Transform cameraTransform;

        void LateUpdate()
        {
            if (cameraTransform == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                cameraTransform = cam.transform;
            }
            transform.rotation = cameraTransform.rotation;
        }
    }
}
