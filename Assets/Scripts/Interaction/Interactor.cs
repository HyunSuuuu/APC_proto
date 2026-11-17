using System.Collections.Generic;
using APC.Logistics;
using UnityEngine;

namespace APC.Interaction
{
    /// <summary>
    /// 존과 상호작용하는 주체 (플레이어, 알바). 자기 위치의 존을 찾아 매 프레임 진행시킨다.
    /// 존 안에서 readyDelay 이상 가만히 있어야 상호작용이 시작된다 (GDD 5.2).
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        // 이 속도(m/s) 이하면 가만히 있는 것으로 본다. 이동 방식(조이스틱·네비메시)과 무관하게 위치 변화로 판단한다.
        const float StillSpeed = 0.05f;

        static readonly List<Interactor> AllInteractors = new();

        [SerializeField] CarryStack stack;

        float readyDelay = 0.5f;
        float transferInterval = 0.15f;
        float stillTime;
        Vector3 lastPosition;
        InteractionZone zone;

        /// <summary>활성화된 모든 interactor (들고 있는 화물 수 집계용).</summary>
        public static IReadOnlyList<Interactor> All => AllInteractors;

        public CarryStack Stack => stack;
        public InteractionZone CurrentZone => zone;

        /// <summary>이번 프레임에 멈춰 있는가.</summary>
        public bool IsStill => stillTime > 0f;

        /// <summary>운반 존 상호작용 가능 상태 (존 안에서 readyDelay 이상 멈춰 있음).</summary>
        public bool IsReady => stillTime >= readyDelay;

        /// <summary>대기 진행도 0~1 (존 바닥 표시용). 알바가 목표가 아닌 존을 지나갈 때는 0.</summary>
        public float ReadyProgress => IsBlockedHere ? 0f : readyDelay <= 0f ? 1f : Mathf.Clamp01(stillTime / readyDelay);

        /// <summary>
        /// 알바 규칙 (M6): true면 <see cref="Target"/> 존에서만 상호작용한다 — 지나가거나 기다리다 다른 존에 걸려도 아무것도 옮기지 않는다.
        /// </summary>
        public bool RestrictToTarget { get; set; }

        /// <summary>RestrictToTarget일 때 상호작용할 존 (작업 지점에 도착하면 알바가 정한다).</summary>
        public InteractionZone Target { get; set; }

        /// <summary>내려놓기 / 집기 허용 (알바가 작업 종류에 맞춰 끈다 — 예: 하차 알바는 팔레트에서 집지 않음). 기본 둘 다 true.</summary>
        public bool AllowDrop { get; set; } = true;
        public bool AllowPick { get; set; } = true;

        bool IsBlockedHere => RestrictToTarget && zone != Target;

        /// <summary>집기/내려놓기 간격 (초). 플레이어 = PlayerConfig, 알바 = StaffRules.</summary>
        public float TransferInterval => transferInterval;

        /// <summary>CargoZone이 간격을 재는 타이머. 상호작용이 막 시작되면 간격만큼 차 있어 첫 1개는 바로 옮긴다.</summary>
        internal float TransferTimer { get; set; }

        /// <summary>
        /// 작업 고정 (GDD 5.3, 플레이어만): 들고 있는 화물이 있으면 **같은 출처의 화물만** 더 집을 수 있다.
        /// 예: 팔레트에서 분류용으로 집은 화물을 들고 있으면, 선반에서 주문용 화물을 집지 않는다 (다 내려놓아야 다음 일).
        /// </summary>
        public bool TaskLock { get; private set; }

        /// <summary>내려놓기 ↔ 집기를 바꿀 때 쉬는 시간 (초). 0이면 없음 (알바).</summary>
        public float SwitchCooldown { get; private set; }

        enum TransferKind { None, Drop, Pick }

        TransferKind lastKind;
        float lastTransferTime;

        public void Configure(float interval, float delay)
        {
            transferInterval = Mathf.Max(0.01f, interval);
            readyDelay = Mathf.Max(0f, delay);
        }

        /// <summary>플레이어 규칙 (작업 고정 · 전환 쿨타임).</summary>
        public void ConfigureRules(bool taskLock, float switchCooldown)
        {
            TaskLock = taskLock;
            SwitchCooldown = Mathf.Max(0f, switchCooldown);
        }

        /// <summary>내려놓기(drop = true) / 집기를 지금 할 수 있는가 — 반대 동작 직후 쿨타임 동안은 막는다.</summary>
        internal bool CanTransfer(bool drop)
        {
            if (!(drop ? AllowDrop : AllowPick)) return false;
            var kind = drop ? TransferKind.Drop : TransferKind.Pick;
            return lastKind == TransferKind.None || lastKind == kind || Time.time - lastTransferTime >= SwitchCooldown;
        }

        internal void NotifyTransfer(bool drop)
        {
            lastKind = drop ? TransferKind.Drop : TransferKind.Pick;
            lastTransferTime = Time.time;
        }

        /// <summary>작업 고정: 이 출처의 화물을 더 집어도 되는가.</summary>
        internal bool CanPickOrigin(CargoOrigin origin)
        {
            if (!TaskLock || stack == null) return true;
            foreach (var cargo in stack.Items)
            {
                if (cargo.Origin != origin) return false;
            }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AllInteractors.Clear();

        void OnEnable()
        {
            AllInteractors.Add(this);
            lastPosition = transform.position;
            stillTime = 0f;
        }

        void OnDisable()
        {
            AllInteractors.Remove(this);
            SetZone(null);
        }

        void Update()
        {
            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            var position = transform.position;
            var moved = position - lastPosition;
            moved.y = 0f;
            lastPosition = position;
            stillTime = moved.magnitude / deltaTime <= StillSpeed ? stillTime + deltaTime : 0f;

            SetZone(InteractionZone.FindAt(position));

            if (zone == null || IsBlockedHere || !(zone.TicksWhileMoving || (zone.WaitsForReadyDelay ? IsReady : IsStill)))
            {
                TransferTimer = transferInterval;
                return;
            }
            zone.Tick(this, deltaTime);
        }

        void SetZone(InteractionZone next)
        {
            if (next == zone) return;
            var previous = zone;
            zone = next;
            if (previous != null) previous.Exit(this);
            if (next != null) next.Enter(this);
        }
    }
}
