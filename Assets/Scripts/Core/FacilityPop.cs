using DG.Tweening;
using UnityEngine;

namespace APC.Core
{
    /// <summary>시설 생성 팝 연출 (체크리스트 M3-6). 선반 · 구매한 시설 · 확장 칸이 같이 쓴다.</summary>
    public static class FacilityPop
    {
        public static Tween Play(Transform target)
        {
            var duration = GameManager.Instance.Database.Logistics.FacilityPopDuration;
            target.DOKill();
            target.localScale = Vector3.zero;
            return target.DOScale(1f, duration).SetEase(Ease.OutBack, 2.2f).SetLink(target.gameObject);
        }
    }
}
