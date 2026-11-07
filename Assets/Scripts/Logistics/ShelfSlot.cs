using UnityEngine;

namespace APC.Logistics
{
    /// <summary>선반을 세울 자리. 계약하면 열린 자리부터 순서대로 선반이 생긴다 (GDD 6.3, 10.3).</summary>
    public sealed class ShelfSlot : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("이 레벨부터 쓸 수 있는 자리 (선반 구역 A 추가 2자리 = Lv.5)")] int unlockLevel = 1;

        public int UnlockLevel => unlockLevel;
        public Shelf Occupant { get; set; }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Occupant != null ? new Color(0.3f, 0.9f, 0.4f, 0.6f) : new Color(0.9f, 0.9f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 1.7f, 2.7f));
        }
    }
}
