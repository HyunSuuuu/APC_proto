using System.Collections.Generic;
using APC.Contracts;
using APC.Core;
using APC.Data;
using APC.Logistics;
using R3;
using UnityEngine;

namespace APC.Orders
{
    /// <summary>
    /// 상차 트럭 주문 (GDD 8.1~8.4, 변경사항 2). 주문서 큐 · 생성 간격은 없다: **상차 칸이 비면** 그 칸이 `CreateForTruck`으로 새 주문을 바로 만든다.
    /// 주문 = 색(회사)별 필요 개수, 합계 3~6 · 색 최대 2. 구역이나 상자가 없는 색은 요구하지 않고, 하나도 없으면 갈색(첫 회사).
    /// 다 실으면 Complete로 보상을 받는다. 씬에 하나 둔다 (Site/Logistics).
    /// </summary>
    public sealed class OrderService : MonoBehaviour
    {
        public static OrderService Instance { get; private set; }

        readonly List<Order> orders = new(); // 상차 칸에 붙은 주문
        readonly Dictionary<ItemDefinition, int> reserved = new();
        readonly Subject<Order> removed = new();
        readonly List<CompanyDefinition> candidates = new();

        int nextNumber = 1;

        /// <summary>출발하지 않은 주문 (상차 칸마다 1건).</summary>
        public IReadOnlyList<Order> Orders => orders;

        /// <summary>출발해서 빠진 주문.</summary>
        public Observable<Order> OnRemoved => removed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[OrderService] 씬에 OrderService가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            removed.Dispose();
        }

        /// <summary>빈 상차 칸이 들어올 트럭의 주문 1건을 만든다 (GDD 8.1 · 8.2). 첫 회사가 아직 없으면 null.</summary>
        public Order CreateForTruck()
        {
            var contracts = ContractService.Instance;
            if (contracts == null || !contracts.HasAnyContract) return null;

            var rules = GameManager.Instance.Database.Orders;
            var total = rules.RollQuantity();

            candidates.Clear();
            foreach (var company in contracts.Contracted)
            {
                if (CanRequest(company)) candidates.Add(company);
            }

            var lines = new List<OrderLine>();
            if (candidates.Count == 0)
            {
                lines.Add(new OrderLine(contracts.Contracted[0], total)); // 요구할 색이 없으면 갈색 (2026-11-16)
            }
            else
            {
                var first = StockBalancer.PickByWeight(candidates);
                candidates.Remove(first);
                if (candidates.Count > 0 && total >= 2 && Random.value < rules.TwoColorChance)
                {
                    var firstCount = Random.Range(1, total); // 1 ~ total−1, 두 색 다 1개 이상
                    lines.Add(new OrderLine(first, firstCount));
                    lines.Add(new OrderLine(StockBalancer.PickByWeight(candidates), total - firstCount));
                }
                else
                {
                    lines.Add(new OrderLine(first, total));
                }
            }

            var order = new Order(nextNumber++, lines);
            order.SetState(OrderState.Arriving);
            orders.Add(order);
            return order;
        }

        public void MarkDocked(Order order) => order.SetState(OrderState.Docked);

        /// <summary>트럭이 떠나는 순간 주문을 끝내고 돈(실린 상자 수 × 색 단가)을 돌려준다 (돈 더미는 베이가 쌓음). 출고 XP 없음.</summary>
        public int Complete(Order order)
        {
            if (!orders.Remove(order)) return 0;

            order.SetState(OrderState.Departed);
            removed.OnNext(order);
            return order.GetReward();
        }

        /// <summary>
        /// 현재 수요 (GDD 8.4) = 정차 중인 트럭 주문의 남은 수량 − 이미 선반에서 꺼내 들고 있는 수 − 예약된 수.
        /// 선반 수거 존은 이만큼만 집게 한다.
        /// </summary>
        public int GetDemand(ItemDefinition item)
        {
            var need = 0;
            foreach (var order in orders)
            {
                if (order.State.CurrentValue == OrderState.Docked) need += order.GetRemaining(item);
            }
            reserved.TryGetValue(item, out var booked);
            return need - Warehouse.CountCarried(item, CargoOrigin.Shelf) - booked;
        }

        /// <summary>알바가 작업을 고를 때 수요를 예약한다 (M6). 같은 수요를 여러 알바가 중복으로 가져가지 않게.</summary>
        public void Reserve(ItemDefinition item, int count)
        {
            reserved.TryGetValue(item, out var current);
            reserved[item] = current + count;
        }

        public void Unreserve(ItemDefinition item, int count)
        {
            reserved.TryGetValue(item, out var current);
            reserved[item] = Mathf.Max(0, current - count);
        }

        /// <summary>
        /// 요구할 수 있는 색 (GDD 8.1): 그 색 선반(보관 구역)이 있고, 선반 · 하차 칸 · 하차 트럭 · 손 어딘가에 그 색 상자가 있다.
        /// 대형(D 가구)도 D보관이 있으면 요구한다 (지게차 알바가 없으면 그 트럭은 기다림, 2026-11-18).
        /// </summary>
        static bool CanRequest(CompanyDefinition company)
        {
            var item = company.Items[0];
            var warehouse = Warehouse.Instance;
            if (warehouse == null || !warehouse.HasShelf(item)) return false;
            if (warehouse.GetStock(item) > 0 || Warehouse.CountCarried(item, CargoOrigin.Truck) > 0 || Warehouse.CountCarried(item, CargoOrigin.Storage) > 0) return true;
            foreach (var truck in FindObjectsByType<InboundTruck>(FindObjectsSortMode.None))
            {
                if (truck.CountOf(item) > 0) return true;
            }
            return false;
        }
    }
}
