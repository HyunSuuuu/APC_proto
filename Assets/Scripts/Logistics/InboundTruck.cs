using System.Collections.Generic;
using APC.Data;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>입고 트럭 1대 (GDD 7.1). 실은 화물 목록과 남은 수량 표시만 가진다. 이동은 InboundBay가 한다.</summary>
    public sealed class InboundTruck : MonoBehaviour
    {
        [SerializeField, Tooltip("화물이 튀어나오는 위치 (픽업 존 쪽 옆면)")] Transform cargoExit;
        [SerializeField] TMP_Text countLabel;

        readonly List<ItemDefinition> load = new();
        readonly ReactiveProperty<int> remaining = new(0);

        public CompanyDefinition Company { get; private set; }
        public bool HasCargo => load.Count > 0;
        public ReadOnlyReactiveProperty<int> Remaining => remaining;

        /// <summary>하차 알바가 집으러 오는 중인 수 (GDD 8.4 예약). 같은 화물을 두고 여러 알바가 오지 않게.</summary>
        public int Reserved { get; private set; }

        /// <summary>아직 아무도 예약하지 않은 화물 수.</summary>
        public int Available => Mathf.Max(0, load.Count - Reserved);

        /// <summary>아직 트럭에 남은 그 품목 수 (상차 주문 색 후보 확인용, GDD 8.1).</summary>
        public int CountOf(ItemDefinition item)
        {
            var n = 0;
            foreach (var loaded in load)
            {
                if (loaded == item) n++;
            }
            return n;
        }

        public void Reserve(int count) => Reserved += count;
        public void Unreserve(int count) => Reserved = Mathf.Max(0, Reserved - count);

        void Awake() => remaining.Subscribe(n => countLabel.text = n.ToString()).AddTo(this);

        void OnDestroy() => remaining.Dispose();

        /// <summary>황금 트럭 (GDD 6.4): 실은 것은 모두 금덩이. 사람이 꺼내지 않고 TruckPickupZone이 존 바닥에 쏟는다.</summary>
        public bool Golden { get; private set; }

        readonly List<Renderer> goldenRenderers = new();
        MaterialPropertyBlock goldenBlock;
        Light goldenLight;

        /// <summary>황금 트럭으로 만든다: 몸체 금색(깜빡임) · 위에 점광원 · 반짝임 파티클. 값은 GoldenTruckConfig (도착할 때 읽음, 색 · 빛 세기는 매 프레임).</summary>
        public void MakeGolden()
        {
            Golden = true;
            var config = Core.GameManager.Instance.Database.GoldenTruck;
            goldenBlock = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TMP_Text>() != null) continue;
                goldenRenderers.Add(r);
            }
            if (config.UseLight)
            {
                goldenLight = new GameObject("GoldenLight").AddComponent<Light>();
                goldenLight.transform.SetParent(transform, false);
                goldenLight.transform.localPosition = config.LightOffset;
                goldenLight.type = LightType.Point;
                goldenLight.color = config.LightColor;
                goldenLight.range = config.LightRange;
                goldenLight.shadows = LightShadows.None;
            }
            if (config.UseSparkle) CreateSparkle(config);
            ApplyGolden(config);
        }

        void Update()
        {
            if (Golden) ApplyGolden(Core.GameManager.Instance.Database.GoldenTruck);
        }

        void ApplyGolden(GoldenTruckConfig config)
        {
            var pulse = config.Pulse(Time.time);
            var color = Color.Lerp(config.BodyColor, config.PulseColor, pulse);
            foreach (var r in goldenRenderers)
            {
                if (r == null) continue;
                goldenBlock.Clear();
                goldenBlock.SetColor("_BaseColor", color);
                if (config.SolidColor) goldenBlock.SetTexture("_BaseMap", Texture2D.whiteTexture);
                r.SetPropertyBlock(goldenBlock);
            }
            if (goldenLight != null) goldenLight.intensity = config.LightIntensity * (1f - config.LightFlicker * (1f - pulse));
        }

        void CreateSparkle(GoldenTruckConfig config)
        {
            var go = new GameObject("GoldenSparkle");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = config.SparkleCenter;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = config.SparkleLifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(config.SparkleSize * 0.5f, config.SparkleSize);
            main.startSpeed = 0f;
            main.startColor = config.SparkleColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;
            var emission = ps.emission;
            emission.rateOverTime = config.SparkleRate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = config.SparkleArea;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(config.SparkleRise, config.SparkleRise);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = ps.sizeOverLifetime; // 커졌다 작아지며 반짝
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = config.SparkleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        public void Load(CompanyDefinition company, IEnumerable<ItemDefinition> items)
        {
            Company = company;
            load.Clear();
            load.AddRange(items);
            remaining.Value = load.Count;
        }

        /// <summary>화물 1개를 꺼내 옆면에 만든다. 받는 쪽(운반 스택)이 날려 보낸다.</summary>
        public Cargo TakeCargo()
        {
            if (!HasCargo) return null;

            var item = load[^1];
            load.RemoveAt(load.Count - 1);
            remaining.Value = load.Count;

            countLabel.transform.DOKill(true);
            countLabel.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 6, 0.5f);

            // 반품 구역이 열린 뒤엔 일정 확률로 찌그러진 상자 (GDD 7.7). 품목(색)은 그대로 — 재포장하면 같은 색 일반 상자
            var isReturn = !Golden && SharedStorage.ReturnsOpen && Random.value < Core.GameManager.Instance.Database.Logistics.ReturnChance;
            var cargo = CargoPool.Instance.Spawn(item, Golden ? CargoState.Gold : isReturn ? CargoState.Return : CargoState.Normal, null, cargoExit.position);
            cargo.Origin = CargoOrigin.Truck;
            return cargo;
        }
    }
}
