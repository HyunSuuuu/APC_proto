using System.Collections;
using APC.Core;
using APC.Visuals;
using APC.Orders;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 상차 베이 1곳 (GDD 7.6, 8.3). 상차 주차장 모듈 안에 있다.
    /// 칸이 비면 **새 주문을 바로 만들어**(OrderService.CreateForTruck) 그 트럭이 주차 칸 바깥쪽에 나타나 **후진으로 곧장** 주차하고,
    /// 주문을 다 실으면 앞으로 나가 사라지며 옆 현금 존에 보상이 쌓인다 (GDD 7.6 · 8.2). InboundBay와 같은 방식이라 모듈이 어디에 붙든 혼자 동작한다.
    /// </summary>
    public sealed class OutboundBay : MonoBehaviour
    {
        const float AppearDuration = 0.25f;

        [SerializeField] OutboundTruck truckPrefab;
        [SerializeField, Tooltip("주차 위치 · 방향. 앞(+z) = 바깥쪽, 뒷문은 물류센터 쪽")] Transform parkPoint;
        [SerializeField, Tooltip("트럭이 나타나고 사라지는 위치 (주차 위치에서 바깥쪽)")] Transform spawnPoint;
        [SerializeField, Tooltip("주문 보상 현금이 쌓이는 곳 (상차 존 옆)")] CashZone cashZone;

        /// <summary>정차해서 상차를 받는 중인 트럭. 없으면 null.</summary>
        public OutboundTruck DockedTruck { get; private set; }

        IEnumerator Start()
        {
            var gm = GameManager.Instance;
            var config = gm.Database.Logistics;

            while (true)
            {
                Order order = null;
                // 상차 파트(2)일 때만 새 트럭 (사무실 전은 언제나, 2026-11-19)
                yield return new WaitUntil(() => Economy.PartService.OutboundOpen && OrderService.Instance != null && (order = OrderService.Instance.CreateForTruck()) != null);
                var orders = OrderService.Instance;

                // 로컬 좌표로 움직인다 (확장 모듈이 커지는 중에 나와도 제자리로, InboundBay와 같음)
                var truck = Instantiate(truckPrefab, transform);
                truck.Assign(order);

                var t = truck.transform;
                t.SetLocalPositionAndRotation(spawnPoint.localPosition, parkPoint.localRotation);
                t.localScale = Vector3.zero;
                if (truck.TryGetComponent<WallCutoutTarget>(out var cutout)) cutout.enabled = true; // 주차장 지붕 구멍 (떠나면 지워짐)
                yield return DOTween.Sequence()
                    .Append(t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack))
                    .Append(t.DOLocalMove(parkPoint.localPosition, config.OutboundReverseDuration).SetEase(Ease.OutCubic))
                    .SetLink(truck.gameObject)
                    .WaitForCompletion();
                DockedTruck = truck;
                orders.MarkDocked(order);

                yield return new WaitUntil(() => order.IsFulfilled);
                DockedTruck = null;

                // 마지막 상자가 뒷문에 들어갈 때까지 기다렸다가 출발한다. 출발하는 순간 XP · 출고 현금 (GDD 7.6).
                yield return new WaitForSeconds(gm.Database.Carry.FlyDuration);
                var reward = orders.Complete(order);
                if (cashZone != null) cashZone.AddCash(reward);
                DOTween.Sequence()
                    .Append(t.DOLocalMove(spawnPoint.localPosition, config.OutboundLeaveDuration).SetEase(Ease.InCubic))
                    .Append(t.DOScale(0f, AppearDuration).SetEase(Ease.InBack))
                    .SetLink(truck.gameObject)
                    .OnComplete(() => Destroy(truck.gameObject));
                yield return new WaitForSeconds(config.OutboundRespawnDelay);
            }
        }

        void OnDrawGizmos()
        {
            if (parkPoint == null || spawnPoint == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(spawnPoint.position + Vector3.up * 0.5f, parkPoint.position + Vector3.up * 0.5f);
            Gizmos.DrawWireSphere(spawnPoint.position + Vector3.up * 0.5f, 0.4f);
        }
    }
}
