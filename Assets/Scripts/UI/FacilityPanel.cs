using System.Collections.Generic;
using APC.Core;
using APC.Logistics;
using APC.Site;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// B책상 `설비` 탭 (GDD 7.3, M16-3): 선반이 세워진 보관 모듈마다 한 줄 — `지금 단계 용량 → 다음 단계 용량` + 비용 버튼.
    /// 사면 그 모듈 선반 4개(팔레트 랙도)가 한꺼번에 다음 단계가 된다. 레벨 제한 없음 · XP 없음 · 지갑 돈만.
    /// 줄은 탭이 켜질 때마다 `rowTemplate`(Name · Value · Buy 자식)을 복제해 다시 만든다.
    /// </summary>
    public sealed class FacilityPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("줄 원본 (꺼진 채). 자식 Name · Value(TMP) · Buy(Button + Label)")] RectTransform rowTemplate;
        [SerializeField, Min(10f)] float rowSpacing = 100f;
        [SerializeField, Tooltip("보관 모듈이 없을 때 안내")] TMP_Text emptyLabel;

        readonly List<RectTransform> rows = new();
        readonly List<SiteModule> modules = new();

        void Start() => GameManager.Instance.Wallet.Cash.Subscribe(_ => Refresh()).AddTo(this);

        void OnEnable() => Rebuild();

        void Rebuild()
        {
            foreach (var row in rows) Destroy(row.gameObject);
            rows.Clear();
            modules.Clear();
            rowTemplate.gameObject.SetActive(false);
            if (Warehouse.Instance != null) modules.AddRange(Warehouse.Instance.ShelfModules());
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(modules.Count == 0);

            for (var i = 0; i < modules.Count; i++)
            {
                var row = Instantiate(rowTemplate, rowTemplate.parent);
                row.gameObject.SetActive(true);
                row.anchoredPosition = rowTemplate.anchoredPosition + Vector2.down * (rowSpacing * i);
                var module = modules[i];
                row.Find("Buy").GetComponent<Button>().onClick.AddListener(() => Buy(module));
                rows.Add(row);
            }
            Refresh();
        }

        void Buy(SiteModule module)
        {
            var warehouse = Warehouse.Instance;
            var costs = GameManager.Instance.Database.Economy.ShelfStageCosts;
            var stage = warehouse.GetShelfStage(module);
            if (stage > costs.Length || !GameManager.Instance.Wallet.TrySpend(costs[stage - 1])) return;
            warehouse.RaiseShelfStage(module);
            var row = rows[modules.IndexOf(module)];
            row.DOKill(true);
            row.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
            Refresh();
        }

        void Refresh()
        {
            var warehouse = Warehouse.Instance;
            if (warehouse == null) return;
            var costs = GameManager.Instance.Database.Economy.ShelfStageCosts;
            var cash = GameManager.Instance.Wallet.Cash.CurrentValue;
            for (var i = 0; i < rows.Count; i++)
            {
                var module = modules[i];
                var shelf = warehouse.FirstShelfOf(module);
                if (shelf == null) continue;
                var stage = warehouse.GetShelfStage(module);
                var now = shelf.CapacityAt(stage);
                var row = rows[i];
                row.Find("Name").GetComponent<TMP_Text>().text = $"{shelf.Item.DisplayName} {SameColorIndex(i)}";
                var value = row.Find("Value").GetComponent<TMP_Text>();
                var buy = row.Find("Buy").GetComponent<Button>();
                var buyLabel = buy.GetComponentInChildren<TMP_Text>();
                if (stage > costs.Length)
                {
                    value.text = $"{stage}단계  {now}개";
                    buyLabel.text = "최대";
                    buy.interactable = false;
                    continue;
                }
                value.text = $"{stage}단계  {now}개  →  {stage + 1}단계  {shelf.CapacityAt(stage + 1)}개";
                buyLabel.text = $"₩{costs[stage - 1]:N0}";
                buy.interactable = cash >= costs[stage - 1];
            }
        }

        /// <summary>같은 색 모듈이 여럿이면 번호 (A보관 2), 하나면 '보관'.</summary>
        string SameColorIndex(int index)
        {
            var warehouse = Warehouse.Instance;
            var item = warehouse.FirstShelfOf(modules[index]).Item;
            int count = 0, order = 0;
            for (var i = 0; i < modules.Count; i++)
            {
                if (warehouse.FirstShelfOf(modules[i]).Item != item) continue;
                count++;
                if (i <= index) order = count;
            }
            return count > 1 ? $"보관 {order}" : "보관";
        }
    }
}
