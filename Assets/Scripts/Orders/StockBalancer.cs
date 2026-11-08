using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Logistics;
using UnityEngine;

namespace APC.Orders
{
    /// <summary>
    /// 교착 방지 가중치 (GDD 7.8). 특정 품목만 쌓이고 주문은 다른 품목만 요구해 흐름이 멈추는 것을 막는다.
    /// - 입고: (대기 주문 수요 − 재고)가 큰 품목일수록 많이 싣는다. 품목마다 최소 비율(OrderRules.inboundMinWeight)을 보장한다.
    /// - 주문: 재고(선반 + 공용 보관소)가 많은 품목일수록 주문에 잘 나온다.
    /// - 회사: 입고 트럭 회사는 그 회사 품목 부족량의 합으로, 주문 회사는 재고의 합으로 고른다.
    /// </summary>
    public static class StockBalancer
    {
        static readonly List<float> Weights = new();

        // ── 입고 ────────────────────────────────────────────────────────

        public static CompanyDefinition PickInboundCompany(IReadOnlyList<CompanyDefinition> companies)
        {
            Weights.Clear();
            foreach (var company in companies)
            {
                var shortage = 0f;
                foreach (var item in company.Items) shortage += Shortage(item);
                Weights.Add(shortage + 1f); // 부족한 품목이 없어도 고를 수 있게
            }
            return companies[PickIndex(Weights)];
        }

        /// <summary>입고 트럭 1대에 실을 품목 목록.</summary>
        public static List<ItemDefinition> ComposeInboundLoad(CompanyDefinition company)
        {
            var items = company.Items;
            var minWeight = GameManager.Instance.Database.Orders.InboundMinWeight;

            Weights.Clear();
            var total = 0f;
            foreach (var item in items)
            {
                var shortage = Shortage(item);
                Weights.Add(shortage);
                total += shortage;
            }

            // 비율로 바꾼 뒤 품목마다 최소 비율을 보장한다. 부족한 품목이 없으면 고르게.
            for (var i = 0; i < Weights.Count; i++)
                Weights[i] = Mathf.Max(total > 0f ? Weights[i] / total : 1f / items.Count, minWeight);

            var load = new List<ItemDefinition>(company.InboundTruckCapacity);
            for (var i = 0; i < company.InboundTruckCapacity; i++) load.Add(items[PickIndex(Weights)]);
            return load;
        }

        // ── 주문 ────────────────────────────────────────────────────────

        public static CompanyDefinition PickOrderCompany(IReadOnlyList<CompanyDefinition> companies)
        {
            Weights.Clear();
            foreach (var company in companies)
            {
                var stock = 0f;
                foreach (var item in company.Items) stock += Stock(item) + 1f;
                Weights.Add(stock);
            }
            return companies[PickIndex(Weights)];
        }

        public static ItemDefinition PickOrderItem(IReadOnlyList<ItemDefinition> items)
        {
            Weights.Clear();
            foreach (var item in items) Weights.Add(Stock(item) + 1f); // 재고 0이어도 나올 수 있게 +1
            return items[PickIndex(Weights)];
        }

        // ── 공통 ────────────────────────────────────────────────────────

        /// <summary>대기 주문이 더 필요로 하는 수 (0 이상).</summary>
        static float Shortage(ItemDefinition item)
        {
            var demand = OrderService.Instance != null ? OrderService.Instance.GetQueuedDemand(item) : 0;
            return Mathf.Max(0, demand - Stock(item));
        }

        static int Stock(ItemDefinition item) => Warehouse.Instance != null ? Warehouse.Instance.GetStock(item) : 0;

        static int PickIndex(List<float> weights)
        {
            var total = 0f;
            foreach (var w in weights) total += w;
            var roll = Random.value * total;
            for (var i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0f) return i;
            }
            return weights.Count - 1;
        }
    }
}
