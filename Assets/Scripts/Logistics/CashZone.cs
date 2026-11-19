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
    /// 플레이어가 **이 존이 있는 상차장 모듈 칸 안에 들어서면** 멈추지 않아도 다발이 하나씩 플레이어에게 날아가고, 닿으면 달러 아이콘이
    /// HUD 저금통으로 날아가 도착할 때 **저금통**에 들어간다 (CashFlyEffect, 알바는 줍지 않음). 2026-11-19 반경 → 모듈 칸.
    /// 먹을 돈이 있는 동안 모듈 외곽에 초록 글로우(TileGlow, 정적). 모듈이 없으면 존 바닥 타일(`tileSize`)로.
    /// 상차 존도 같은 모듈 안이라 상차하는 동안에도 돈이 들어온다 (2026-11-19 사용자 확정). 돈 때문에 벽을 반투명하게 하지는 않는다 (2026-11-19 삭제).
    /// 줍지 않은 돈은 사라지지 않는다 (다음 파트로 넘어감).
    /// </summary>
    public sealed class CashZone : MonoBehaviour
    {
        [SerializeField] Transform pileRoot;
        [SerializeField, Tooltip("지폐 다발 1개 (외형만)")] Transform bundlePrefab;
        [SerializeField, Tooltip("다발 0.65 × 0.18 × 0.36 (×1.3, 2026-11-19)에 맞춰 빈틈없이")] CargoGridLayout layout = new(4, 5, 0.66f, 0.185f, 0.37f);
        [SerializeField, Tooltip("다발이 최대 수를 넘으면 쌓인 총액을 보여 주는 글자 (없어도 됨)")] TMP_Text overflowLabel;
        [Header("줍는 구역 · 글로우 (2026-11-19)")]
        [SerializeField, Tooltip("켜면 이 존이 있는 모듈(SiteModule) 칸 전체가 줍는 구역 · 글로우 테두리")] bool useModuleArea = true;
        [SerializeField, Tooltip("모듈이 없거나 끈 경우 줍는 바닥 타일 크기 (존 로컬 X × Z, m)")] Vector2 tileSize = new(2.4f, 2.9f);
        [SerializeField, Min(0f), Tooltip("글로우 테두리를 구역 가장자리에서 안으로 들이는 거리 (m, 벽 밑에 묻히지 않게)")] float glowInset = 0.2f;
        [SerializeField, Tooltip("테두리 글로우 재질 (M_CashGlow). 비우면 글로우 없음")] Material glowMaterial;
        [SerializeField, Min(0f), Tooltip("글로우가 테두리 안쪽으로 퍼지는 폭 (m)")] float glowInner = 0.6f;
        [SerializeField, Min(0f), Tooltip("글로우가 테두리 바깥쪽으로 퍼지는 폭 (m)")] float glowOuter = 0.15f;
        [SerializeField, Min(0f), Tooltip("글로우 높이 (구역 기준, m). 바닥 아트 타일 윗면보다 위")] float glowHeight = 0.08f;

        readonly List<Transform> bundles = new();
        readonly Stack<Transform> spare = new();
        EconomyConfig economy;
        GameObject glow;
        Transform area;     // 줍는 구역 기준 (모듈 루트 또는 이 존)
        Vector2 areaSize;   // 구역 로컬 X × Z
        int amount;
        bool collecting;

        /// <summary>쌓여 있는 금액 (아직 줍지 않음).</summary>
        public int StoredCash => amount;

        void Start()
        {
            economy = GameManager.Instance.Database.Economy;
            var module = useModuleArea ? GetComponentInParent<Site.SiteModule>() : null;
            area = module != null ? module.transform : transform;
            areaSize = module != null ? (Vector2)module.Size * module.CellSize : tileSize;
            if (glowMaterial != null)
            {
                var glowSize = areaSize - Vector2.one * (glowInset * 2f);
                glow = Visuals.TileGlow.Create(area, glowSize, glowInner, glowOuter, glowHeight, glowMaterial);
            }
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

            foreach (var collector in CashCollector.All)
            {
                if (!IsOnTile(collector.transform.position)) continue;
                StartCoroutine(Collect(collector));
                return;
            }
        }

        /// <summary>월드 위치가 줍는 구역(모듈 칸 또는 바닥 타일) 사각형 안인가 (회전 반영, 높이 무시).</summary>
        bool IsOnTile(Vector3 position)
        {
            if (area == null) return false;
            var local = area.InverseTransformPoint(position);
            return Mathf.Abs(local.x) <= areaSize.x * 0.5f && Mathf.Abs(local.z) <= areaSize.y * 0.5f;
        }

        /// <summary>반경과 상관없이 바로 줍게 한다 (디버그 메뉴). 이미 줍는 중이거나 돈이 없으면 무시.</summary>
        public void CollectBy(CashCollector collector)
        {
            if (collecting || amount <= 0 || collector == null) return;
            StartCoroutine(Collect(collector));
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
            if (glow != null && glow.activeSelf != amount > 0) glow.SetActive(amount > 0);
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
            // 지폐 다발이 쌓이는 자리 (첫 층)
            CargoGizmos.DrawSlots(pileRoot != null ? pileRoot : transform, layout.GetLocalPosition, layout.columns * layout.rows, new Color(0.4f, 1f, 0.4f),
                new Vector3(layout.spacing, layout.layerHeight, layout.RowSpacing));
            // 줍는 구역 (실행 중엔 모듈 칸, 아니면 바닥 타일)
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.6f);
            var gizmoArea = area != null ? area : transform;
            var gizmoSize = area != null ? areaSize : tileSize;
            Gizmos.matrix = gizmoArea.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(gizmoSize.x, 0.1f, gizmoSize.y));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
