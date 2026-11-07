using System.Collections;
using System.Collections.Generic;
using APC.Contracts;
using APC.Core;
using APC.Data;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 입고 베이 1곳 (GDD 7.1). 계약이 있으면 트럭을 불러 정차시키고, 화물이 다 빠지면 출발시킨 뒤 다음 트럭을 부른다.
    /// 트럭 경로는 씬의 DOTweenPath(Pro)에서 웨이포인트만 읽어 쓴다 — 경로는 씬에서 직접 고친다.
    /// </summary>
    public sealed class InboundBay : MonoBehaviour
    {
        [SerializeField] InboundTruck truckPrefab;
        [SerializeField, Tooltip("도로 → 정차 위치. 시작점 = 이 오브젝트 위치")] DOTweenPath arrivePath;
        [SerializeField, Tooltip("정차 위치 → 도로 밖")] DOTweenPath leavePath;

        /// <summary>정차해서 화물을 내주는 중인 트럭. 없으면 null.</summary>
        public InboundTruck DockedTruck { get; private set; }

        IEnumerator Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            var contracts = ContractService.Instance;

            while (true)
            {
                yield return new WaitUntil(() => contracts.HasAnyContract);

                var company = contracts.PickInboundCompany();
                var truck = Instantiate(truckPrefab, transform);
                truck.Load(company, ComposeLoad(company));

                yield return Drive(truck.transform, arrivePath, config.TruckArriveDuration).WaitForCompletion();
                DockedTruck = truck;

                yield return new WaitUntil(() => !truck.HasCargo);
                DockedTruck = null;

                Drive(truck.transform, leavePath, config.TruckLeaveDuration)
                    .OnComplete(() => Destroy(truck.gameObject));
                yield return new WaitForSeconds(config.InboundRespawnDelay);
            }
        }

        /// <summary>트럭에 실을 품목. M4에서 교착 방지 가중치(GDD 7.8)로 바꾼다.</summary>
        static List<ItemDefinition> ComposeLoad(CompanyDefinition company)
        {
            var load = new List<ItemDefinition>(company.InboundTruckCapacity);
            for (var i = 0; i < company.InboundTruckCapacity; i++)
                load.Add(company.Items[Random.Range(0, company.Items.Count)]);
            return load;
        }

        static Tween Drive(Transform truck, DOTweenPath path, float duration)
        {
            var start = path.transform.position;
            var waypoints = path.wps.ToArray();
            truck.position = start;
            if (waypoints.Length > 0 && waypoints[0] != start)
                truck.rotation = Quaternion.LookRotation(waypoints[0] - start, Vector3.up);

            return truck.DOPath(waypoints, duration, path.pathType)
                .SetLookAt(0.01f)
                .SetEase(Ease.InOutSine)
                .SetLink(truck.gameObject);
        }
    }
}
