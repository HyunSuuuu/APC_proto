using System.Collections;
using System.Collections.Generic;
using APC.Contracts;
using APC.Core;
using APC.Data;
using APC.Orders;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 하차 트럭 도착 · 차량 대기열 (GDD 7.1, M13-9). 물류센터 전체에 `inboundArrivalInterval`마다 트럭 1대가 도착한다 (시작 때 1대 바로).
    /// 빈 하차 칸(기다리는 InboundBay)이 있으면 바로 넘기고, 없으면 부지 밖 도로의 대기 자리에 줄을 세운다. 줄이 차면 도착을 멈춘다.
    /// 하차장에 넘기는 것은 하차 파트(1)와 사무실 전에만 (PartService.InboundOpen, 2026-11-19). 상차 파트 동안 도착한 트럭은 줄에서 기다린다.
    /// 트럭 색은 도착할 때 정한다. SiteGrid 오브젝트에 붙는다 (줄 자리는 월드 좌표).
    /// </summary>
    public sealed class InboundQueue : MonoBehaviour
    {
        const float AppearDuration = 0.25f;

        [SerializeField] InboundTruck truckPrefab;
        [SerializeField, Tooltip("줄 맨 앞 트럭 자리 (월드)")] Vector3 headPosition = new(0f, 0f, -21f);
        [SerializeField, Tooltip("맨 앞에서 뒤로 줄이 늘어나는 방향 (월드)")] Vector3 lineDirection = Vector3.forward;
        [SerializeField, Min(1f), Tooltip("대기 자리 간격 (m)")] float spacing = 7f;
        [SerializeField, Tooltip("줄 선 트럭이 보는 방향 (월드 Y 회전)")] float truckYaw = 180f;
        [SerializeField, Tooltip("대기 자리 표시 재질 (반투명, M_TruckWaitSlot). 빌드에서 Shader.Find는 셰이더가 빠져 null — 에셋으로 둔다")] Material markerMaterial;

        public static InboundQueue Instance { get; private set; }

        readonly List<InboundTruck> line = new();
        readonly List<InboundBay> waiting = new();
        int arrived; // 만든 하차 트럭 수 (첫 대 = 튜토리얼 트럭)
        int docked;  // 하차장에 넘긴 트럭 수

        public int Count => line.Count;

        /// <summary>디버그 메뉴: 다음 도착 트럭을 황금 트럭으로.</summary>
        public static bool ForceGoldenNext;

        void Awake() => Instance = this;

        /// <summary>줄의 트럭을 모두 회차: 줄 뒤쪽으로 빠져나가며 작아진 뒤 사라짐.</summary>
        void SendAwayLine()
        {
            foreach (var truck in line)
            {
                var t = truck.transform;
                t.DOKill();
                DOTween.Sequence().SetLink(truck.gameObject)
                    .Append(t.DOMove(t.position + lineDirection.normalized * spacing * 3f, 0.8f).SetEase(Ease.InCubic))
                    .Join(t.DOScale(0f, 0.8f).SetEase(Ease.InBack))
                    .OnComplete(() => TruckPool.Release(truck));
            }
            line.Clear();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            // 상차 파트가 되면 줄에 선 하차 트럭은 바로 돌아간다 (2026-11-20). 하차장에 정차한 트럭은 남음
            if (Economy.PartService.Instance != null) Economy.PartService.Instance.OnShift.Subscribe(_ => SendAwayLine()).AddTo(this);
            var config = GameManager.Instance.Database.Logistics;
            if (truckPrefab != null) TruckPool.Prewarm(truckPrefab, config.InboundQueueSize + 2); // 임시: 트럭 TMP Awake 비용을 시작 때 (풀 최대 16에서 잘림)
            for (var i = 0; i < config.InboundQueueSize; i++) CreateMarker(i);

            CompanyDefinition large = null;
            var normalOpen = false;
            while (true)
            {
                // 트럭 = 일반(보관 모듈 A~C가 하나라도 있을 때) · 보라(D보관을 지은 뒤, largeTruckChance) · 황금 (GDD 6.3, 2026-11-24 — 색 뽑기 · 3대 연속 삭제)
                yield return new WaitUntil(() =>
                {
                    if (truckPrefab == null || line.Count >= config.InboundQueueSize || !Site.StartSequence.TrucksOpen) return false; // 상차 1을 지어야 시작 (GDD 9.6)
                    large = null;
                    normalOpen = false;
                    foreach (var c in GameManager.Instance.Database.Companies)
                    {
                        if (!Warehouse.Instance.HasShelf(c.Items[0])) continue;
                        if (!c.IsLarge) normalOpen = true;
                        else if (System.Linq.Enumerable.Contains(ContractService.Instance.Contracted, c)) large = c;
                    }
                    return normalOpen || large != null;
                });

                var truck = Instantiate(truckPrefab, transform);
                var tutorialTruck = arrived++ == 0 && !Site.StartSequence.OfficeBuilt; // 튜토리얼 트럭: 상자 TutorialBoxes개, 황금 없음
                var step = GameManager.Instance.Upgrades.TruckStep.CurrentValue;
                if (large != null && (!normalOpen || (!tutorialTruck && Random.value < config.LargeTruckChance)))
                    truck.Load(large, StockBalancer.ComposeInboundLoad(large));
                else
                    truck.LoadNormal(tutorialTruck ? Mathf.Min(Economy.PartService.TutorialBoxes, config.GetNormalTruckCapacity(step)) : config.GetNormalTruckCapacity(step));
                if (!tutorialTruck && (ForceGoldenNext || Random.value < GameManager.Instance.Database.GoldenTruck.Chance)) truck.MakeGolden(); // 황금 트럭 (GDD 6.4)
                ForceGoldenNext = false;
                if (waiting.Count > 0 && CanDock)
                {
                    var bay = waiting[0];
                    waiting.RemoveAt(0);
                    docked++;
                    bay.Receive(truck);
                }
                else
                {
                    var t = truck.transform;
                    t.SetPositionAndRotation(SlotPosition(line.Count), Quaternion.Euler(0f, truckYaw, 0f));
                    t.localScale = Vector3.zero;
                    t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack).SetLink(truck.gameObject);
                    line.Add(truck);
                }
                yield return new WaitForSeconds(config.InboundArrivalInterval);
            }
        }

        /// <summary>빈 하차 칸이 트럭을 달라고 한다. 줄에 있으면 맨 앞을 넘기고(줄은 한 칸씩 당김), 없으면 다음 도착 때 넘긴다.</summary>
        public void Request(InboundBay bay)
        {
            if (line.Count == 0 || !CanDock)
            {
                if (!waiting.Contains(bay)) waiting.Add(bay);
                return;
            }
            Dispatch(bay);
        }

        /// <summary>하차 파트가 다시 열리면 기다리던 하차 칸에 줄 맨 앞부터 넘긴다.</summary>
        void Update()
        {
            if (waiting.Count == 0 || line.Count == 0 || !CanDock) return;
            var bay = waiting[0];
            waiting.RemoveAt(0);
            Dispatch(bay);
        }

        /// <summary>하차장에 넘겨도 되는가: 하차 파트 진행 중이고, 튜토리얼 구간이면 아직 한 대도 안 넘겼을 때 (2026-11-20).</summary>
        bool CanDock => Economy.PartService.InboundOpen && !(docked > 0 && Economy.PartService.TutorialPhase);

        void Dispatch(InboundBay bay)
        {
            docked++;
            var truck = line[0];
            line.RemoveAt(0);
            for (var i = 0; i < line.Count; i++)
            {
                line[i].transform.DOMove(SlotPosition(i), 0.6f).SetEase(Ease.InOutSine).SetLink(line[i].gameObject);
            }
            truck.transform.DOKill();
            truck.transform.DOScale(0f, AppearDuration).SetEase(Ease.InBack).SetLink(truck.gameObject)
                .OnComplete(() => bay.Receive(truck));
        }

        /// <summary>하차 칸이 사라지면(모듈 재배치 등) 기다리는 목록에서 뺀다.</summary>
        public void Cancel(InboundBay bay) => waiting.Remove(bay);

        Vector3 SlotPosition(int index) => headPosition + lineDirection.normalized * (spacing * index);

        /// <summary>트럭 대기 자리 표시 (얇은 판). shortcut: 기본 큐브 — 대기 자리 표시 에셋이 오면 프리팹으로</summary>
        void CreateMarker(int index)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"TruckWaitSlot_{index}";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.SetPositionAndRotation(SlotPosition(index) + Vector3.up * 0.02f, Quaternion.Euler(0f, truckYaw, 0f));
            marker.transform.localScale = new Vector3(2.6f, 0.02f, 6.4f);
            var renderer = marker.GetComponent<Renderer>();
            if (markerMaterial != null) renderer.sharedMaterial = markerMaterial;
            else renderer.enabled = false; // 재질이 없으면 분홍 대신 숨김
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
