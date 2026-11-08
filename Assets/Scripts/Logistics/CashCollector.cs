using System.Collections.Generic;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>현금을 흡수할 수 있는 대상 (플레이어). 현금 존이 이 목록에서 가까운 대상을 찾는다 (GDD 7.6).</summary>
    public sealed class CashCollector : MonoBehaviour
    {
        static readonly List<CashCollector> Registered = new();

        [SerializeField, Tooltip("현금 뭉치가 날아와 사라지는 지점 (발 기준)")] Vector3 catchOffset = new(0f, 1f, 0f);

        public static IReadOnlyList<CashCollector> All => Registered;

        public Vector3 CatchPoint => transform.position + catchOffset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);
    }
}
