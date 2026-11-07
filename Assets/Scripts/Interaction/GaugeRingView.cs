using R3;
using UnityEngine;
using UnityEngine.UI;

namespace APC.Interaction
{
    /// <summary>게이지 존 바닥의 원형 게이지 (월드 캔버스 Image, Filled).</summary>
    public sealed class GaugeRingView : MonoBehaviour
    {
        [SerializeField] GaugeZone zone;
        [SerializeField] Image fill;

        void Start()
        {
            zone.Progress.Subscribe(value => fill.fillAmount = value).AddTo(this);
        }
    }
}
