using System;
using R3;
using UnityEngine;

namespace APC.Core
{
    /// <summary>현금 (GDD 9.1). 재화는 현금 1종뿐이다.</summary>
    public sealed class Wallet : IDisposable
    {
        readonly ReactiveProperty<int> cash;

        public Wallet(int startingCash)
        {
            cash = new ReactiveProperty<int>(Mathf.Max(0, startingCash));
        }

        public ReadOnlyReactiveProperty<int> Cash => cash;

        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "음수는 Spend로 처리하세요.");
            cash.Value += amount;
        }

        /// <summary>전액을 낼 수 있을 때만 차감한다 (고용금, 시급, 새로고침).</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (cash.Value < amount) return false;
            cash.Value -= amount;
            return true;
        }

        /// <summary>가진 만큼만 차감하고 실제로 낸 금액을 돌려준다 (구매 패드의 부분 투입, GDD 9.2).</summary>
        public int SpendUpTo(int amount)
        {
            var paid = Mathf.Clamp(amount, 0, cash.Value);
            cash.Value -= paid;
            return paid;
        }

        public void Dispose() => cash.Dispose();
    }
}
