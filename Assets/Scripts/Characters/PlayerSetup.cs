using APC.Core;
using APC.Interaction;
using APC.Logistics;
using UnityEngine;

namespace APC.Characters
{
    /// <summary>플레이어의 운반 · 상호작용 수치를 PlayerConfig에서 적용한다 (GDD 5.1). 업그레이드(2순위)도 여기서 갱신한다.</summary>
    [RequireComponent(typeof(CarryStack), typeof(Interactor))]
    public sealed class PlayerSetup : MonoBehaviour
    {
        void Start()
        {
            var config = GameManager.Instance.Database.Player;
            GetComponent<CarryStack>().SetCapacity(config.MaxCarry);
            var interactor = GetComponent<Interactor>();
            interactor.Configure(config.PickInterval, config.InteractionDelay);
            interactor.ConfigureRules(taskLock: true, config.TransferSwitchCooldown); // GDD 5.3 — 플레이어만
        }
    }
}
