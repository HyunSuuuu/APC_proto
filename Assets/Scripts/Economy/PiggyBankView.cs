using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Economy
{
    /// <summary>
    /// 사무실 투명 저금통 (GDD 9.5). 이번 파트 저금통 금액만큼 초록 지폐 층이 **3단계**(`EconomyConfig.piggyStepCount`)로 차오르고, 정산 때 비워진다. 꽉 차도 돈은 계속 모인다 (모습만 멈춤).
    /// 가득 기준 = PartService.PiggyFullAmount (MAX(300, 직전 정산액 × 1.2)). 돈이 들어올 때마다 통이 살짝 튄다.
    /// 사무실 모듈 프리팹 안에 있으므로 GameManager · PartService를 Instance로 찾는다.
    /// </summary>
    public sealed class PiggyBankView : MonoBehaviour
    {
        [SerializeField, Tooltip("동전 층 (y 크기로 높이 조절, 피벗은 바닥)")] Transform fill;
        [SerializeField, Tooltip("튀는 대상 (유리 통)")] Transform jar;
        [SerializeField] TMP_Text amountLabel;
        [SerializeField, Min(0.01f), Tooltip("가득 찼을 때 동전 층 높이 (y 크기)")] float fullHeight = 1f;

        int lastStep = -1;
        int lastAmount;

        void Start()
        {
            fill.localScale = new Vector3(fill.localScale.x, 0f, fill.localScale.z);
            GameManager.Instance.Wallet.Piggy.Subscribe(Refresh).AddTo(this);
        }

        void Refresh(int amount)
        {
            if (amountLabel != null) amountLabel.text = amount > 0 ? amount.ToString("N0") : "";

            var steps = GameManager.Instance.Database.Economy.PiggyStepCount;
            var full = PartService.Instance != null ? PartService.Instance.PiggyFullAmount : 300;
            var step = amount <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt((float)amount / full * steps), 1, steps);

            if (step != lastStep)
            {
                lastStep = step;
                fill.DOKill();
                fill.DOScaleY(fullHeight * step / steps, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            if (amount > lastAmount && jar != null)
            {
                jar.DOKill(true);
                jar.DOPunchScale(new Vector3(0.08f, -0.06f, 0.08f), 0.25f, 6, 0.5f).SetLink(gameObject);
            }
            lastAmount = amount;
        }
    }
}
