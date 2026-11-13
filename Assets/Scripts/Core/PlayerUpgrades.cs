using System;
using APC.Data;
using R3;

namespace APC.Core
{
    /// <summary>업그레이드 종류 (GDD 5.1 · 14.2).</summary>
    public enum PlayerUpgradeKind
    {
        /// <summary>적재 도구: 손 → 구루마 → 롤테이너 (지게차는 나중).</summary>
        Carry,
        MoveSpeed,
    }

    /// <summary>
    /// 플레이어 업그레이드 단계 (사무실 업그레이드 책상, GDD 5.1 · 9.4). 단계 0 = 기본값(PlayerConfig).
    /// 다음 단계는 레벨이 되고 **보유 재화**가 충분할 때만 산다 (저금통 돈은 못 씀). 사면 XP (`XpPurchase`).
    /// </summary>
    public sealed class PlayerUpgrades : IDisposable
    {
        readonly PlayerConfig config;
        readonly Wallet wallet;
        readonly LevelProgress level;
        readonly int xpPurchase;
        readonly ReactiveProperty<int> carryStep = new(0);
        readonly ReactiveProperty<int> speedStep = new(0);

        public PlayerUpgrades(PlayerConfig config, Wallet wallet, LevelProgress level, int xpPurchase)
        {
            this.config = config;
            this.wallet = wallet;
            this.level = level;
            this.xpPurchase = xpPurchase;
        }

        public ReadOnlyReactiveProperty<int> CarryStep => carryStep;
        public ReadOnlyReactiveProperty<int> SpeedStep => speedStep;

        /// <summary>지금 최대 적재 수.</summary>
        public int Capacity => carryStep.Value == 0 ? config.MaxCarry : (int)config.CarrySteps[carryStep.Value - 1].value;

        /// <summary>지금 이동 속도 (m/s).</summary>
        public float MoveSpeed => speedStep.Value == 0 ? config.MoveSpeed : config.SpeedSteps[speedStep.Value - 1].value;

        /// <summary>그 종류의 이름 · 값 (단계 0 = 기본). 팝업 표시용.</summary>
        public string CurrentName(PlayerUpgradeKind kind)
        {
            var step = Step(kind).Value;
            if (step > 0) return Steps(kind)[step - 1].name;
            return kind == PlayerUpgradeKind.Carry ? "손으로 들기" : "기본 속도";
        }

        /// <summary>다음 단계. 없으면(최대) false.</summary>
        public bool TryGetNext(PlayerUpgradeKind kind, out PlayerUpgradeStep next)
        {
            var steps = Steps(kind);
            var step = Step(kind).Value;
            next = step < steps.Length ? steps[step] : default;
            return step < steps.Length;
        }

        /// <summary>다음 단계가 있고, 레벨이 됐고, 보유 재화가 충분한가.</summary>
        public bool CanBuy(PlayerUpgradeKind kind)
            => TryGetNext(kind, out var next) && level.Level.CurrentValue >= next.unlockLevel && wallet.Cash.CurrentValue >= next.cost;

        public bool TryBuy(PlayerUpgradeKind kind)
        {
            if (!CanBuy(kind)) return false;
            TryGetNext(kind, out var next);
            if (!wallet.TrySpend(next.cost)) return false;
            Step(kind).Value++;
            level.AddXp(xpPurchase);
            return true;
        }

        PlayerUpgradeStep[] Steps(PlayerUpgradeKind kind) => kind == PlayerUpgradeKind.Carry ? config.CarrySteps : config.SpeedSteps;

        ReactiveProperty<int> Step(PlayerUpgradeKind kind) => kind == PlayerUpgradeKind.Carry ? carryStep : speedStep;

        public void Dispose()
        {
            carryStep.Dispose();
            speedStep.Dispose();
        }
    }
}
