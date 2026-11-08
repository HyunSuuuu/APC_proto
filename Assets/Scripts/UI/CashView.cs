using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>HUD 좌상단 현금 표시 (GDD 12.1, 체크리스트 M4-8). 값이 바뀌면 숫자가 굴러가듯 올라가고, 오를 때 살짝 튄다.</summary>
    public sealed class CashView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField, Tooltip("현금이 오를 때 튀는 대상")] RectTransform punchTarget;
        [SerializeField, Min(0.05f)] float countDuration = 0.4f;

        int shown;
        Tween counter;

        void Start()
        {
            var cash = GameManager.Instance.Wallet.Cash;
            shown = cash.CurrentValue;
            label.text = Format(shown);
            cash.Subscribe(CountTo).AddTo(this);
        }

        void CountTo(int target)
        {
            if (target == shown && counter == null) return;

            var rising = target > shown;
            counter?.Kill();
            counter = DOVirtual.Float(shown, target, countDuration, value =>
                {
                    shown = Mathf.RoundToInt(value);
                    label.text = Format(shown);
                })
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    shown = target;
                    label.text = Format(target);
                    counter = null;
                });

            if (!rising || punchTarget == null) return;
            punchTarget.DOKill(true);
            punchTarget.DOPunchScale(Vector3.one * 0.12f, 0.2f, 6, 0.5f).SetLink(gameObject);
        }

        static string Format(int value) => value.ToString("N0");
    }
}
