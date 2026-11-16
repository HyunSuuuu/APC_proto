using System.Collections.Generic;
using APC.Data;
using R3;
using UnityEngine;

namespace APC.Orders
{
    /// <summary>주문 진행 상태.</summary>
    public enum OrderState
    {
        /// <summary>트럭이 상차 칸으로 후진 중.</summary>
        Arriving,

        /// <summary>트럭이 정차해 상차를 받는 중. 수요(GDD 8.4)는 이 상태의 주문만 센다.</summary>
        Docked,

        /// <summary>다 싣고 떠남.</summary>
        Departed,
    }

    /// <summary>주문의 색 1줄 (색(회사) × 수량).</summary>
    public sealed class OrderLine
    {
        public OrderLine(CompanyDefinition company, int quantity)
        {
            Company = company;
            Item = company.Items[0]; // 회사 = 품목 1종
            Quantity = Mathf.Max(1, quantity);
        }

        public CompanyDefinition Company { get; }
        public ItemDefinition Item { get; }
        public int Quantity { get; }
        public int Loaded { get; internal set; }
        public int Remaining => Quantity - Loaded;
    }

    /// <summary>
    /// 주문 1건 = 상차 트럭 1대 (GDD 8.1). 색별 필요 개수 (색 최대 2).
    /// 만들고 상태를 바꾸는 것은 OrderService가 한다.
    /// </summary>
    public sealed class Order
    {
        readonly List<OrderLine> lines;
        readonly ReactiveProperty<OrderState> state = new(OrderState.Arriving);
        readonly Subject<Unit> changed = new();

        internal Order(int number, List<OrderLine> lines)
        {
            Number = number;
            this.lines = lines;
        }

        /// <summary>번호 (#1부터, 로그용).</summary>
        public int Number { get; }

        public IReadOnlyList<OrderLine> Lines => lines;
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

/// <summary>실린 상자 수.</summary>
        public int LoadedCount
        {
            get
            {
                var n = 0;
                foreach (var line in lines) n += line.Loaded;
                return n;
            }
        }

        /// <summary>돈 (GDD 8.5 `임시`): 줄마다 실린 상자 수 × 그 색 단가의 합.</summary>
        public int GetReward()
        {
            var reward = 0;
            foreach (var line in lines) reward += line.Loaded * line.Company.ContractUnitPrice;
            return reward;
        }

        internal void SetState(OrderState value) => state.Value = value;
    }
}
