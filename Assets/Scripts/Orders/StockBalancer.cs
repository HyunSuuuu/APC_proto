using System.Collections.Generic;
using APC.Data;
using UnityEngine;

namespace APC.Orders
{
    /// <summary>
    /// 입고 트럭 · 주문 회사 고르기. 둘 다 회사 가중치(GDD 6.2) 비율로 무작위.
    /// 교착 방지 가중치(재고 · 부족량 비례)는 2026-11-13 기획 변경으로 삭제 (회사 = 품목 1종).
    /// </summary>
    public static class StockBalancer
    {
        /// <summary>입고 트럭 회사 = 회사 가중치 비율로 무작위 (GDD 6.3).</summary>
        public static CompanyDefinition PickInboundCompany(IReadOnlyList<CompanyDefinition> companies) => PickByWeight(companies);

        /// <summary>주문 회사 = 회사 가중치 비율로 무작위 (GDD 8.1).</summary>
        public static CompanyDefinition PickOrderCompany(IReadOnlyList<CompanyDefinition> companies) => PickByWeight(companies);

        /// <summary>입고 트럭 1대에 실을 품목 목록 = 그 회사 품목으로 가득.</summary>
        public static List<ItemDefinition> ComposeInboundLoad(CompanyDefinition company)
        {
            var load = new List<ItemDefinition>(company.InboundTruckCapacity);
            for (var i = 0; i < company.InboundTruckCapacity; i++) load.Add(company.Items[0]);
            return load;
        }

        static CompanyDefinition PickByWeight(IReadOnlyList<CompanyDefinition> companies)
        {
            var total = 0f;
            foreach (var company in companies) total += company.Weight;

            var roll = Random.value * total;
            foreach (var company in companies)
            {
                roll -= company.Weight;
                if (roll < 0f) return company;
            }
            return companies[^1];
        }
    }
}
