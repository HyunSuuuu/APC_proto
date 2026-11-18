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
    /// 하차 트럭 도착 · 차량 대기열 (GDD 7.1, M13-9). 물류센터 전체에 `inboundArrivalInterval`마다 트럭 1대가 도착한다 (시작 때 1대 바로).
    /// 빈 하차 칸(기다리는 InboundBay)이 있으면 바로 넘기고, 없으면 부지 밖 도로의 대기 자리에 줄을 세운다. 줄이 차면 도착을 멈춘다.
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

        public int Count => line.Count;

        /// <summary>디버그 메뉴: 다음 도착 트럭을 황금 트럭으로.</summary>
        public static bool ForceGoldenNext;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            for (var i = 0; i < config.InboundQueueSize; i++) CreateMarker(i);

            var candidates = new List<CompanyDefinition>();
            while (true)
            {
                // 그 색 보관 구역(선반)이 없으면 그 색 트럭은 안 나온다 (GDD 6.3). 줄이 차면 자리가 날 때까지 멈춤
                yield return new WaitUntil(() =>
                {
                    if (truckPrefab == null || line.Count >= config.InboundQueueSize || !Site.StartSequence.TrucksOpen) return false; // 상차 1을 지어야 시작 (GDD 9.6)
                    candidates.Clear();
                    foreach (var c in ContractService.Instance.Contracted)
                    {
                        if (Warehouse.Instance.HasShelf(c.Items[0])) candidates.Add(c);
                    }
                    return candidates.Count > 0;
                });

                var company = StockBalancer.PickInboundCompany(candidates);
                var truck = Instantiate(truckPrefab, transform);
                truck.Load(company, StockBalancer.ComposeInboundLoad(company));
                if (ForceGoldenNext || Random.value < GameManager.Instance.Database.GoldenTruck.Chance) truck.MakeGolden(); // 황금 트럭 (GDD 6.4)
                ForceGoldenNext = false;
                if (waiting.Count > 0)
                {
                    var bay = waiting[0];
                    waiting.RemoveAt(0);
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
            if (line.Count == 0)
            {
                if (!waiting.Contains(bay)) waiting.Add(bay);
                return;
            }
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
