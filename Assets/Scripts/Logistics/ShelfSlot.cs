using System.Collections.Generic;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 선반을 세울 자리 (선반 구역 모듈 안). 계약하면 그 회사 선반 구역의 열린 자리부터 순서대로 선반이 생긴다 (GDD 6.3).
    /// 모듈이 붙을 때 스스로 등록하므로, 모듈이 어디에 붙든 Warehouse가 찾는다.
    /// </summary>
    public sealed class ShelfSlot : MonoBehaviour
    {
        static readonly List<ShelfSlot> Registered = new();

        [SerializeField, Min(1), Tooltip("이 레벨부터 쓸 수 있는 자리 (기본 모듈은 모두 1)")] int unlockLevel = 1;
        [SerializeField, Min(0), Tooltip("모듈 안에서 선반을 세우는 순서 (작은 것부터)")] int order;

        /// <summary>등록 순서 = 모듈이 붙은 순서. OnEnable 순서는 보장되지 않으므로 고를 때는 Order를 먼저 본다.</summary>
        public static IReadOnlyList<ShelfSlot> All => Registered;

        /// <summary>선반 + 앞뒤 존이 차지할 바닥 (비어 있어도 나중에 선반이 생기므로 비워 둬야 하는 자리).</summary>
        public Rect WorldFootprint => APC.Interaction.InteractionZone.FootprintRect(transform, new Vector2(2.05f, 1.35f));

        public int UnlockLevel => unlockLevel;
        public int Order => order;
        public Shelf Occupant { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Occupant != null ? new Color(0.3f, 0.9f, 0.4f, 0.6f) : new Color(0.9f, 0.9f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 1.7f, 2.7f));
        }
    }
}
