using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// HUD 좌상단 돈 패널 (GDD 12.1, 9.1). 한 패널에 두 줄: 위 = **보유 재화**(크게), 아래 = **저금통**(`+180 대기`, 작게).
    /// 값이 바뀌면 숫자가 굴러가듯 바뀌고, 오를 때 해당 줄이 튄다. 정산 때 저금통이 줄고 보유 재화가 오르는 모습이 그대로 보인다.
    /// </summary>
    public sealed class CashView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField, Tooltip("현금이 오를 때 튀는 대상")] RectTransform punchTarget;
        [SerializeField, Tooltip("저금통 줄 (없어도 됨)")] TMP_Text piggyLabel;
        [SerializeField, Min(0.05f)] float countDuration = 0.4f;

        readonly Counter cash = new();
        readonly Counter piggy = new();

        void Start()
        {
            var wallet = GameManager.Instance.Wallet;
            cash.Bind(this, label, v => v.ToString("N0"), punchTarget);
            wallet.Cash.Subscribe(cash.CountTo).AddTo(this);

            if (piggyLabel == null) return;
            piggy.Bind(this, piggyLabel, v => $"+{v:N0} 대기", piggyLabel.rectTransform);
            wallet.Piggy.Subscribe(piggy.CountTo).AddTo(this);
        }

        /// <summary>숫자 하나를 굴려 보여 준다.</summary>
        sealed class Counter
        {
            CashView owner;
            TMP_Text text;
            System.Func<int, string> format;
            RectTransform punch;
            int shown;
            bool bound;
            Tween tween;

            public void Bind(CashView view, TMP_Text target, System.Func<int, string> formatter, RectTransform punchRect)
            {
                owner = view;
                text = target;
                format = formatter;
                punch = punchRect;
            }

            public void CountTo(int target)
            {
                if (!bound)
                {
                    // 처음 값은 바로 보여 준다
                    bound = true;
                    shown = target;
                    text.text = format(shown);
                    return;
                }
                if (target == shown && tween == null) return;

                var rising = target > shown;
                tween?.Kill();
                tween = DOVirtual.Float(shown, target, owner.countDuration, value =>
                    {
                        shown = Mathf.RoundToInt(value);
                        text.text = format(shown);
                    })
                    .SetEase(Ease.OutCubic)
                    .SetLink(owner.gameObject)
                    .OnComplete(() =>
                    {
                        shown = target;
                        text.text = format(target);
                        tween = null;
                    });

                if (!rising || punch == null) return;
                punch.DOKill(true);
                punch.DOPunchScale(Vector3.one * 0.12f, 0.2f, 6, 0.5f).SetLink(owner.gameObject);
            }
        }
    }
}
