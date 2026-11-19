using UnityEngine;

namespace APC.Interaction
{
    /// <summary>
    /// 알바 서는 위치 후보 (M6, 2026-11-19 개편). 존 프리팹 안에 1~5개 둔다. **예약하지 않는다** —
    /// 알바는 후보 중 지금 사람이 가장 적은 곳(같으면 가까운 곳)에 서고, 모두 차 있으면 존 안에서 비켜 선다 (StaffWorker).
    /// 존 사각형 안에 둬야 한다 (알바가 서면 그 존이 잡히도록). 화면에는 보이지 않고 Scene 뷰 기즈모로만 보인다.
    /// 재포장 테이블의 1명 제한은 슬롯이 아니라 `RepackTable`이 한다.
    /// </summary>
    public sealed class WorkSlot : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
    }
}
