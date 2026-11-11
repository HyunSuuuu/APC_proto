using System.Collections.Generic;
using UnityEngine;

namespace APC.Interaction
{
    /// <summary>
    /// 알바 작업 지점 (M6). 존 프리팹 안에 1~4개 둔다 — 알바는 빈 슬롯을 예약하고 그 지점에 서서만 상호작용한다
    /// (겹치거나 몰려서 작업하지 않게). 플레이어는 슬롯과 무관하게 존 안 어디서나 상호작용한다.
    /// 존 사각형 안에 둬야 한다 (알바가 서면 그 존이 잡히도록). 화면에는 보이지 않고 Scene 뷰 기즈모로만 보인다.
    /// </summary>
    public sealed class WorkSlot : MonoBehaviour
    {
        /// <summary>예약한 알바. 비어 있으면 null.</summary>
        public Component Owner { get; private set; }

        public bool IsFreeFor(Component who) => Owner == null || Owner == who;

        public bool TryClaim(Component who)
        {
            if (!IsFreeFor(who)) return false;
            Owner = who;
            return true;
        }

        public void Release(Component who)
        {
            if (Owner == who) Owner = null;
        }

        /// <summary>존 안의 빈 슬롯 중 위치에서 가장 가까운 것 (자기가 예약한 슬롯 포함). 없으면 null.</summary>
        public static WorkSlot FindNearestFree(IReadOnlyList<WorkSlot> slots, Component who, Vector3 from)
        {
            WorkSlot best = null;
            var bestDistance = float.MaxValue;
            foreach (var slot in slots)
            {
                if (!slot.isActiveAndEnabled || !slot.IsFreeFor(who)) continue;
                var distance = (slot.transform.position - from).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = slot;
                bestDistance = distance;
            }
            return best;
        }

        void OnDisable() => Owner = null;

        void OnDrawGizmos()
        {
            Gizmos.color = Owner != null ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
    }
}
