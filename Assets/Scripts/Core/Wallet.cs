using System;
using R3;
using UnityEngine;

namespace APC.Core
{
    /// <summary>
    /// 돈 (GDD 9.1, 9.5). 두 칸으로 나뉜다.
    /// - <see cref="Cash"/> = **보유 재화**: 실제로 쓰는 돈 (구매 패드 · 고용).
    /// - <see cref="Piggy"/> = **저금통**: 이번 파트에 주운 돈. 쓸 수 없고, 파트 종료 정산 때 보유 재화로 옮겨진다.
    /// </summary>
    public sealed class Wallet : IDisposable
    {
        readonly ReactiveProperty<int> cash;
        readonly ReactiveProperty<int> piggy = new(0);

        public Wallet(int startingCash)
        {
            cash = new ReactiveProperty<int>(Mathf.Max(0, startingCash));
        }

        /// <summary>보유 재화 (쓸 수 있는 돈).</summary>
        public ReadOnlyReactiveProperty<int> Cash => cash;

        /// <summary>저금통 (이번 파트에 주운 돈, 쓸 수 없음).</summary>
        public ReadOnlyReactiveProperty<int> Piggy => piggy;

        /// <summary>보유 재화에 바로 더한다 (정산 · 디버그).</summary>
        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "음수는 Spend로 처리하세요.");
            cash.Value += amount;
        }

        /// <summary>true면 주운 돈이 저금통 대신 보유 재화로 바로 간다 (사무실 · 저금통을 짓기 전, GDD 9.6).</summary>
        public bool DirectToCash { get; set; }

        /// <summary>주운 현금을 저금통에 넣는다 (현금 존). DirectToCash면 보유 재화로.</summary>
        public void AddToPiggy(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (DirectToCash) cash.Value += amount;
            else piggy.Value += amount;
        }

        /// <summary>
        /// 파트 종료 정산: 저금통을 비우고 `저금통 + 조정액`(보너스 − 시급)을 보유 재화에 반영한다. 보유 재화는 0 밑으로 내려가지 않는다.
        /// 실제로 늘어난 금액을 돌려준다 (음수 가능).
        /// </summary>
        public int SettlePiggy(int adjustment)
        {
            var before = cash.Value;
            var total = piggy.Value + adjustment;
            piggy.Value = 0;
            cash.Value = Mathf.Max(0, before + total);
            return cash.Value - before;
        }

        /// <summary>전액을 낼 수 있을 때만 보유 재화에서 차감한다 (고용금, 시급).</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (cash.Value < amount) return false;
            cash.Value -= amount;
            return true;
        }

        /// <summary>가진 만큼만 보유 재화에서 차감하고 실제로 낸 금액을 돌려준다 (구매 패드의 부분 투입, GDD 9.2).</summary>
        public int SpendUpTo(int amount)
        {
            var paid = Mathf.Clamp(amount, 0, cash.Value);
            cash.Value -= paid;
            return paid;
        }

        public void Dispose()
        {
            cash.Dispose();
            piggy.Dispose();
        }
    }
}
