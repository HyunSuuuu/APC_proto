using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>하차 알바가 가서 서는 스캔 자리 (스캔 모듈 팔레트 · 벨트 스캔 구역, GDD 7.2a). StaffWorker가 같은 흐름으로 다룬다.</summary>
    public interface IScanStation
    {
        /// <summary>하차 알바가 와서 스캔할 만한가.</summary>
        bool StaffScanWanted { get; }

        /// <summary>지금 스캔하는 사람 (한 자리 한 사람). 없으면 null.</summary>
        Interactor Scanner { get; }
    }

    /// <summary>
    /// 벨트 스캔 구역 (GDD 7.2a · 7.10, M20-5). 하차 칸 공간 → 스캔 팔레트 벨트의 한 지점(`Distance`) 옆 작업 존.
    /// 미분류 상자는 그 지점에서 멈추고(뒤 상자도 줄지어), 존에 멈춰 선 플레이어 · 하차 알바가 1개씩 `scanSecondsPerBox`(1초) 스캔 →
    /// 색이 정해진 상자만 다시 움직여 팔레트로 간다. 사람이 없거나 정할 색이 없으면 기다린다.
    /// 벨트 자식이라 벨트를 철거하면 같이 없어진다. 위치 · 쪽은 `Place`로 (설치 화면에서 옮기기 · 뒤집기).
    /// </summary>
    public sealed class BeltScanZone : InteractionZone, IScanStation
    {
        static readonly Vector2 ZoneSize = new(1.6f, 1.6f); // `임시`
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Interactor scanner;
        float progress;
        ScanIndicator indicator;
        GameObject marker; // 벨트 위 초록 상자

        public ConveyorBelt Belt { get; private set; }

        /// <summary>벨트 Start에서 스캔 지점까지 경로 거리 (m).</summary>
        public float Distance { get; private set; }

        /// <summary>작업 존이 벨트 진행 방향의 오른쪽(+1) · 왼쪽(−1).</summary>
        public int Side { get; private set; } = 1;

        public Interactor Scanner => scanner;

        public float Progress => progress;

        public override bool WaitsForReadyDelay => false;

        public override void Tick(Interactor who, float deltaTime) { } // 게이지는 Update가 채운다

        /// <summary>벨트 위 distance 지점, side 쪽에 작업 존이 오는 스캔 구역을 만든다 (벨트 자식).</summary>
        public static BeltScanZone Create(ConveyorBelt belt, float distance, int side)
        {
            var go = new GameObject("BeltScanZone");
            go.SetActive(false); // 크기 · 자리를 정한 뒤 켜서 등록
            go.transform.SetParent(belt.transform, false);
            var zone = go.AddComponent<BeltScanZone>();
            zone.Belt = belt;
            zone.SetSize(ZoneSize);
            var slot = new GameObject("WorkSlot");
            slot.transform.SetParent(go.transform, false);
            slot.AddComponent<WorkSlot>();
            zone.marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(zone.marker.GetComponent<Collider>());
            zone.marker.name = "ScanMarker";
            zone.marker.transform.SetParent(belt.transform, false);
            var config = Core.GameManager.Instance.Database.Logistics;
            var renderer = zone.marker.GetComponent<Renderer>();
            if (config.BeltMaterial != null) renderer.sharedMaterial = config.BeltMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(0.25f, 0.85f, 0.35f)); // shortcut: 벨트 재질에 초록만 — 스캔 구역 모델이 오면 교체
            renderer.SetPropertyBlock(block);
            zone.Place(distance, side);
            go.SetActive(true);
            belt.ScanZone = zone;
            return zone;
        }

        /// <summary>존 · 초록 상자를 벨트 distance 지점, side 쪽으로 옮긴다.</summary>
        public void Place(float distance, int side)
        {
            Distance = Mathf.Clamp(distance, 0f, Belt.Length);
            Side = side >= 0 ? 1 : -1;
            Belt.PoseAt(Distance, out var point, out var dir);
            var right = Vector3.Cross(Vector3.up, dir) * Side;
            transform.SetPositionAndRotation(point + right * WorkOffset, Quaternion.LookRotation(-right)); // 작업 지점이 벨트를 본다
            var width = ConveyorBelt.Width;
            var top = Core.GameManager.Instance.Database.Logistics.BeltHeight;
            marker.transform.SetPositionAndRotation(point + Vector3.up * (top + 0.03f), Quaternion.LookRotation(dir)); // 벨트 윗면 위에 얇게 (묻히지 않게)
            marker.transform.localScale = new Vector3(width + 0.1f, 0.06f, 0.5f);
            scanner = null;
            progress = 0f;
        }

        /// <summary>벨트 중심선에서 작업 존 중심까지 (벨트 반 폭 + 존 반 크기).</summary>
        public static float WorkOffset => ConveyorBelt.Width * 0.5f + ZoneSize.y * 0.5f;

        public static Vector2 WorkSize => ZoneSize;

        /// <summary>하차 알바가 올 만한가: 그 벨트에 미분류 상자가 지점으로 오는 중이거나 기다린다 (GDD 7.2a M20-5).</summary>
        public bool StaffScanWanted => Belt != null && Belt.UnsortedBeforeScan > 0 && SharedStorage.AnyScanRoom();

        bool CanScanBy(Interactor who)
        {
            if (!who.isActiveAndEnabled || !who.AllowScan || who.CurrentZone != this) return false;
            return !who.RestrictToTarget || who.Target == this;
        }

        protected override void Update()
        {
            base.Update();
            if (Belt == null) return;
            var config = Core.GameManager.Instance.Database.Logistics;
            if (scanner != null && (!scanner.IsStill || !CanScanBy(scanner)))
            {
                scanner = null;
                progress = 0f;
            }
            if (scanner == null)
            {
                foreach (var who in Occupants)
                {
                    if (who.StillTime < config.ScanStartDelay || !CanScanBy(who)) continue;
                    scanner = who;
                    progress = 0f;
                    break;
                }
            }

            var target = Belt.CargoAtScan();
            if (scanner != null && target != null && SharedStorage.AnyScanRoom())
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / Mathf.Max(0.05f, config.ScanSecondsPerBox));
                if (progress >= 1f)
                {
                    SharedStorage.RevealOne(target); // 색이 정해지면 벨트가 다시 움직인다
                    progress = 0f;
                }
            }
            else if (target == null) progress = 0f;

            if (scanner != null && indicator == null) indicator = ScanIndicator.Create(transform);
            if (indicator != null) indicator.Show(scanner, progress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            scanner = null;
            progress = 0f;
            if (indicator != null) indicator.Show(null, 0f);
        }

        void OnDestroy()
        {
            if (marker != null) Destroy(marker);
        }
    }
}
