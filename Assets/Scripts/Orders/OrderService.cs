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
    /// 주문 큐 (GDD 8.1~8.4). 게임 시작 때 첫 주문이 바로 생기고, 이후 레벨 구간의 간격마다 회사 가중치로 1건씩 생긴다 (큐 최대 8건).
    /// 출고 베이가 채울 수 있는 가장 오래된 대기 주문을 가져가고(AssignOldestWaiting), 다 실으면 Complete로 보상을 받는다.
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

        /// <summary>상차장에 배정할 수 있는 대기 주문이 있는가 (채울 수 없는 주문은 건너뜀, GDD 8.3).</summary>
        public bool HasWaiting => FindOldestFillable() != null;

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
            // 게임 시작 때 첫 회사(A사) 주문 1건만 바로 (GDD 8.2). 이후 새 회사도 간격대로.
            ContractService.Instance.OnContracted
                .Take(1)
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

            // 회사 = 품목 1종 (GDD 6.2)
            var lines = new List<OrderLine> { new(company.Items[0], gm.Database.Orders.RollQuantity()) };
            var order = new Order(nextNumber++, company, lines);
            orders.Add(order);
            added.OnNext(order);
            return order;
        }

        /// <summary>출고 베이용: 채울 수 있는 가장 오래된 대기 주문을 배정한다 (GDD 8.3). 없으면 null.</summary>
        public Order AssignOldestWaiting()
        {
            var order = FindOldestFillable();
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

            order.SetState(OrderState.Departed);
            removed.OnNext(order); // 출고 XP 없음 (GDD 10.1, 2026-11-14)
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

        Order FindOldestFillable()
        {
            foreach (var order in orders)
            {
                if (order.State.CurrentValue == OrderState.Waiting && CanFill(order)) return order;
            }
            return null;
        }

        // shortcut: 대형(D사) 주문은 지게차가 없어 못 채운다 — M11-8에서 지게차 보유 검사로 바꾼다.
        static bool CanFill(Order order) => !order.Company.IsLarge;
    }
}
