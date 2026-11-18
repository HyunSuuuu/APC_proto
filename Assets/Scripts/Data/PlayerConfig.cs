using UnityEngine;

namespace APC.Data
{
    /// <summary>업그레이드 1단계 (GDD 5.1 · 9.4). 이름 · 오른 뒤 값 · 열리는 레벨 · 비용(보유 재화).</summary>
    [System.Serializable]
    public struct PlayerUpgradeStep
    {
        public string name;
        public float value;
        [Min(1)] public int unlockLevel;
        [Min(0)] public int cost;

        public PlayerUpgradeStep(string name, float value, int unlockLevel, int cost)
        {
            this.name = name;
            this.value = value;
            this.unlockLevel = unlockLevel;
            this.cost = cost;
        }
    }

    /// <summary>플레이어 기본 수치 + 사무실 업그레이드 책상 단계 (GDD 5.1).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("이동")]
        [SerializeField, Min(0.1f), Tooltip("조이스틱을 끝까지 당겼을 때 속도 (m/s). 속도는 입력 크기에 비례")] float moveSpeed = 4f;
        [SerializeField, Min(1f), Tooltip("이동 방향으로 도는 속도 (도/초)")] float turnSpeed = 720f;
        [SerializeField, Range(0f, 1f), Tooltip("입력 크기가 이 값 이상이면 뛰기 애니메이션 (GDD 3.1)")] float runThreshold = 0.5f;

        [Header("운반")]
        [SerializeField, Min(1), Tooltip("기본 적재 수 (손으로 들기)")] int maxCarry = 3;
        [SerializeField, Min(0.01f), Tooltip("집기/내려놓기 간격 (초 / 1개)")] float pickInterval = 0.15f;

        [Header("상호작용")]
        [SerializeField, Min(0f), Tooltip("존 안에서 이만큼 가만히 있어야 상호작용 시작 (초, GDD 5.2)")] float interactionDelay = 0.5f;
        [SerializeField, Min(0f), Tooltip("내려놓기 ↔ 집기를 바꿀 때 쉬는 시간 (초, GDD 5.3). 플레이어만")] float transferSwitchCooldown = 0.5f;

        [Header("업그레이드 (사무실 책상, 기본값 다음 단계부터)")]
        [SerializeField, Tooltip("적재 도구: value = 최대 적재 수")]
        PlayerUpgradeStep[] carrySteps = { new("구루마", 8, 3, 150), new("롤테이너", 12, 5, 400) };
        [SerializeField, Tooltip("이동 속도: value = m/s")]
        PlayerUpgradeStep[] speedSteps = { new("스테미너 1", 4.5f, 2, 100), new("스테미너 2", 5f, 3, 200), new("스테미너 3", 5.5f, 5, 350) }; // 스테미너 = 이동 속도 (GDD 5.1, 2026-11-18)

        public float MoveSpeed => moveSpeed;
        public PlayerUpgradeStep[] CarrySteps => carrySteps;
        public PlayerUpgradeStep[] SpeedSteps => speedSteps;
        public float TurnSpeed => turnSpeed;
        public float RunThreshold => runThreshold;
        public int MaxCarry => maxCarry;
        public float PickInterval => pickInterval;
        public float InteractionDelay => interactionDelay;
        public float TransferSwitchCooldown => transferSwitchCooldown;
    }
}
