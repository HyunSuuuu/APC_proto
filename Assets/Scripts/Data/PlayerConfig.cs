using UnityEngine;

namespace APC.Data
{
    /// <summary>플레이어 기본 수치 (GDD 5.1). 업그레이드 값은 2순위에서 추가한다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("이동")]
        [SerializeField, Min(0.1f), Tooltip("조이스틱을 끝까지 당겼을 때 속도 (m/s). 속도는 입력 크기에 비례")] float moveSpeed = 4f;
        [SerializeField, Min(1f), Tooltip("이동 방향으로 도는 속도 (도/초)")] float turnSpeed = 720f;
        [SerializeField, Range(0f, 1f), Tooltip("입력 크기가 이 값 이상이면 뛰기 애니메이션 (GDD 3.1)")] float runThreshold = 0.5f;

        [Header("운반")]
        [SerializeField, Min(1)] int maxCarry = 5;
        [SerializeField, Min(0.01f), Tooltip("집기/내려놓기 간격 (초 / 1개)")] float pickInterval = 0.15f;

        [Header("상호작용")]
        [SerializeField, Min(0f), Tooltip("존 안에서 이만큼 가만히 있어야 상호작용 시작 (초, GDD 5.2)")] float interactionDelay = 0.5f;

        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;
        public float RunThreshold => runThreshold;
        public int MaxCarry => maxCarry;
        public float PickInterval => pickInterval;
        public float InteractionDelay => interactionDelay;
    }
}
