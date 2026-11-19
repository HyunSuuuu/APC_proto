using APC.Core;
using APC.Logistics;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace APC.Interaction
{
    /// <summary>
    /// 구매 / 해금 패드 (GDD 9.2). 위에 멈춰 서 있으면 보유 현금이 조금씩 들어가고 게이지가 찬다.
    /// 금액과 관계없이 전액이 들어가는 데 padFillDuration(1.5초). 현금이 모자라면 가진 만큼만 들어가고 멈추며, 들어간 금액은 유지된다.
    /// 다 차면 OnPurchased(시설 생성 등) → 경험치 → 패드가 사라진다. 플레이어만 돈을 낸다 (CashCollector가 있는 대상).
    /// </summary>
    public abstract class PurchasePad : GaugeZone
    {
        [SerializeField] TMP_Text costLabel;
        [SerializeField] TMP_Text nameLabel;

        [Header("돈 넣는 연출 (2026-11-19)")]
        [SerializeField, Tooltip("돈이 들어갈 때 통통 튀는 것 (UnlockBox). 비우면 안 튐")] Transform bounceTarget;
        [SerializeField, Tooltip("몸에서 박스로 날아가는 돈 (CashBundle). 비우면 안 날림")] GameObject moneyPrefab;
        [SerializeField, Min(0.02f), Tooltip("돈 하나가 날아가는 간격 (초)")] float moneyInterval = 0.07f;
        [SerializeField, Min(0.05f)] float moneyFlyDuration = 0.25f;

        float nextMoney;
        int cost;
        int paid;
        float owed;
        bool done;

        public int Cost => cost;
        public int Paid => paid;
        public int Remaining => cost - paid;

        static Wallet Wallet => GameManager.Instance.Wallet;

        /// <summary>패드를 쓰기 전에 한 번 부른다.</summary>
        protected void SetupPad(string title, int price)
        {
            cost = Mathf.Max(0, price);
            paid = 0;
            owed = 0f;
            done = false;
            Duration = GameManager.Instance.Database.Economy.PadFillDuration;
            if (nameLabel != null) nameLabel.text = title;
            RefreshCost();
            ResetGauge();
        }

        protected override bool CanProgress(Interactor who)
            => !done && who.GetComponent<CashCollector>() != null && (Remaining == 0 || Wallet.Cash.CurrentValue > 0);

        /// <summary>시간에 비례해 현금을 넣는다. 진행도 = 낸 금액 / 비용.</summary>
        protected override float Advance(Interactor who, float current, float deltaTime)
        {
            if (cost == 0) return 1f;

            owed += cost * deltaTime / Duration;
            var want = Mathf.Min(Mathf.FloorToInt(owed), Remaining);
            if (want > 0)
            {
                var got = Wallet.SpendUpTo(want);
                paid += got;
                owed = got < want ? 0f : owed - got; // 모자라면 여기서 멈춘다
                RefreshCost();
                if (got > 0) PayFeedback(who);
            }
            return (float)paid / cost;
        }

        /// <summary>돈이 들어가는 동안: 몸 → 박스로 돈 다발이 포물선으로 날아가 빨려 들어가고, 박스가 통통 튄다.</summary>
        void PayFeedback(Interactor who)
        {
            if (Time.time < nextMoney) return;
            nextMoney = Time.time + moneyInterval;

            if (bounceTarget != null)
            {
                bounceTarget.DOKill(true);
                bounceTarget.DOPunchScale(new Vector3(0.12f, 0.12f, 0.12f), 0.18f, 6, 0.6f);
            }
            if (moneyPrefab == null || who == null) return;
            var money = Instantiate(moneyPrefab, who.transform.position + Vector3.up * 1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var t = money.transform;
            var end = (bounceTarget != null ? bounceTarget.position : transform.position) + Vector3.up * 0.05f;
            DOTween.Sequence()
                .Append(t.DOJump(end, 1.2f, 1, moneyFlyDuration).SetEase(Ease.Linear))
                .Join(t.DOScale(0.35f, moneyFlyDuration).SetEase(Ease.InQuad))
                .SetLink(money)
                .OnComplete(() => Destroy(money));
        }

        protected sealed override void OnCompleted(Interactor who)
        {
            done = true;
            OnPurchased(who);
            if (GivesXp) GameManager.Instance.Level.AddXp(GameManager.Instance.Database.Economy.XpPurchase);
            Disappear();
        }

        /// <summary>다 냈을 때 XP를 주는가 (시작 순서 발판은 안 줌, GDD 9.6).</summary>
        protected virtual bool GivesXp => true;

        /// <summary>구매 완료 시 할 일 (시설 생성, 칸 해금 등).</summary>
        protected abstract void OnPurchased(Interactor who);

        void RefreshCost()
        {
            if (costLabel != null) costLabel.text = Remaining.ToString("N0");
        }

        /// <summary>존에서 빠지고 작아지며 사라진다.</summary>
        void Disappear()
        {
            enabled = false; // InteractionZone 목록에서 빠짐
            transform.DOKill();
            transform.DOScale(0f, 0.25f).SetEase(Ease.InBack).SetLink(gameObject).OnComplete(() => Destroy(gameObject));
        }
    }
}
