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
    /// InboundQueue가 이 칸 앞(도로 너머) 대기 줄 맨 앞 트럭을 넘기면 **후진으로 곧장** 주차하고,
    /// 화물이 다 빠지면 앞으로 나가 칸 앞 도로에서 왼쪽으로 꺾어 떠난다 (2026-11-25). 경로 없이 칸 방향만 써서 모듈이 어디에 붙든 동작한다.
    /// 상차 파트가 시작되면(PartService.OnShift) 남은 상자가 있어도 바로 떠난다 (2026-11-20).
    /// </summary>
    public sealed class InboundBay : MonoBehaviour
    {
        const float AppearDuration = 0.25f;
        const float TurnDuration = 0.35f;

        [SerializeField] InboundTruck truckPrefab;
        [SerializeField, Tooltip("주차 위치 · 방향. 앞(+z) = 바깥쪽, 뒷문은 물류센터 쪽")] Transform parkPoint;
        [SerializeField, Tooltip("주차 칸 바깥 끝 (주차 위치에서 바깥쪽). 도로 · 대기 줄은 여기서 잰다")] Transform spawnPoint;
        [SerializeField, Tooltip("칸 앞 도로 가운데: spawnPoint에서 바깥쪽 (m). 나가는 트럭이 여기서 왼쪽으로 꺾는다 (배경 도로 칸 가운데)")] float roadOffset = 4.8f;
        [SerializeField, Tooltip("대기 줄 맨 앞 자리: spawnPoint에서 바깥쪽 (m) — 도로 너머")] float lineOffset = 12f;
        [SerializeField, Tooltip("나가는 트럭이 도로를 따라 가는 거리 (m). 끝에서 작아지며 사라짐")] float exitDistance = 15f;

        Vector3 Outward => parkPoint.localRotation * Vector3.forward; // 이 베이 로컬

        Vector3 LineSlotLocal(int index, float spacing) => spawnPoint.localPosition + Outward * (lineOffset + spacing * index);

        /// <summary>이 칸 대기 줄 index번째 자리 (월드). 모듈 팝(크기 0 → 1) 중에도 맞게 위치 · 회전만 쓴다.</summary>
        public Vector3 LineSlot(int index, float spacing) => transform.position + transform.rotation * LineSlotLocal(index, spacing);

        /// <summary>줄 선 트럭 방향 (월드) = 주차 방향 (앞 = 바깥쪽, 뒷문이 칸 쪽 — 그대로 후진해 들어온다).</summary>
        public Quaternion LineRotation => transform.rotation * parkPoint.localRotation;

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
                // 트럭은 줄 맨 앞 자리에 있다 (줄에서 왔으면 크기 1, 바로 넘겼으면 크기 0 → 키운다)
                var t = truck.transform;
                var appear = t.localScale.x < 0.999f;
                t.DOKill();
                t.SetParent(transform, false);
                t.SetLocalPositionAndRotation(LineSlotLocal(0, 0f), parkPoint.localRotation);
                var arrive = DOTween.Sequence().SetLink(truck.gameObject);
                if (appear) arrive.Append(t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack));
                else t.localScale = Vector3.one;
                // 임시: 후진 거리가 도로만큼 길어졌지만 시간은 그대로 (하차 속도 · 밸런스 유지)
                yield return arrive
                    .Append(t.DOLocalMove(parkPoint.localPosition, config.TruckReverseDuration).SetEase(Ease.OutCubic))
                    .WaitForCompletion();
                DockedTruck = truck;

                // 황금 트럭이면 TruckPickupZone이 금을 존 바닥에 쏟는다 (GDD 6.4)
                yield return new WaitUntil(() => !truck.HasCargo || leaving);
                if (truck.Golden && !leaving) yield return new WaitForSeconds(GameManager.Instance.Database.GoldenTruck.LeaveDelay);
                DockedTruck = null;

                // 앞으로 나가 칸 앞 도로에서 왼쪽으로 꺾어 도로를 따라 떠난다 (대기 줄은 도로 너머라 안 부딪침)
                var road = spawnPoint.localPosition + Outward * roadOffset;
                var left = Quaternion.Euler(0f, -90f, 0f) * Outward;
                yield return DOTween.Sequence()
                    .Append(t.DOLocalMove(road, config.TruckLeaveDuration * 0.5f).SetEase(Ease.InOutSine))
                    .Append(t.DOLocalRotateQuaternion(Quaternion.LookRotation(left), TurnDuration).SetEase(Ease.OutSine))
                    .Append(t.DOLocalMove(road + left * exitDistance, config.TruckLeaveDuration * 0.5f).SetEase(Ease.InSine))
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
