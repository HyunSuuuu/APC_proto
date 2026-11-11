using UnityEngine;

namespace APC.Staff
{
    /// <summary>사무실 문 앞 — 고용한 알바가 나타나는 곳 (GDD 11.1). 사무실 모듈 안에 있으므로 스스로 등록한다.</summary>
    public sealed class StaffSpawnPoint : MonoBehaviour
    {
        public static StaffSpawnPoint Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.6f, new Vector3(0.8f, 1.2f, 0.8f));
        }
    }
}
