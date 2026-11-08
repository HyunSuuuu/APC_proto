using System.Collections;
using System.Collections.Generic;
using APC.Core;
using APC.Data;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 현금 존 (GDD 7.6). 상차 베이마다 상차 존 옆에 있다. 주문 보상이 10원당 1뭉치로 쌓이고(나머지는 마지막 뭉치에 포함),
    /// 플레이어가 반경 안에 들어오면 **멈추지 않아도** 위에서부터 하나씩 빨려 들어가며 현금이 오른다.
    /// 뭉치 오브젝트는 존 안에서 재사용한다.
    /// </summary>
    public sealed class CashZone : MonoBehaviour
    {
        [SerializeField] Transform pileRoot;
        [SerializeField, Tooltip("현금 뭉치 1개 (외형만)")] Transform bundlePrefab;
        [SerializeField] CargoGridLayout layout = new(2, 3, 0.55f, 0.16f);

        readonly List<(Transform bundle, int value)> bundles = new();
        readonly Stack<Transform> spare = new();
        EconomyConfig economy;
        bool collecting;

        public int StoredCash
        {
            get
            {
                var sum = 0;
                foreach (var (_, value) in bundles) sum += value;
                return sum;
            }
        }

        void Start() => economy = GameManager.Instance.Database.Economy;

        /// <summary>보상을 뭉치로 쌓는다 (예: 30 = 3뭉치, 52 = 6뭉치).</summary>
        public void AddCash(int amount)
        {
            if (amount <= 0) return;
            var per = economy.CashPerBundle;
            var count = (amount + per - 1) / per;
            for (var i = 0; i < count; i++)
            {
                var value = i < count - 1 ? per : amount - per * (count - 1);
                var bundle = GetBundle();
                var slot = layout.GetLocalPosition(bundles.Count);
                bundles.Add((bundle, value));

                // 위에서 떨어지며 하나씩 나타난다.
                bundle.DOKill();
                bundle.localPosition = slot + Vector3.up * 1.5f;
                bundle.localRotation = Quaternion.identity;
                bundle.localScale = Vector3.zero;
                var delay = i * 0.04f;
                bundle.DOScale(1f, 0.15f).SetDelay(delay);
                bundle.DOLocalMove(slot, 0.3f).SetDelay(delay).SetEase(Ease.OutBounce);
            }
        }

        void Update()
        {
            if (collecting || bundles.Count == 0) return;

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

        /// <summary>맨 위 뭉치부터 하나씩 대상에게 날려 보낸다. 도착할 때 현금이 오른다.</summary>
        IEnumerator Collect(CashCollector collector)
        {
            collecting = true;
            var wallet = GameManager.Instance.Wallet;
            while (bundles.Count > 0 && collector != null)
            {
                var (bundle, value) = bundles[^1];
                bundles.RemoveAt(bundles.Count - 1);

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
                        wallet.Add(value);
                        ReturnBundle(bundle);
                    });

                yield return new WaitForSeconds(economy.CashAbsorbInterval);
            }
            collecting = false;
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
