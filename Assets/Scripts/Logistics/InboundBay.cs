using System.Collections;
using System.Collections.Generic;
using APC.Core;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 하차 베이 1곳 (GDD 7.1). 주차장 모듈 안에 있다.
    /// InboundQueue가 넘긴 트럭이 주차 칸 바깥쪽에 나타나 **후진으로 곧장** 주차하고, 화물이 다 빠지면 앞으로 나가 사라진다.
    /// 도로 · 경로가 필요 없어서 모듈이 어디에 붙든 혼자 동작한다.
    /// 상차 파트가 시작되면(PartService.OnShift) 남은 상자가 있어도 바로 떠난다 (2026-11-20).
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

        static readonly List<InboundBay> Registered = new();

        /// <summary>켜진 하차 칸 전부 (남은 시간 보너스 판단, M17-3).</summary>
        public static IReadOnlyList<InboundBay> All => Registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        InboundTruck received;
        bool leaving; // 상차 파트 시작 → 회차

        /// <summary>InboundQueue가 이 칸에 트럭을 넘긴다 (도착 · 줄 맨 앞).</summary>
        public void Receive(InboundTruck truck) => received = truck;

        void OnEnable() => Registered.Add(this);

        void OnDisable()
        {
            Registered.Remove(this);
            InboundQueue.Instance?.Cancel(this);
        }

        IEnumerator Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            yield return new WaitUntil(() => InboundQueue.Instance != null);
            if (Economy.PartService.Instance != null) Economy.PartService.Instance.OnShift.Subscribe(_ => leaving = true).AddTo(this);

            while (true)
            {
                // 트럭 도착 · 색은 InboundQueue가 정한다 (GDD 7.1 차량 대기열)
                received = null;
                InboundQueue.Instance.Request(this);
                yield return new WaitUntil(() => received != null);
                var truck = received;
                leaving = false; // 하차 파트에 받은 트럭 (그 전 교대 신호는 무시)

                // 로컬 좌표로 움직인다: 확장 모듈은 크기 0에서 커지는 중(FacilityPop)에 첫 트럭이 나오므로,
                // 월드 좌표를 그때 잡으면 엉뚱한 곳에서 들어온다. 주차 · 생성 지점은 이 베이의 바로 아래 자식.
                var t = truck.transform;
                t.DOKill();
                t.SetParent(transform, false);
                t.SetLocalPositionAndRotation(spawnPoint.localPosition, parkPoint.localRotation);
                t.localScale = Vector3.zero;
                yield return DOTween.Sequence()
                    .Append(t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack))
                    .Append(t.DOLocalMove(parkPoint.localPosition, config.TruckReverseDuration).SetEase(Ease.OutCubic))
                    .SetLink(truck.gameObject)
                    .WaitForCompletion();
                DockedTruck = truck;

                // 황금 트럭이면 TruckPickupZone이 금을 존 바닥에 쏟는다 (GDD 6.4)
                yield return new WaitUntil(() => !truck.HasCargo || leaving);
                if (truck.Golden && !leaving) yield return new WaitForSeconds(GameManager.Instance.Database.GoldenTruck.LeaveDelay);
                DockedTruck = null;

                yield return DOTween.Sequence()
                    .Append(t.DOLocalMove(spawnPoint.localPosition, config.TruckLeaveDuration).SetEase(Ease.InCubic))
                    .Append(t.DOScale(0f, AppearDuration).SetEase(Ease.InBack))
                    .SetLink(truck.gameObject)
                    .OnComplete(() => TruckPool.Release(truck))
                    .WaitForCompletion();
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
