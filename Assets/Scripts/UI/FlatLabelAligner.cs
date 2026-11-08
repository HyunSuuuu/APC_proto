using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 바닥에 눕힌 글자를 카메라 방향으로 돌려 놓는다. 모듈이 회전해서 붙어도 글자가 뒤집히지 않는다.
    /// </summary>
    [ExecuteAlways]
    public sealed class FlatLabelAligner : MonoBehaviour
    {
        void OnEnable()
        {
            var cam = Camera.main;
            var yaw = cam != null ? cam.transform.eulerAngles.y : 45f;
            transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        }
    }
}
