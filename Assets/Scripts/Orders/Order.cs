using System.Collections.Generic;
using APC.Data;
using R3;
using UnityEngine;

namespace APC.Orders
{
    /// <summary>주문 진행 상태 (GDD 12.2 상태 배지).</summary>
    public enum OrderState
    {
        /// <summary>큐에서 트럭 배정을 기다림 — 배지 "대기".</summary>
        Waiting,

        /// <summary>트럭이 배정되어 상차 칸으로 후진 중 — 배지 "도착".</summary>
        Arriving,

        /// <summary>트럭이 정차해 상차를 받는 중 — 배지 "도착". 수요(GDD 8.4)는 이 상태의 주문만 센다.</summary>
        Docked,

        /// <summary>다 싣고 출발함. 큐에서 빠진다 — 배지 "출발".</summary>
        Departed,
    }

    /// <summary>주문의 품목 1줄 (품목 × 수량).</summary>
    public sealed class OrderLine
    {
        public OrderLine(ItemDefinition item, int quantity)
        {
            Item = item;
            Quantity = Mathf.Max(1, quantity);
        }

        public ItemDefinition Item { get; }
        public int Quantity { get; }
        public int Loaded { get; internal set; }
        public int Remaining => Quantity - Loaded;
    }

    /// <summary>
    /// 주문 1건 (GDD 8.1). 한 회사의 품목으로만 구성한다. 출고 트럭 1대가 주문 1건을 맡는다.
    /// 만들고 상태를 바꾸는 것은 OrderService가 한다.
    /// </summary>
    public sealed class Order
    {
        readonly List<OrderLine> lines;
        readonly ReactiveProperty<OrderState> state = new(OrderState.Waiting);
        readonly Subject<Unit> changed = new();

        internal Order(int number, CompanyDefinition company, List<OrderLine> lines)
        {
            Number = number;
            Company = company;
            this.lines = lines;
        }

        /// <summary>화면 표시용 번호 (#1부터).</summary>
        public int Number { get; }

        public CompanyDefinition Company { get; }
        public IReadOnlyList<OrderLine> Lines => lines;
        public bool IsCompound => lines.Count > 1;
        public ReadOnlyReactiveProperty<OrderState> State => state;

        /// <summary>상차될 때마다 알린다 (카드 · 트럭 표시 갱신).</summary>
        public Observable<Unit> Changed => changed;

        public bool IsFulfilled
        {
            get
            {
                foreach (var line in lines)
                {
                    if (line.Remaining > 0) return false;
                }
                return true;
            }
        }

        public int GetRemaining(ItemDefinition item)
        {
            var remaining = 0;
            foreach (var line in lines)
            {
                if (line.Item == item) remaining += line.Remaining;
            }
            return remaining;
        }

        /// <summary>그 품목 1개를 실었다고 기록한다. 더 필요 없으면 false.</summary>
        public bool TryLoad(ItemDefinition item)
        {
            foreach (var line in lines)
            {
                if (line.Item != item || line.Remaining <= 0) continue;
                line.Loaded++;
                changed.OnNext(Unit.Default);
                return true;
            }
            return false;
        }

        /// <summary>보상 금액 (GDD 8.5): Σ(단가 × 수량), 복합 주문은 × 배율.</summary>
        public int GetReward(EconomyConfig economy)
        {
            var sum = 0;
            foreach (var line in lines) sum += line.Item.UnitPrice * line.Quantity;
            return IsCompound ? Mathf.RoundToInt(sum * economy.CompoundOrderMultiplier) : sum;
        }

        internal void SetState(OrderState value) => state.Value = value;
    }
}
