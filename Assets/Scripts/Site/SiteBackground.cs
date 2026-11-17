using APC.Core;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 배경 모듈 (M14-7): `SiteLayout.Background` 표대로 부지 밖 칸에 장식 프리팹을 시작할 때 놓는다.
    /// 지금은 트럭이 쓰는 주차장 바닥(`Module_BgParking`) — 하차 · 상차 모듈은 문 · 지붕만 갖고, 바닥은 여기서 처음부터 깔린다 (두 모듈이 한 칸을 같이 써도 안 겹침).
    /// 걸을 수 없고 충돌체 · 해금 없음.
    /// </summary>
    public sealed class SiteBackground : MonoBehaviour
    {
        void Start()
        {
            var layout = GameManager.Instance.Database.Site;
            var root = new GameObject("Background").transform;
            root.SetParent(transform, false);
            foreach (var placement in layout.Background)
            {
                if (placement.prefab == null) continue;
                var position = layout.CellCenter(placement.row, placement.column);
                Instantiate(placement.prefab, position, Quaternion.Euler(0f, placement.rotation, 0f), root);
            }
        }
    }
}
