using APC.Economy;
using R3;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>HUD 파트 시계 (GDD 12.1): `3파트 · 01:42`. 계약 전(파트 0)에는 숨기고, 10초 남으면 빨갛게.</summary>
    public sealed class PartClockView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text label;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color warningColor = new(1f, 0.45f, 0.35f);
        [SerializeField, Min(0f)] float warningSeconds = 10f;

        void Start()
        {
            var parts = PartService.Instance;
            if (parts == null)
            {
                root.SetActive(false);
                return;
            }

            parts.Part.Subscribe(_ => Refresh(parts)).AddTo(this);
            parts.Remaining.Subscribe(_ => Refresh(parts)).AddTo(this);
        }

        void Refresh(PartService parts)
        {
            var number = parts.Part.CurrentValue;
            root.SetActive(number > 0);
            if (number <= 0) return;

            var seconds = Mathf.CeilToInt(parts.Remaining.CurrentValue);
            var kind = number == PartService.InboundPart ? " 하차" : number == PartService.OutboundPart ? " 상차" : ""; // 1 · 2 반복 (2026-11-19)
            label.text = parts.Tutorial ? $"{number}파트{kind} · 튜토리얼" : $"{number}파트{kind} · {seconds / 60:00}:{seconds % 60:00}";
            label.color = !parts.Tutorial && parts.Remaining.CurrentValue <= warningSeconds ? warningColor : normalColor; // 튜토리얼은 시계 0이라 경고 색 빼기
        }
    }
}
