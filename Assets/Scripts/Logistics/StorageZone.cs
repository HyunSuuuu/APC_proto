using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 하차장 팔레트(공용 보관소) 둘레 존 (GDD 7.2). 근처에서 멈추면 **트럭 화물 내려놓기를 먼저** 하고,
    /// 내려놓을 것이 없으면 선반에 빈자리가 있는 품목만 집는다 (선반 빈자리 − 이미 들고 있는 수).
    /// </summary>
    public sealed class StorageZone : CargoZone
    {
        [SerializeField] SharedStorage storage;

        public SharedStorage Storage => storage;

        protected override bool CanReceive(Cargo cargo, Interactor who) => storage.CanAccept(cargo);

        protected override void Receive(Cargo cargo, Interactor who) => storage.Store(cargo);

        /// <summary>반품 선반에서 하나도 못 넣었을 때 (플레이어만, GDD 5.3): 반품 상자인데 꽉 찼으면 '가득', 그 밖의 상자면 '넣을 수 없음'. 선반에서 꺼낸 상자는 세지 않는다.</summary>
        protected override void OnNothingDropped(Interactor who)
        {
            if (storage.Kind != StorageKind.Returns && storage.Kind != StorageKind.RepackInput) return;
            var returns = false;
            var other = false;
            foreach (var cargo in who.Stack.Items)
            {
                if (cargo.Origin == CargoOrigin.Shelf) continue;
                if (cargo.State == CargoState.Return) returns = true;
                else other = true;
            }
            if (returns) Notice(who, storage.CountLabel, LabelPosition, "가득");
            else if (other) NoticeCannotDrop(who, LabelPosition);
        }

        protected override CargoOrigin GiveOrigin => storage.Kind switch
        {
            StorageKind.Trash => CargoOrigin.Trash,
            StorageKind.Returns => CargoOrigin.ReturnPallet,
            _ => CargoOrigin.Storage,
        };

        protected override bool IgnoresCarryCapacity => storage.Kind == StorageKind.Trash;

        public override bool HasCargoFor(Interactor who) => CanCarryMoreTrash(who) && storage.HasCargoForShelves();

        /// <summary>쓰레기는 도구와 상관없이 최대 `trashCarryMax`(12)개까지 든다 (GDD 7.9). 쓰레기 출력이 아니면 항상 true.</summary>
        bool CanCarryMoreTrash(Interactor who)
        {
            if (storage.Kind != StorageKind.Trash || who.Stack == null) return true;
            var carried = 0;
            foreach (var cargo in who.Stack.Items)
            {
                if (cargo.State == CargoState.Trash) carried++;
            }
            return carried < Core.GameManager.Instance.Database.Logistics.TrashCarryMax;
        }

        protected override bool TryGive(Interactor who, out Cargo cargo)
        {
            cargo = null;
            if (HoldsPick(who)) return false; // 내려놓기 → 스캔 → 집기 (GDD 7.2a)
            cargo = CanCarryMoreTrash(who) ? storage.TakeForShelves() : null;
            // 하차 칸 공간에 상자는 있는데 그 색 선반이 다 차서 하나도 못 집음 → '선반 가득' (플레이어만, GDD 7.3)
            if (cargo == null && storage.Kind == StorageKind.Pallet && storage.Count.CurrentValue > 0)
                Notice(who, storage.CountLabel, LabelPosition, "선반 가득");
            return cargo != null;
        }

        // ── 하차 스캔 (GDD 7.2a, M17-4) — 하차 칸 공간(Pallet)만 ─────────────

        /// <summary>지금까지 끝낸 스캔 수 (모든 하차 칸, 튜토리얼 단계 5-1).</summary>
        public static int ScanCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetScanStatics() => ScanCount = 0;

        Interactor scanner;
        float scanProgress;
        ScanIndicator indicator;

        /// <summary>이 칸을 스캔 중인 사람 (한 칸 한 사람). 없으면 null.</summary>
        public Interactor Scanner => scanner;

        /// <summary>스캔 게이지 0~1.</summary>
        public float ScanProgress => scanProgress;

        bool IsPallet => storage != null && storage.Kind == StorageKind.Pallet;

        /// <summary>하차 알바가 스캔하러 올 만한가: 스캔할 수 있고, 미분류가 `staffScanMinBoxes`개 이상이거나 이 칸 트럭이 비었거나 없다 (18장 55 가정).</summary>
        public bool StaffScanWanted
        {
            get
            {
                if (!IsPallet || !storage.CanScan) return false;
                if (storage.UnsortedCount >= Core.GameManager.Instance.Database.Logistics.StaffScanMinBoxes) return true;
                foreach (var bay in InboundBay.All)
                {
                    if (bay.Storage != storage) continue;
                    return bay.DockedTruck == null || !bay.DockedTruck.HasCargo;
                }
                return true;
            }
        }

        /// <summary>
        /// 집기를 미루는가 (플레이어만): 다른 사람이 이 칸을 스캔 중이면 끝날 때까지 기다리고, 내가 스캔할 수 있으면 스캔부터 한다.
        /// 알바(보관 알바)는 스캔 중에도 이미 스캔된 상자를 집는다.
        /// </summary>
        bool HoldsPick(Interactor who)
        {
            if (!IsPallet || who.RestrictToTarget) return false;
            if (scanner != null) return true;
            return who.AllowScan && storage.CanScan;
        }

        /// <summary>그 사람이 지금 이 칸 스캔을 시작 · 계속할 수 있는가 (멈춘 시간은 따로 본다).</summary>
        bool CanScanBy(Interactor who)
        {
            if (!who.AllowScan || who.CurrentZone != this) return false;
            if (who.RestrictToTarget) return who.Target == this && StaffScanWanted;
            // 플레이어: 내려놓을 것이 남았으면 내려놓기부터
            if (who.Stack != null)
            {
                foreach (var cargo in who.Stack.Items)
                {
                    if (storage.CanAccept(cargo)) return false;
                }
            }
            return storage.CanScan; // 남은 빈자리가 없으면 스캔하지 않는다 — 스캔된 상자 · 찌그러진 상자 집기를 막지 않게
        }

        protected override void Update()
        {
            base.Update();
            if (!IsPallet) return;
            var config = Core.GameManager.Instance.Database.Logistics;

            // 스캔하던 사람이 움직이거나 떠나면 게이지 0 (GDD 7.2a)
            if (scanner != null && (!scanner.isActiveAndEnabled || !scanner.IsStill || !CanScanBy(scanner)))
            {
                scanner = null;
                scanProgress = 0f;
            }
            if (scanner == null)
            {
                foreach (var who in Occupants)
                {
                    if (who.StillTime < config.ScanStartDelay || !CanScanBy(who)) continue;
                    scanner = who;
                    scanProgress = 0f;
                    break;
                }
            }

            // 게이지는 남은 빈자리가 있는 색이 하나라도 있을 때만 찬다
            if (scanner != null && storage.CanScan)
            {
                var duration = scanner.RestrictToTarget ? config.StaffScanDuration : config.PlayerScanDuration;
                scanProgress = Mathf.Min(1f, scanProgress + Time.deltaTime / Mathf.Max(0.05f, duration));
                if (scanProgress >= 1f)
                {
                    storage.RevealUnsorted();
                    ScanCount++;
                    scanner = null;
                    scanProgress = 0f;
                }
            }

            if (scanner != null && indicator == null) indicator = ScanIndicator.Create(transform);
            if (indicator != null) indicator.Show(scanner, scanProgress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            scanner = null;
            scanProgress = 0f;
            if (indicator != null) indicator.Show(null, 0f);
        }

        Vector3 LabelPosition => (storage.CountLabel != null ? storage.CountLabel.transform.position : transform.position) + Vector3.up * 0.5f;
    }
}
