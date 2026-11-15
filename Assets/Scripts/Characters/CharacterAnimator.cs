using APC.Logistics;
using UnityEngine;

namespace APC.Characters
{
    /// <summary>
    /// 캐릭터 모델 애니 (플레이어 · 알바 공용). 실제 이동 속도와 운반 스택으로 상태를 고른다:
    /// 멈춤 = idle, 빈손 이동 = walk, 상자 들고 이동 = carry, 끌기 도구(구루마 · 롤테이너)로 이동 = pull.
    /// Animator는 자식 중 켜진 모델의 것을 쓴다 (알바는 모델 여러 개 중 하나만 켜짐). 상태 이름은 컨트롤러의 상태 이름과 같다.
    /// </summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        static readonly int Idle = Animator.StringToHash("idle");
        static readonly int Walk = Animator.StringToHash("walk");
        static readonly int Carry = Animator.StringToHash("carry");
        static readonly int Pull = Animator.StringToHash("pull");

        [SerializeField] CarryStack stack;
        [SerializeField, Min(0f), Tooltip("이 속도(m/s)보다 빠르면 걷는 중")] float moveThreshold = 0.15f;
        [SerializeField, Min(0f)] float crossFade = 0.12f;

        Animator animator;
        Vector3 lastPosition;
        int current;

        /// <summary>끌기 도구를 쓰는 중 (플레이어 적재 도구 구루마 이상). 이동하면 pull.</summary>
        public bool UsePull { get; set; }

        void OnEnable()
        {
            lastPosition = transform.position;
            current = 0;
        }

        void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                current = 0;
                if (animator == null) return;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return; // 팝업으로 멈춘 동안은 그대로

            var delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            var moving = delta.magnitude / deltaTime > moveThreshold;

            var state = !moving ? Idle
                : UsePull ? Pull
                : stack != null && stack.Count.CurrentValue > 0 ? Carry
                : Walk;
            if (state == current) return;
            current = state;
            animator.CrossFadeInFixedTime(state, crossFade);
        }
    }
}
