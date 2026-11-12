using System.Collections;
using System.Collections.Generic;
using APC.Core;
using APC.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 현금 존 (GDD 7.6). 상차장마다 상차 존 옆에 있다. 출고 트럭이 출발하면 `실린 상자 수 × 계약 단가`가 쌓인다.
    /// 10원당 지폐 다발 1개로 보여 주되 **최대 40개**까지만 보이고, 넘으면 위에 `+금액`을 띄운다.
    /// 플레이어가 반경 안에 들어오면 **멈추지 않아도** 다발이 하나씩 플레이어에게 날아가고, 닿으면 달러 아이콘이
    /// HUD 저금통으로 날아가 도착할 때 **저금통**에 들어간다 (CashFlyEffect, 알바는 줍지 않음).
    /// 줍지 않은 돈은 사라지지 않는다 (다음 파트로 넘어감).
    /// </summary>
    public sealed class CashZone : MonoBehaviour
    {
        [SerializeField] Transform pileRoot;
        [SerializeField, Tooltip("지폐 다발 1개 (외형만)")] Transform bundlePrefab;
        [SerializeField] CargoGridLayout layout = new(4, 5, 0.55f, 0.16f);
        [SerializeField, Tooltip("다발이 최대 수를 넘으면 쌓인 총액을 보여 주는 글자 (없어도 됨)")] TMP_Text overflowLabel;

        readonly List<Transform> bundles = new();
        readonly Stack<Transform> spare = new();
        EconomyConfig economy;
        int amount;
        bool collecting;

        /// <summary>쌓여 있는 금액 (아직 줍지 않음).</summary>
        public int StoredCash => amount;

        void Start()
        {
            economy = GameManager.Instance.Database.Economy;
            RefreshOverflow();
        }

        /// <summary>출고 현금을 쌓는다. 보이는 다발 = min(최대 수, 올림(금액 ÷ 다발 금액)).</summary>
        public void AddCash(int value)
        {
            if (value <= 0) return;
            amount += value;
            var target = VisibleCountFor(amount);
            var spawned = 0;
            while (bundles.Count < target)
            {
                var bundle = GetBundle();
                var slot = layout.GetLocalPosition(bundles.Count);
                bundles.Add(bundle);

                // 위에서 떨어지며 하나씩 나타난다.
                bundle.DOKill();
                bundle.localPosition = slot + Vector3.up * 1.5f;
                bundle.localRotation = Quaternion.identity;
                bundle.localScale = Vector3.zero;
                var delay = spawned++ * 0.04f;
                bundle.DOScale(1f, 0.15f).SetDelay(delay);
                bundle.DOLocalMove(slot, 0.3f).SetDelay(delay).SetEase(Ease.OutBounce);
            }
            RefreshOverflow();
        }

        void Update()
        {
            if (collecting || amount <= 0) return;

            var radius = economy.CashPickupRadius;
            var center = transform.position;
            foreach (var collector in CashCollector.All)
            {
                var offset = collector.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius) continue;
                StartCoroutine(Collect(collector));
                return;
            }
        }

        /// <summary>맨 위 다발부터 하나씩 대상에게 날려 보낸다. 다발마다 남은 금액을 나눠 싣고, 도착할 때 저금통이 오른다.</summary>
        IEnumerator Collect(CashCollector collector)
        {
            collecting = true;
            var wallet = GameManager.Instance.Wallet;
            while (bundles.Count > 0 && collector != null)
            {
                // 넘친 금액은 보이는 다발들에 고르게 실어 보낸다 (마지막 다발이 나머지 전부).
                var value = bundles.Count == 1 ? amount : Mathf.CeilToInt((float)amount / bundles.Count);
                amount -= value;
                var bundle = bundles[^1];
                bundles.RemoveAt(bundles.Count - 1);
                RefreshOverflow();

                bundle.DOKill();
                bundle.SetParent(null, true);
                var start = bundle.position;
                DOVirtual.Float(0f, 1f, economy.CashFlyDuration, t =>
                    {
                        // 움직이는 플레이어를 따라가는 포물선
                        var target = collector != null ? collector.CatchPoint : start;
                        bundle.position = Vector3.Lerp(start, target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.2f);
                        bundle.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
                    })
                    .SetEase(Ease.InQuad)
                    .SetLink(bundle.gameObject)
                    .OnComplete(() =>
                    {
                        // 플레이어에게 닿으면 달러 아이콘이 HUD 저금통으로 날아가고, 도착할 때 저금통에 더한다
                        var fx = UI.CashFlyEffect.Instance;
                        if (fx != null) fx.Fly(bundle.position, () => wallet.AddToPiggy(value));
                        else wallet.AddToPiggy(value);
                        ReturnBundle(bundle);
                    });

                yield return new WaitForSeconds(economy.CashAbsorbInterval);
            }
            collecting = false;
        }

        int VisibleCountFor(int total)
        {
            var per = Mathf.Max(1, economy != null ? economy.CashPerBundle : 10);
            var max = economy != null ? economy.CashMaxVisibleBundles : 40;
            return Mathf.Min(max, (total + per - 1) / per);
        }

        void RefreshOverflow()
        {
            if (overflowLabel == null || economy == null) return;
            var overflow = amount > bundles.Count * economy.CashPerBundle && bundles.Count >= economy.CashMaxVisibleBundles;
            overflowLabel.gameObject.SetActive(overflow);
            if (overflow) overflowLabel.text = $"+{amount:N0}";
        }

        Transform GetBundle()
        {
            var bundle = spare.Count > 0 ? spare.Pop() : Instantiate(bundlePrefab, pileRoot);
            bundle.SetParent(pileRoot, false);
            bundle.gameObject.SetActive(true);
            return bundle;
        }

        void ReturnBundle(Transform bundle)
        {
            if (bundle == null) return;
            bundle.DOKill();
            bundle.gameObject.SetActive(false);
            bundle.SetParent(pileRoot, false);
            spare.Push(bundle);
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying || economy == null) return;
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, economy.CashPickupRadius);
        }
    }
}
