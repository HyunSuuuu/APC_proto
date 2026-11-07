using System;
using APC.Data;
using R3;

namespace APC.Core
{
    /// <summary>레벨 · 경험치 (GDD 10.1, 10.2).</summary>
    public sealed class LevelProgress : IDisposable
    {
        readonly LevelTable table;
        readonly ReactiveProperty<int> level = new(1);
        readonly ReactiveProperty<int> xp = new(0);
        readonly Subject<int> leveledUp = new();

        public LevelProgress(LevelTable table)
        {
            this.table = table ? table : throw new ArgumentNullException(nameof(table));
        }

        public ReadOnlyReactiveProperty<int> Level => level;

        /// <summary>현재 레벨 안에서 모은 XP (레벨업하면 남은 만큼만 이월).</summary>
        public ReadOnlyReactiveProperty<int> Xp => xp;

        /// <summary>새 레벨 값을 보낸다. 여러 레벨을 한 번에 올리면 레벨마다 한 번씩 보낸다.</summary>
        public Observable<int> LeveledUp => leveledUp;

        public int XpToNext => table.GetXpToNext(level.Value);
        public int MaxStaff => table.GetMaxStaff(level.Value);

        public void AddXp(int amount)
        {
            if (amount <= 0) return;

            var from = level.Value;
            var to = from;
            var remaining = xp.Value + amount;
            while (remaining >= table.GetXpToNext(to))
            {
                remaining -= table.GetXpToNext(to);
                to++;
            }

            // 값을 먼저 확정한 뒤 이벤트를 보낸다 (구독자가 최신 값을 읽도록).
            xp.Value = remaining;
            for (var l = from + 1; l <= to; l++)
            {
                level.Value = l;
                leveledUp.OnNext(l);
            }
        }

        /// <summary>디버그용: 다음 레벨까지 남은 XP를 채운다.</summary>
        public void LevelUpNow() => AddXp(XpToNext - xp.Value);

        public void Dispose()
        {
            level.Dispose();
            xp.Dispose();
            leveledUp.Dispose();
        }
    }
}
