using APC.Core;
using APC.Interaction;
using APC.Logistics;
using R3;
using UnityEngine;

namespace APC.Characters
{
    /// <summary>플레이어의 운반 · 상호작용 수치를 PlayerConfig에서 적용한다 (GDD 5.1). 적재 수는 업그레이드 단계(`PlayerUpgrades`)를 따른다.</summary>
    [RequireComponent(typeof(CarryStack), typeof(Interactor))]
    public sealed class PlayerSetup : MonoBehaviour
    {
        void Start()
        {
            var config = GameManager.Instance.Database.Player;
            var stack = GetComponent<CarryStack>();
            var upgrades = GameManager.Instance.Upgrades;
            var animator = GetComponent<CharacterAnimator>();
            upgrades.CarryStep.Subscribe(step =>
            {
                stack.SetCapacity(upgrades.Capacity); // 업그레이드 탭 (GDD 5.1)
                if (animator != null) animator.UsePull = step > 0; // 구루마 · 롤테이너는 끌고 다닌다
            }).AddTo(this);
            var interactor = GetComponent<Interactor>();
            interactor.Configure(config.PickInterval, config.InteractionDelay);
            interactor.ConfigureRules(taskLock: true, config.TransferSwitchCooldown); // GDD 5.3 — 플레이어만
        }
    }
}
