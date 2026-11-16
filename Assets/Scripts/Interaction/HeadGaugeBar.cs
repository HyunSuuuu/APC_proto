using UnityEngine;
using UnityEngine.UI;

namespace APC.Interaction
{
    /// <summary>
    /// 머리 위 가로 게이지 막대 (GDD 5.2). 게이지 존(사무실 고용 · 업그레이드, 반품 검수)에서 게이지가 차는 동안만 보인다.
    /// 구매 패드(확장 패드)는 자기 바닥 게이지를 쓰므로 제외한다.
    /// </summary>
    public sealed class HeadGaugeBar : MonoBehaviour
    {
        [SerializeField] Interactor interactor;
        [SerializeField, Tooltip("월드 캔버스 루트. 캐릭터 자식이어도 되고 회전은 카메라를 따른다")] Transform bar;
        [SerializeField, Tooltip("Filled · Horizontal Image")] Image fill;

        Camera cam;

        void Start()
        {
            cam = Camera.main;
            bar.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var zone = interactor.CurrentZone as GaugeZone;
            var value = zone != null && zone is not PurchasePad ? zone.Progress.CurrentValue : 0f;
            var show = value > 0f && value < 1f;
            if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
            if (!show) return;
            fill.fillAmount = value;
            if (cam != null) bar.rotation = cam.transform.rotation;
        }
    }
}
