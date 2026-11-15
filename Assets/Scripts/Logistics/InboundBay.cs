using System.Collections;
using System.Collections.Generic;
using APC.Contracts;
using APC.Core;
using APC.Data;
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

        /// <summary>이 하차 칸의 상자 두는 공간 (하차장 모듈 Content에 붙은 팔레트, GDD 7.2).</summary>
        public SharedStorage Storage { get; private set; }

        void Awake() => Storage = GetComponentInParent<SharedStorage>();

        IEnumerator Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            var contracts = ContractService.Instance;

            while (true)
            {
                // 그 색 보관 구역(선반)이 없으면 그 색 트럭은 안 나온다 (GDD 6.3)
                var candidates = new List<CompanyDefinition>();
                yield return new WaitUntil(() =>
                {
                    candidates.Clear();
                    foreach (var c in contracts.Contracted)
                    {
                        if (Warehouse.Instance.HasShelf(c.Items[0])) candidates.Add(c);
                    }
                    return candidates.Count > 0;
                });

                var company = StockBalancer.PickInboundCompany(candidates);
                // 로컬 좌표로 움직인다: 확장 모듈은 크기 0에서 커지는 중(FacilityPop)에 첫 트럭이 나오므로,
                // 월드 좌표를 그때 잡으면 엉뚱한 곳에서 들어온다. 주차 · 생성 지점은 이 베이의 바로 아래 자식.
                var truck = Instantiate(truckPrefab, transform);
                truck.Load(company, StockBalancer.ComposeInboundLoad(company));

                var t = truck.transform;
                t.SetLocalPositionAndRotation(spawnPoint.localPosition, parkPoint.localRotation);
                t.localScale = Vector3.zero;
                yield return DOTween.Sequence()
                    .Append(t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack))
                    .Append(t.DOLocalMove(parkPoint.localPosition, config.TruckReverseDuration).SetEase(Ease.OutCubic))
                    .SetLink(truck.gameObject)
                    .WaitForCompletion();
                DockedTruck = truck;

                yield return new WaitUntil(() => !truck.HasCargo);
                DockedTruck = null;

                DOTween.Sequence()
                    .Append(t.DOLocalMove(spawnPoint.localPosition, config.TruckLeaveDuration).SetEase(Ease.InCubic))
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
