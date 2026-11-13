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
    /// 주문 큐 (GDD 8.2~8.4). 계약하면 첫 주문이 바로 생기고, 이후 레벨 구간의 간격마다 1건씩 생긴다 (큐 최대 8건).
    /// 출고 베이가 가장 오래된 대기 주문을 가져가고(AssignOldestWaiting), 다 실으면 Complete로 보상을 받는다.
    /// 씬에 하나 둔다 (Site/Logistics).
    /// </summary>
    public sealed class OrderService : MonoBehaviour
    {
        public static OrderService Instance { get; private set; }

        readonly List<Order> orders = new(); // 오래된 순
        readonly Dictionary<ItemDefinition, int> reserved = new();
        readonly Subject<Order> added = new();
        readonly Subject<Order> removed = new();

        float spawnTimer;
        int nextNumber = 1;

        /// <summary>출발하지 않은 주문 (대기 + 배정됨), 오래된 순.</summary>
        public IReadOnlyList<Order> Orders => orders;

        public Observable<Order> OnAdded => added;

        /// <summary>출발해서 큐에서 빠진 주문.</summary>
        public Observable<Order> OnRemoved => removed;

        public bool HasWaiting => FindOldest(OrderState.Waiting) != null;

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

        void Start()
        {
            // 회사 입고가 시작되는 순간 그 회사 주문 1건 (게임 시작 = A사, GDD 8.2). 이후는 간격대로.
            ContractService.Instance.OnContracted
                .Subscribe(company =>
                {
                    TryGenerate(company);
                    spawnTimer = 0f;
                })
                .AddTo(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            added.Dispose();
            removed.Dispose();
        }

        void Update()
        {
            var contracts = ContractService.Instance;
            if (contracts == null || !contracts.HasAnyContract) return;

            var gm = GameManager.Instance;
            if (orders.Count >= gm.Database.Orders.MaxQueuedOrders) return; // 가득 차면 생성을 멈춘다 (타이머도 멈춤)

            spawnTimer += Time.deltaTime;
            var bracket = gm.Database.Orders.GetBracket(gm.Level.Level.CurrentValue);
            if (spawnTimer < bracket.spawnInterval) return;

            spawnTimer = 0f;
            TryGenerate(StockBalancer.PickOrderCompany(contracts.Contracted));
        }

        /// <summary>그 회사 주문 1건을 큐에 넣는다. 큐가 가득 찼으면 null.</summary>
        public Order TryGenerate(CompanyDefinition company)
        {
            var gm = GameManager.Instance;
            if (company == null || company.Items.Count == 0 || orders.Count >= gm.Database.Orders.MaxQueuedOrders) return null;

            var bracket = gm.Database.Orders.GetBracket(gm.Level.Level.CurrentValue);
            var order = new Order(nextNumber++, company, ComposeLines(company, bracket));
            orders.Add(order);
            added.OnNext(order);
            return order;
        }

        /// <summary>출고 베이용: 가장 오래된 대기 주문을 배정한다 (GDD 8.3). 없으면 null.</summary>
        public Order AssignOldestWaiting()
        {
            var order = FindOldest(OrderState.Waiting);
            order?.SetState(OrderState.Arriving);
            return order;
        }

        public void MarkDocked(Order order) => order.SetState(OrderState.Docked);

        /// <summary>
        /// 트럭이 **출발하는 순간** 주문을 끝낸다 (GDD 7.6): 경험치를 주고, 출고 현금(실린 상자 수 × 계약 단가)을 돌려준다
        /// (현금 더미는 베이가 쌓음). 출발 대수는 OnDeparted로 알린다 (할당량).
        /// </summary>
        public int Complete(Order order)
        {
            if (!orders.Remove(order)) return 0;

            var economy = GameManager.Instance.Database.Economy;
            order.SetState(OrderState.Departed);
            removed.OnNext(order);
            GameManager.Instance.Level.AddXp(order.IsCompound ? economy.XpCompoundOrder : economy.XpSingleOrder);
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

        /// <summary>큐 전체(대기 포함)의 남은 수량. 입고 구성 가중치(GDD 7.8)가 쓴다.</summary>
        public int GetQueuedDemand(ItemDefinition item)
        {
            var need = 0;
            foreach (var order in orders) need += order.GetRemaining(item);
            return need;
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

        Order FindOldest(OrderState state)
        {
            foreach (var order in orders)
            {
                if (order.State.CurrentValue == state) return order;
            }
            return null;
        }

        static List<OrderLine> ComposeLines(CompanyDefinition company, OrderRules.Bracket bracket)
        {
            var pool = new List<ItemDefinition>(company.Items);
            var lines = new List<OrderLine>();

            var compound = pool.Count >= 2 && Random.value < bracket.compoundChance;
            if (!compound)
            {
                lines.Add(new OrderLine(StockBalancer.PickOrderItem(pool), bracket.singleQuantity.Roll()));
                return lines;
            }

            var kinds = Mathf.Clamp(bracket.compoundKinds.Roll(), 2, pool.Count);
            var total = 0;
            for (var k = 0; k < kinds; k++)
            {
                var item = StockBalancer.PickOrderItem(pool);
                pool.Remove(item);
                var kindsLeft = kinds - k - 1;
                var max = Mathf.Max(1, bracket.compoundMaxTotal - total - kindsLeft); // 남은 품목도 최소 1개씩
                var quantity = Mathf.Clamp(bracket.compoundQuantityPerItem.Roll(), 1, max);
                total += quantity;
                lines.Add(new OrderLine(item, quantity));
            }
            return lines;
        }
    }
}
