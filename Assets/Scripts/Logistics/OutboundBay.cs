using System.Collections;
using APC.Core;
using APC.Orders;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 상차 베이 1곳 (GDD 7.6, 8.3). 상차 주차장 모듈 안에 있다.
    /// 칸이 비면 **새 주문을 바로 만들어**(OrderService.CreateForTruck) 그 트럭이 주차 칸 바깥쪽에 나타나 **후진으로 곧장** 주차하고,
    /// 주문을 다 실으면 앞으로 나가 사라지며 옆 현금 존에 보상이 쌓인다 (GDD 7.6 · 8.2). InboundBay와 같은 방식이라 모듈이 어디에 붙든 혼자 동작한다.
    /// 2파트가 끝나 정산하기 직전(PartService.OnDayEnding)엔 덜 실었어도 바로 떠나고, 실은 만큼의 돈은 저금통에 바로 넣어 이번 정산에 잡힌다 (2026-11-20).
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

        Order current;
        bool sentAway; // 하루 끝 → 회차
        bool paid;     // 회차 때 이미 정산함

        /// <summary>하루 끝: 정차한 트럭은 실은 만큼 바로 저금통에 (현금 더미 없이), 오는 중이면 도착하자마자 빈 채로 떠남.</summary>
        void SendAway()
        {
            sentAway = true;
            if (current == null || DockedTruck == null) return;
            var reward = OrderService.Instance.Complete(current);
            if (current.LoadedCount > 0) Economy.PartService.Instance?.CountShipment(current.LoadedCount, reward);
            if (reward > 0) GameManager.Instance.Wallet.AddToPiggy(reward);
            paid = true;
        }

        IEnumerator Start()
        {
            var gm = GameManager.Instance;
            var config = gm.Database.Logistics;
            if (Economy.PartService.Instance != null) Economy.PartService.Instance.OnDayEnding.Subscribe(_ => SendAway()).AddTo(this);

            while (true)
            {
                Order order = null;
                current = null;
                // 상차 파트(2)일 때만 새 트럭 (사무실 전은 언제나, 2026-11-19)
                yield return new WaitUntil(() => Economy.PartService.OutboundOpen && OrderService.Instance != null && (order = OrderService.Instance.CreateForTruck()) != null);
                var orders = OrderService.Instance;
                sentAway = paid = false; // 이번 상차 파트 트럭 (그 전 하루 끝 신호는 무시)

                // 로컬 좌표로 움직인다 (확장 모듈이 커지는 중에 나와도 제자리로, InboundBay와 같음)
                var truck = Instantiate(truckPrefab, transform);
                truck.Assign(order);
                current = order;

                var t = truck.transform;
                t.SetLocalPositionAndRotation(spawnPoint.localPosition, parkPoint.localRotation);
                t.localScale = Vector3.zero;
                yield return DOTween.Sequence()
                    .Append(t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack))
                    .Append(t.DOLocalMove(parkPoint.localPosition, config.OutboundReverseDuration).SetEase(Ease.OutCubic))
                    .SetLink(truck.gameObject)
                    .WaitForCompletion();
                DockedTruck = truck;
                orders.MarkDocked(order);

                yield return new WaitUntil(() => order.IsFulfilled || sentAway);
                if (sentAway && !paid) SendAway(); // 오는 중에 하루가 끝남 → 지금 빈 채로 정리
                DockedTruck = null;

                if (!sentAway)
                {
                    // 마지막 상자가 뒷문에 들어갈 때까지 기다렸다가 출발한다. 출발하는 순간 XP · 출고 현금 (GDD 7.6).
                    yield return new WaitForSeconds(gm.Database.Carry.FlyDuration);
                    var reward = orders.Complete(order);
                    Economy.PartService.Instance?.CountShipment(order.LoadedCount, reward); // 영수증 집계
                    if (cashZone != null) cashZone.AddCash(reward);
                }
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
