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
    /// 대기 줄은 **하차 칸마다 그 칸 앞**(도로 너머)에 하나씩 (2026-11-25 — 멀리 있는 한 줄은 안 보였다). 도착 트럭은 가장 한가한 칸으로:
    /// 비어 기다리는 칸이면 바로 넘기고, 아니면 그 칸 줄 뒤에 선다. 줄 합계가 `inboundQueueSize`이거나 칸마다 몫(합계 ÷ 칸 수)이 차면 도착을 멈춘다.
    /// 하차장에 넘기는 것은 하차 파트(1)와 사무실 전에만 (PartService.InboundOpen, 2026-11-19). 상차 파트 동안 도착한 트럭은 줄에서 기다린다.
    /// 트럭 색은 도착할 때 정한다. SiteGrid 오브젝트에 붙는다 (줄 자리는 월드 좌표, InboundBay.LineSlot).
    /// </summary>
    public sealed class InboundQueue : MonoBehaviour
    {
        const float AppearDuration = 0.25f;

        [SerializeField] InboundTruck truckPrefab;
        [SerializeField, Min(1f), Tooltip("대기 자리 간격 (m)")] float spacing = 7f;
        [SerializeField, Tooltip("대기 자리 표시 재질 (반투명, M_TruckWaitSlot). 빌드에서 Shader.Find는 셰이더가 빠져 null — 에셋으로 둔다")] Material markerMaterial;

        public static InboundQueue Instance { get; private set; }

        readonly Dictionary<InboundBay, List<InboundTruck>> lines = new(); // 하차 칸 → 그 칸 앞 대기 줄 (0 = 맨 앞)
        readonly Dictionary<InboundBay, int> markers = new(); // 하차 칸 → 만든 대기 자리 표시 수
        readonly List<InboundBay> waiting = new();
        int arrived; // 만든 하차 트럭 수 (첫 대 = 튜토리얼 트럭)
        int docked;  // 하차장에 넘긴 트럭 수

        /// <summary>디버그 메뉴: 다음 도착 트럭을 황금 트럭으로.</summary>
        public static bool ForceGoldenNext;

        /// <summary>타이쿤 시험: 도착 간격 없이 줄을 바로 채우고, 한 대 빠지면 바로 보충한다 (TycoonTest가 켬, 이 오브젝트가 사라지면 꺼짐).</summary>
        public static bool FillAtOnce;

        void Awake() => Instance = this;

        List<InboundTruck> LineOf(InboundBay bay)
        {
            if (!lines.TryGetValue(bay, out var line)) lines[bay] = line = new List<InboundTruck>();
            return line;
        }

        int Waiting
        {
            get
            {
                var n = 0;
                foreach (var line in lines.Values) n += line.Count;
                return n;
            }
        }

        /// <summary>줄의 트럭을 모두 회차: 바깥쪽(트럭 앞)으로 빠져나가며 작아진 뒤 사라짐.</summary>
        void SendAwayLine()
        {
            foreach (var line in lines.Values) SendAway(line);
        }

        void SendAway(List<InboundTruck> line)
        {
            foreach (var truck in line)
            {
                var t = truck.transform;
                t.DOKill();
                DOTween.Sequence().SetLink(truck.gameObject)
                    .Append(t.DOMove(t.position + t.forward * spacing * 3f, 0.8f).SetEase(Ease.InCubic))
                    .Join(t.DOScale(0f, 0.8f).SetEase(Ease.InBack))
                    .OnComplete(() => TruckPool.Release(truck));
            }
            line.Clear();
        }

        /// <summary>도착 트럭을 받을 칸: 줄이 몫보다 짧은 칸 중 (줄 + 주차 중 1) 가장 적은 칸 — 비어 기다리는 칸이 먼저. 없으면 null.</summary>
        InboundBay PickBay(int total)
        {
            var bays = InboundBay.All;
            if (bays.Count == 0) return null;
            var perBay = Mathf.Max(1, Mathf.CeilToInt(total / (float)bays.Count));
            InboundBay best = null;
            var bestLoad = int.MaxValue;
            foreach (var bay in bays)
            {
                var count = LineOf(bay).Count;
                if (count >= perBay) continue;
                var load = count + (waiting.Contains(bay) ? 0 : 1);
                if (load >= bestLoad) continue;
                best = bay;
                bestLoad = load;
            }
            return best;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            FillAtOnce = false;
        }

        IEnumerator Start()
        {
            // 상차 파트가 되면 줄에 선 하차 트럭은 바로 돌아간다 (2026-11-20). 하차장에 정차한 트럭은 남음
            if (Economy.PartService.Instance != null) Economy.PartService.Instance.OnShift.Subscribe(_ => { if (!FillAtOnce) SendAwayLine(); }).AddTo(this); // 바로 채우는 모드는 줄 유지 (돌려보내며 다시 채우면 잠깐 80대)
            var config = GameManager.Instance.Database.Logistics;
            // 동시에 나오는 최대 = 줄 합계 + 칸마다 1대(주차 또는 떠나는 중). 트럭 TMP Awake 비용을 시작 때 (풀 최대 64에서 잘림)
            // shortcut: 시작 때 있는 칸만 센다 — 확장으로 칸이 늘면 그만큼은 도중에 만든다
            if (truckPrefab != null) TruckPool.Prewarm(truckPrefab, config.InboundQueueSize + InboundBay.All.Count);

            CompanyDefinition large = null;
            InboundBay target = null;
            var normalOpen = false;
            while (true)
            {
                // 트럭 = 일반(보관 모듈 A~C가 하나라도 있을 때) · 보라(D보관을 지은 뒤, largeTruckChance) · 황금 (GDD 6.3, 2026-11-24 — 색 뽑기 · 3대 연속 삭제)
                yield return new WaitUntil(() =>
                {
                    if (truckPrefab == null || Waiting >= config.InboundQueueSize || !Site.StartSequence.TrucksOpen) return false; // 상차 1을 지어야 시작 (GDD 9.6)
                    target = PickBay(config.InboundQueueSize);
                    if (target == null) return false;
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

                var truck = TruckPool.Get(truckPrefab, transform);
                var tutorialTruck = arrived++ == 0 && !Site.StartSequence.OfficeBuilt; // 튜토리얼 트럭: 상자 TutorialBoxes개, 황금 없음
                var step = GameManager.Instance.Upgrades.TruckStep.CurrentValue;
                if (large != null && (!normalOpen || (!tutorialTruck && Random.value < config.LargeTruckChance)))
                    truck.Load(large, StockBalancer.ComposeInboundLoad(large));
                else
                    truck.LoadNormal(tutorialTruck ? Mathf.Min(Economy.PartService.TutorialBoxes, config.GetNormalTruckCapacity(step)) : config.GetNormalTruckCapacity(step));
                if (!tutorialTruck && (ForceGoldenNext || Random.value < GameManager.Instance.Database.GoldenTruck.Chance)) truck.MakeGolden(); // 황금 트럭 (GDD 6.4)
                ForceGoldenNext = false;

                var line = LineOf(target);
                var t = truck.transform;
                t.localScale = Vector3.zero; // 줄 자리(또는 칸 앞)에서 커지며 나타남
                if (line.Count == 0 && CanDock && waiting.Remove(target))
                {
                    docked++;
                    t.SetPositionAndRotation(target.LineSlot(0, spacing), target.LineRotation);
                    target.Receive(truck);
                }
                else
                {
                    t.SetPositionAndRotation(target.LineSlot(line.Count, spacing), target.LineRotation);
                    t.DOScale(1f, AppearDuration).SetEase(Ease.OutBack).SetLink(truck.gameObject);
                    line.Add(truck);
                    EnsureMarkers(target, line.Count);
                }
                if (FillAtOnce) yield return null; // 한 프레임에 한 대 (풀에서 꺼내므로 끊김 없음)
                else yield return new WaitForSeconds(config.InboundArrivalInterval);
            }
        }

        /// <summary>빈 하차 칸이 트럭을 달라고 한다. 그 칸 줄에 있으면 맨 앞을 넘기고(줄은 한 칸씩 당김), 없으면 다음 도착 때 넘긴다.</summary>
        public void Request(InboundBay bay)
        {
            if (LineOf(bay).Count == 0 || !CanDock)
            {
                if (!waiting.Contains(bay)) waiting.Add(bay);
                return;
            }
            Dispatch(bay);
        }

        /// <summary>하차 파트가 다시 열리면 기다리던 하차 칸에 그 칸 줄 맨 앞을 넘긴다.</summary>
        void Update()
        {
            if (waiting.Count == 0 || !CanDock) return;
            for (var i = waiting.Count - 1; i >= 0; i--)
            {
                var bay = waiting[i];
                if (LineOf(bay).Count == 0) continue;
                waiting.RemoveAt(i);
                Dispatch(bay);
            }
        }

        /// <summary>하차장에 넘겨도 되는가: 하차 파트 진행 중이고, 튜토리얼 구간이면 아직 한 대도 안 넘겼을 때 (2026-11-20).</summary>
        bool CanDock => Economy.PartService.InboundOpen && !(docked > 0 && Economy.PartService.TutorialPhase);

        void Dispatch(InboundBay bay)
        {
            docked++;
            var line = lines[bay];
            var truck = line[0];
            line.RemoveAt(0);
            for (var i = 0; i < line.Count; i++)
            {
                line[i].transform.DOMove(bay.LineSlot(i, spacing), 0.6f).SetEase(Ease.InOutSine).SetLink(line[i].gameObject);
            }
            bay.Receive(truck); // 줄 맨 앞에서 그대로 후진해 들어간다 (InboundBay)
        }

        /// <summary>하차 칸이 사라지면(모듈 재배치 등) 기다리는 목록에서 빼고 그 칸 줄은 돌려보낸다.</summary>
        public void Cancel(InboundBay bay)
        {
            waiting.Remove(bay);
            if (lines.Remove(bay, out var line)) SendAway(line);
        }

        /// <summary>그 칸 대기 자리 표시를 count개까지 만든다 (얇은 판). shortcut: 기본 큐브 — 대기 자리 표시 에셋이 오면 프리팹으로</summary>
        void EnsureMarkers(InboundBay bay, int count)
        {
            markers.TryGetValue(bay, out var made);
            for (var i = made; i < count; i++) CreateMarker(bay, i);
            if (count > made) markers[bay] = count;
        }

        void CreateMarker(InboundBay bay, int index)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"TruckWaitSlot_{bay.name}_{index}";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.SetPositionAndRotation(bay.LineSlot(index, spacing) + Vector3.up * 0.02f, bay.LineRotation);
            marker.transform.localScale = new Vector3(2.6f, 0.02f, 6.4f);
            var renderer = marker.GetComponent<Renderer>();
            if (markerMaterial != null) renderer.sharedMaterial = markerMaterial;
            else renderer.enabled = false; // 재질이 없으면 분홍 대신 숨김
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
