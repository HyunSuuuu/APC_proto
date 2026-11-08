using System.Collections;
using APC.Contracts;
using APC.Core;
using APC.Orders;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 하차 베이 1곳 (GDD 7.1). 주차장 모듈 안에 있다.
    /// 계약이 있으면 트럭이 주차 칸 바깥쪽에 나타나 **후진으로 곧장** 주차하고, 화물이 다 빠지면 앞으로 나가 사라진다.
    /// 도로 · 경로가 필요 없어서 모듈이 어디에 붙든 혼자 동작한다.
    /// </summary>
    public sealed class InboundBay : MonoBehaviour
    {
        const float AppearDuration = 0.25f;

        [SerializeField] InboundTruck truckPrefab;
        [SerializeField, Tooltip("주차 위치 · 방향. 앞(+z) = 바깥쪽, 뒷문은 물류센터 쪽")] Transform parkPoint;
        [SerializeField, Tooltip("트럭이 나타나고 사라지는 위치 (주차 위치에서 바깥쪽)")] Transform spawnPoint;

        /// <summary>주차해서 화물을 내주는 중인 트럭. 없으면 null.</summary>
        public InboundTruck DockedTruck { get; private set; }

        IEnumerator Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            var contracts = ContractService.Instance;

            while (true)
            {
                yield return new WaitUntil(() => contracts.HasAnyContract);

                var company = StockBalancer.PickInboundCompany(contracts.Contracted);
                var truck = Instantiate(truckPrefab, spawnPoint.position, parkPoint.rotation, transform);
                truck.Load(company, StockBalancer.ComposeInboundLoad(company));

                var t = truck.transform;
                t.localScale = Vector3.zero;
                yield return DOTween.Sequence()
                    .Append(t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack))
                    .Append(t.DOMove(parkPoint.position, config.TruckReverseDuration).SetEase(Ease.OutCubic))
                    .SetLink(truck.gameObject)
                    .WaitForCompletion();
                DockedTruck = truck;

                yield return new WaitUntil(() => !truck.HasCargo);
                DockedTruck = null;

                DOTween.Sequence()
                    .Append(t.DOMove(spawnPoint.position, config.TruckLeaveDuration).SetEase(Ease.InCubic))
                    .Append(t.DOScale(0f, AppearDuration).SetEase(Ease.InBack))
                    .SetLink(truck.gameObject)
                    .OnComplete(() => Destroy(truck.gameObject));
                yield return new WaitForSeconds(config.InboundRespawnDelay);
            }
        }

        void OnDrawGizmos()
        {
            if (parkPoint == null || spawnPoint == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(spawnPoint.position + Vector3.up * 0.5f, parkPoint.position + Vector3.up * 0.5f);
            Gizmos.DrawWireSphere(spawnPoint.position + Vector3.up * 0.5f, 0.4f);
        }
    }
}
