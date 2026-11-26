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
    /// 주문 = 색(회사)별 필요 개수, 합계 3~6 · 색 최대 2. **선반에 주문 수량만큼 있을 때만** 주문(= 트럭)이 생긴다 (2026-11-19).
    /// 다 실으면 Complete로 보상을 받는다. 씬에 하나 둔다 (Site/Logistics).
    /// </summary>
    public sealed class OrderService : MonoBehaviour
    {
        public static OrderService Instance { get; private set; }

        readonly List<Order> orders = new(); // 상차 칸에 붙은 주문
        readonly Dictionary<ItemDefinition, int> reserved = new();
        readonly Subject<Order> removed = new();
        readonly List<CompanyDefinition> candidates = new();
        readonly Dictionary<CompanyDefinition, int> free = new(); // 재사용: 빈 상차 칸마다 매 프레임 부른다 (새로 만들면 8칸 × 프레임당 8.6KB GC, 2026-11-25)

        int nextNumber = 1;
        int pendingQuantity; // 다음 주문 수량 (선반이 찰 때까지 그대로, 0 = 아직 안 뽑음)
        bool firstOrderMade; // 첫 상차 트럭은 선반에 있는 만큼만 (튜토리얼)

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

        /// <summary>
        /// 지금 새 상차 트럭(주문)이 생길 수 있는가 = 선반 여유 합 ≥ 다음 주문 수량 (`CreateForTruck`과 같은 기준, 만들지는 않음 — 남은 시간 보너스 판단, M17-3).
        /// </summary>
        public bool CanCreateForTruck()
        {
            var contracts = ContractService.Instance;
            if (contracts == null || !contracts.HasAnyContract || Warehouse.Instance == null) return false;
            if (pendingQuantity <= 0) pendingQuantity = GameManager.Instance.Database.Orders.RollQuantity();
            var sum = 0;
            var contracted = contracts.Contracted;
            for (var i = 0; i < contracted.Count; i++) sum += Mathf.Max(0, ShelfFree(contracted[i].Items[0]));
            return firstOrderMade ? sum >= pendingQuantity : sum > 0;
        }

        /// <summary>
        /// 빈 상차 칸이 들어올 트럭의 주문 1건을 만든다 (GDD 8.1 · 8.2). **선반에 주문 수량만큼 있을 때만** 만든다 (2026-11-19) —
        /// 없으면 null(트럭이 아직 안 옴, OutboundBay가 매 프레임 다시 부름). 수량은 한 번 뽑아 두고 주문이 생길 때까지 그대로 (작은 수가 유리해지지 않게).
        /// 색 = 선반 여유(선반 수 − 다른 주문에 잡힌 수)가 있는 색 중 가중치로, 최대 2색.
        /// </summary>
        public Order CreateForTruck()
        {
            var contracts = ContractService.Instance;
            if (contracts == null || !contracts.HasAnyContract || Warehouse.Instance == null) return null;

            var rules = GameManager.Instance.Database.Orders;
            if (pendingQuantity <= 0) pendingQuantity = rules.RollQuantity();
            var total = pendingQuantity;

            candidates.Clear();
            free.Clear();
            var sum = 0;
            var contracted = contracts.Contracted;
            for (var i = 0; i < contracted.Count; i++) // foreach는 IReadOnlyList 열거자 박싱 (매 프레임 호출)
            {
                var company = contracted[i];
                var n = ShelfFree(company.Items[0]);
                if (n <= 0) continue;
                candidates.Add(company);
                free[company] = n;
                sum += n;
            }
            // 첫 상차 트럭(튜토리얼 7단계)은 선반에 있는 만큼만 — 6단계가 1개만 올려도 끝나서 트럭이 안 오던 문제 (2026-11-19)
            if (!firstOrderMade && sum > 0) total = Economy.PartService.TutorialPhase ? sum : Mathf.Min(total, sum); // 튜토리얼은 선반에 올린 상자 전부 (2026-11-20)
            if (sum < total) return null; // 선반에 아직 모자람
            if (candidates.Count == 1 && free[candidates[0]] < total) return null; // 아래 '한 색으로 모자람'과 같음 — 매 프레임 List · 뽑기 할당 전에 (2026-11-25)

            var lines = new List<OrderLine>();
            var first = StockBalancer.PickByWeight(candidates);
            candidates.Remove(first);
            var twoColors = candidates.Count > 0 && total >= 2 && (free[first] < total || Random.value < rules.TwoColorChance);
            if (!twoColors)
            {
                if (free[first] < total) return null; // 한 색으로 모자라고 두 번째 색도 없음
                lines.Add(new OrderLine(first, total));
            }
            else
            {
                // 두 색 다 1개 이상, 각 색 선반 여유 안에서
                OrderLine split = null;
                for (var tries = 0; tries < 4 && split == null && candidates.Count > 0; tries++)
                {
                    var second = StockBalancer.PickByWeight(candidates);
                    var min = Mathf.Max(1, total - free[second]);
                    var max = Mathf.Min(free[first], total - 1);
                    if (min <= max)
                    {
                        var firstCount = Random.Range(min, max + 1);
                        lines.Add(new OrderLine(first, firstCount));
                        split = new OrderLine(second, total - firstCount);
                    }
                    else candidates.Remove(second);
                }
                if (split == null)
                {
                    if (free[first] < total) return null;
                    lines.Add(new OrderLine(first, total));
                }
                else lines.Add(split);
            }
            pendingQuantity = 0;
            firstOrderMade = true;

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
        /// 새 주문에 쓸 수 있는 선반 여유 = 선반(랙 포함)에 있는 수 + 이미 꺼내 상차하러 가는 수 − 출발 안 한 주문들의 남은 수량.
        /// (옛 규칙 '하차 칸 · 트럭 어딘가에 있으면 요구 · 없으면 갈색'은 2026-11-19 삭제 — 선반에 충분할 때만 트럭이 온다.)
        /// </summary>
        int ShelfFree(ItemDefinition item)
        {
            var committed = 0;
            foreach (var order in orders) committed += order.GetRemaining(item);
            return Warehouse.Instance.GetShelfCount(item) + Warehouse.CountCarried(item, CargoOrigin.Shelf) - committed;
        }
    }
}
