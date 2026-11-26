using System.Collections.Generic;
using APC.Core;
using APC.Data;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>보관 더미의 쓰임 (GDD 7.2 · 7.7).</summary>
    public enum StorageKind
    {
        /// <summary>하차장 팔레트: 트럭에서 꺼낸 일반 화물만 받는다.</summary>
        Pallet,

        /// <summary>반품 팔레트: 하차 칸 공간에서 꺼낸 찌그러진 상자만 받고, 재포장 테이블 입력에 빈자리가 있는 만큼 준다 (Origin = ReturnPallet).</summary>
        Returns = 1,

        /// <summary>재포장 상자 팔레트 (테이블 맞은편): 재포장 테이블만 넣고, 선반 빈자리만큼 준다 (팔레트와 같은 규칙).</summary>
        Repacked = 2,

        /// <summary>재포장 테이블 입력: 반품 팔레트에서 꺼낸 찌그러진 상자만 받고 주지 않는다 (재포장 테이블이 꺼냄).</summary>
        RepackInput = 4, // 3 = 옛 InspectOutput (2026-11-17 삭제) — 프리팹에 저장된 숫자를 지키려고 비워 둠

        /// <summary>재포장 테이블 쓰레기 출력: 재포장만 넣는다. 쓰레기를 주고(Origin = Trash) 쓰레기장(`TrashDropZone`)에 버린다.</summary>
        Trash = 5,

        /// <summary>스캔 모듈 팔레트 (2026-11-24): 하차 칸 공간에서 온 미분류 상자를 받고, 여기서 스캔해 색이 정해진 상자를 선반 · 반품 팔레트로 준다.</summary>
        Scan = 6,
    }

    /// <summary>
    /// 공용 보관소 본체 (GDD 7.2). 트럭에서 꺼낸 화물만 받고, 품목을 섞어 바닥 팔레트에 쌓는다.
    /// 존은 따로 있다 — 내려놓기 존(트럭 쪽) / 집기 존(선반 쪽).
    /// 반품 구역의 롤테이너 · 재포장 출구도 같은 더미를 쓴다 (`kind`).
    /// </summary>
    public sealed class SharedStorage : MonoBehaviour, IBeltStart, IBeltEnd
    {
        [SerializeField] StorageKind kind = StorageKind.Pallet;
        [SerializeField] Transform pileRoot;
        [SerializeField, Tooltip("하차 칸 = 2 × 2 한 층(4개)씩 위로 쌓기 (2026-11-17)")] CargoGridLayout layout = new(2, 2, 0.6f, 0.5f);
        [SerializeField] TMP_Text countLabel;

        static readonly List<SharedStorage> Registered = new();

        readonly ReactiveProperty<int> count = new(0);
        CargoPile pile;
        int capacity;

        // 분류 알바가 집으러 오는 중인 스캔된 상자 수 (품목별, 2026-11-26). 다른 알바 · 플레이어는 이만큼 덜 본다 — 상자 3개에 여러 명이 몰려가던 것
        readonly Dictionary<ItemDefinition, int> plannedBoxes = new();
        static readonly Dictionary<ItemDefinition, int> SkipScratch = new(); // 메인 스레드 · 재진입 없음

        /// <summary>붙어 있는 모든 공용 보관소 (재고 집계용). 모듈 안에 있으므로 스스로 등록한다.</summary>
        public static List<SharedStorage> All => Registered; // 읽기만 한다 — 구체 타입은 foreach 박싱 방지 (2026-11-26)

        public ReadOnlyReactiveProperty<int> Count => count;
        public int Capacity => capacity;
        public List<Cargo> Items => pile.Items; // 읽기만 한다 — 구체 타입은 foreach 박싱 방지 (2026-11-26)
        public StorageKind Kind => kind;

        /// <summary>수량 글자 (떠오르는 알림 글자의 폰트 · 모양 원본으로도 쓴다).</summary>
        public TMP_Text CountLabel => countLabel;
        public bool HasSpace => pile.Count < capacity;

        /// <summary>반품 구역(롤테이너)이 열렸는가 — 열려야 입고 화물에 반품이 섞인다 (GDD 7.7).</summary>
        public static bool ReturnsOpen
        {
            get
            {
                foreach (var storage in Registered)
                {
                    if (storage.kind == StorageKind.Returns) return true;
                }
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        void Awake() => pile = new CargoPile(pileRoot, layout.GetLocalPosition);

        // 쌓이는 자리 (첫 두 층, 실행 중엔 용량만큼)
        void OnDrawGizmos()
        {
            var n = Application.isPlaying && capacity > 0 ? capacity : layout.columns * layout.rows * 2;
            CargoGizmos.DrawSlots(pileRoot != null ? pileRoot : transform, layout.GetLocalPosition, n, new Color(1f, 0.8f, 0.2f), CargoGizmos.BoxSize);
        }

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);

        /// <summary>그 품목 보관 수.</summary>
        public int CountOf(ItemDefinition item)
        {
            var n = 0;
            foreach (var cargo in pile.Items)
            {
                if (cargo.Item == item && cargo.State == CargoState.Normal && !cargo.Unsorted) n++; // 반품 · 미분류는 그 색 재고가 아니다
            }
            return n;
        }

        void Start()
        {
            var config = GameManager.Instance.Database.Logistics;
            capacity = kind switch
            {
                StorageKind.Returns => config.ReturnBinCapacity,
                StorageKind.Repacked => config.RepackOutputCapacity,
                StorageKind.RepackInput => config.RepackInputCapacity,
                StorageKind.Trash => config.TrashOutputCapacity,
                StorageKind.Scan => config.ScanPalletCapacity,
                _ => config.SharedStorageCapacity,
            };
            count.Subscribe(n => countLabel.text = $"{n}/{capacity}").AddTo(this);
        }

        void OnDestroy() => count.Dispose();

        /// <summary>
        /// 빈자리가 있고: 하차 칸 공간 = **이 하차 칸 트럭**에서 꺼낸 화물 (찌그러진 상자 포함, 하차 칸마다 따로, GDD 7.2 · 7.7),
        /// 반품 팔레트 = 하차 칸 공간에서 꺼낸 찌그러진 상자 (트럭 직행 금지), 재포장 입력 = 반품 팔레트에서 꺼낸 찌그러진 상자.
        /// 재포장 상자 팔레트 · 쓰레기 출력은 사람에게서 받지 않는다 (재포장 테이블이 Store).
        /// </summary>
        public bool CanAccept(Cargo cargo)
        {
            if (pile.Count >= capacity) return false;
            return kind switch
            {
                StorageKind.Pallet => cargo.Origin == CargoOrigin.Truck && cargo.Home == this,
                StorageKind.Scan => cargo.Origin == CargoOrigin.Unscanned, // 미분류 · 벨트 스캔 구역에서 색이 정해진 상자 (M20-5)
                StorageKind.Returns => cargo.State == CargoState.Return && cargo.Origin == CargoOrigin.Storage,
                StorageKind.RepackInput => cargo.State == CargoState.Return && cargo.Origin == CargoOrigin.ReturnPallet,
                _ => false,
            };
        }

        /// <summary>그 상태의 상자 1개를 꺼낸다 (재포장 테이블 = 찌그러진 상자). 없으면 null.</summary>
        public Cargo Take(CargoState state)
        {
            for (var i = pile.Count - 1; i >= 0; i--)
            {
                if (pile.Items[i].State != state) continue;
                var cargo = pile.RemoveAt(i);
                count.Value = pile.Count;
                return cargo;
            }
            return null;
        }

        public bool Has(CargoState state)
        {
            foreach (var cargo in pile.Items)
            {
                if (cargo.State == state) return true;
            }
            return false;
        }

        /// <summary>벨트 End: 받는 종류이고, 이 더미로 벨트에서 오는 수를 빼도 자리가 남음.</summary>
        public bool Wants(Cargo cargo)
            => CanAccept(cargo) && capacity - pile.Count - ConveyorBelt.CountOnBelts(b => ReferenceEquals(b.End, this), _ => true) > 0;

        public bool CanTakeFromBelt(Cargo cargo) => CanAccept(cargo);

        /// <summary>
        /// 벨트 Start (GDD 7.10): 사람이 집는 것과 같은 조건(FindForShelves — 선반 · 반품 여유)인 화물 중 End가 원하는 것을 최근 것부터.
        /// 하차 칸 공간 → 선반 · 반품 팔레트, 반품 팔레트 → 재포장 입력, 쓰레기 출력 → 쓰레기장, 재포장 팔레트 → 선반.
        /// </summary>
        public Cargo TakeForBelt(IBeltEnd end)
        {
            for (var i = pile.Count - 1; i >= 0; i--)
            {
                var candidate = pile.Items[i];
                if (!CanGive(candidate) || !ConveyorBelt.Settled(candidate)) continue;
                var before = candidate.Origin;
                candidate.Origin = OriginOnTake(candidate); // End는 출처로 받을지 정한다
                var wanted = end.Wants(candidate);
                candidate.Origin = before;
                if (!wanted) continue;
                var cargo = pile.RemoveAt(i);
                cargo.Origin = OriginOnTake(cargo);
                count.Value = pile.Count;
                return cargo;
            }
            return null;
        }

        public void ReceiveFromBelt(Cargo cargo) => Store(cargo);

        /// <summary>벨트 철거 · 회차: 이 더미 출처로 되돌려 쌓는다 (넘침 허용). 하차 칸 공간은 트럭 화물로, 반품 팔레트는 공간에서 온 것으로.</summary>
        public void ReturnFromBelt(Cargo cargo)
        {
            switch (kind)
            {
                case StorageKind.Pallet:
                    cargo.Origin = CargoOrigin.Truck;
                    cargo.Home = this;
                    break;
                case StorageKind.Returns:
                    cargo.Origin = CargoOrigin.Storage;
                    break;
                case StorageKind.RepackInput:
                    cargo.Origin = CargoOrigin.ReturnPallet;
                    break;
            }
            Store(cargo);
        }

        public void Store(Cargo cargo)
        {
            pile.Add(cargo);
            count.Value = pile.Count;
        }

        /// <summary>
        /// 선반에 빈자리가 남은 품목만 꺼낸다 (선반 빈자리 − 이미 들고 가는 수, GDD 7.2 확정 규칙).
        /// 찌그러진 상자는 반품 팔레트에 빈자리가 남았을 때만 (GDD 7.7). 최근에 들어온 것부터 찾는다. 없으면 null.
        /// </summary>
        /// <param name="who">집는 사람 — 그 사람이 at 존에 잡아 둔 예약은 빼지 않는다 (2026-11-26). 벨트 · 플레이어 확인은 null.</param>
        public Cargo TakeForShelves(APC.Interaction.Interactor who = null, APC.Interaction.InteractionZone at = null)
        {
            var i = FindForShelves(who, at);
            if (i < 0) return null;

            var cargo = pile.RemoveAt(i);
            cargo.Origin = OriginOnTake(cargo);
            count.Value = pile.Count;
            return cargo;
        }

        CargoOrigin OriginOnTake(Cargo cargo) => cargo.Unsorted ? CargoOrigin.Unscanned
            : cargo.State == CargoState.Trash ? CargoOrigin.Trash : kind == StorageKind.Returns ? CargoOrigin.ReturnPallet : CargoOrigin.Storage;

        /// <summary>TakeForShelves로 꺼낼 화물이 있는가 (꺼내지 않음, 분류 알바용).</summary>
        public bool HasCargoForShelves(APC.Interaction.Interactor who = null, APC.Interaction.InteractionZone at = null) => FindForShelves(who, at) >= 0;

        /// <summary>
        /// 분류 알바 예약용: 지금 집으면 선반에 갈 화물을 품목별로 최대 max개까지 센다 (꺼내지 않음).
        /// TakeForShelves와 같은 순서(최근 것부터) · 같은 조건. 결과는 plan에 비우고 채운다 (알바가 재사용 — 판단마다 new 방지, 2026-11-26). 합계를 돌려준다.
        /// </summary>
        public int PlanForShelves(int max, Dictionary<ItemDefinition, int> plan)
        {
            plan.Clear();
            FillSkip(null, null);
            var total = 0;
            for (var i = pile.Count - 1; i >= 0 && total < max; i--)
            {
                var item = pile.Items[i].Item;
                if (pile.Items[i].State != CargoState.Normal || pile.Items[i].Unsorted) continue; // 미분류는 스캔해야 집는다 (GDD 7.2a)
                if (SkipPlanned(item)) continue; // 다른 분류 알바가 이미 계획한 상자
                plan.TryGetValue(item, out var taken);
                if (ShelfAllowance(item) - taken <= 0) continue;
                plan[item] = taken + 1;
                total++;
            }
            return total;
        }

        int FindForShelves(APC.Interaction.Interactor who, APC.Interaction.InteractionZone at)
        {
            FillSkip(who, at);
            for (var i = pile.Count - 1; i >= 0; i--)
            {
                var candidate = pile.Items[i];
                if (candidate.State == CargoState.Normal && !candidate.Unsorted && SkipPlanned(candidate.Item)) continue;
                if (CanGive(candidate, who, at)) return i;
            }
            return -1;
        }

        /// <summary>분류 알바가 이 더미에서 집을 상자를 예약한다 (출발할 때, 품목별).</summary>
        public void ReserveBoxes(ItemDefinition item, int count)
        {
            plannedBoxes.TryGetValue(item, out var n);
            plannedBoxes[item] = n + count;
        }

        public void UnreserveBoxes(ItemDefinition item, int count)
        {
            if (!plannedBoxes.TryGetValue(item, out var n)) return;
            if (n - count > 0) plannedBoxes[item] = n - count;
            else plannedBoxes.Remove(item);
        }

        /// <summary>품목별로 건너뛸 수 = 계획된 상자 − 집는 사람 자신의 예약 (같은 품목 상자는 서로 바꿔도 되므로 위에서부터 그만큼 남의 몫으로 본다).</summary>
        void FillSkip(APC.Interaction.Interactor who, APC.Interaction.InteractionZone at)
        {
            SkipScratch.Clear();
            foreach (var pair in plannedBoxes)
            {
                var skip = pair.Value - (who != null ? who.Reserved(at, pair.Key) : 0);
                if (skip > 0) SkipScratch[pair.Key] = skip;
            }
        }

        static bool SkipPlanned(ItemDefinition item)
        {
            if (item == null || !SkipScratch.TryGetValue(item, out var skip)) return false;
            if (skip > 1) SkipScratch[item] = skip - 1;
            else SkipScratch.Remove(item);
            return true;
        }

        // ── 하차 스캔 (GDD 7.2a, M17-4) ─────────────────────────────

        /// <summary>미분류 상자 수.</summary>
        public int UnsortedCount
        {
            get
            {
                var n = 0;
                foreach (var cargo in pile.Items)
                {
                    if (cargo.Unsorted) n++;
                }
                return n;
            }
        }

        /// <summary>스캔해서 색을 정할 수 있는가: 미분류가 있고, 남은 빈자리가 있는 보관 색이 하나라도 있다.</summary>
        public bool CanScan => UnsortedCount > 0 && AnyScanRoom();

        /// <summary>스캔 결과로 나올 수 있는 색 = 지어진 보관 모듈 색 (A~C, D 가구 제외).</summary>
        static void CollectScanColors(List<CompanyDefinition> into)
        {
            into.Clear();
            var warehouse = Warehouse.Instance;
            if (warehouse == null) return;
            foreach (var company in GameManager.Instance.Database.Companies)
            {
                if (company == null || company.IsLarge || company.Items.Count == 0) continue;
                if (warehouse.HasShelf(company.Items[0])) into.Add(company);
            }
        }

        static readonly List<CompanyDefinition> ScanColors = new();

        /// <summary>남은 빈자리가 있는 보관 색이 하나라도 있나 (스캔 게이지가 찰 수 있나).</summary>
        public static bool AnyScanRoom()
        {
            CollectScanColors(ScanColors);
            foreach (var company in ScanColors)
            {
                if (ScanRoom(company.Items[0]) > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 그 색으로 더 스캔해 줄 수 있는 수 = 선반 빈자리 − 하차 칸 공간에 그 색으로 스캔돼 있는 상자 − 들고 가는 상자 (선반행 · 회차 되돌릴 상차용).
        /// 보관 알바 예약은 공간에 있는 상자를 예약한 것이라 따로 빼지 않는다 (두 번 빠지지 않게).
        /// </summary>
        public static int ScanRoom(ItemDefinition item)
        {
            var warehouse = Warehouse.Instance;
            if (warehouse == null) return 0;
            var room = warehouse.GetShelfFreeSpace(item) - Warehouse.CountCarried(item, CargoOrigin.Storage) - Warehouse.CountCarried(item, CargoOrigin.Shelf);
            foreach (var storage in Registered)
            {
                if (storage.kind == StorageKind.Scan) room -= storage.CountOf(item); // 스캔 팔레트에 그 색으로 스캔돼 기다리는 상자
            }
            // 벨트 스캔 구역에서 색이 정해져 스캔 팔레트로 가는 중인 상자 (M20-5)
            room -= ConveyorBelt.CountOnBelts(b => b.End is SharedStorage { kind: StorageKind.Scan }, c => !c.Unsorted && c.Item == item && c.State == CargoState.Normal);
            return room;
        }

        /// <summary>
        /// 벨트 스캔 구역 (GDD 7.2a M20-5): 상자 1개의 색을 정한다 — `RevealUnsorted`와 같은 규칙(찌그러짐 판정 → 남은 빈자리 ≥ 1인 색 가중치).
        /// 정할 색이 없으면 false (상자는 미분류로 남아 기다린다).
        /// </summary>
        public static bool RevealOne(Cargo cargo)
        {
            if (!cargo.Unsorted) return false;
            var config = GameManager.Instance.Database.Logistics;
            CollectScanColors(ScanColors);
            var room = new Dictionary<CompanyDefinition, int>();
            var total = 0f;
            foreach (var company in ScanColors)
            {
                room[company] = ScanRoom(company.Items[0]);
                if (room[company] > 0) total += company.Weight;
            }
            if (total <= 0f) return false;
            if (ReturnsOpen && Random.value < config.ReturnChance)
            {
                cargo.Reveal(PickColor(ScanColors, null).Items[0], CargoState.Return, 0f);
                return true;
            }
            cargo.Reveal(PickColor(ScanColors, room).Items[0], CargoState.Normal, 0f);
            return true;
        }

        /// <summary>
        /// 스캔 완료 (GDD 7.2a): 지금 공간에 있는 미분류 상자 전부의 색을 정한다. 상자마다 반품 구역이 있으면 찌그러짐 판정 →
        /// 아니면 남은 빈자리 ≥ 1인 지어진 보관 색 중에서 색 가중치(6.2)로. 남은 빈자리가 모두 0이면 나머지는 미분류로 남는다.
        /// 겉모습은 상자마다 `scanRevealInterval` 간격으로 바뀐다. 정한 수를 돌려준다.
        /// </summary>
        public int RevealUnsorted()
        {
            var config = GameManager.Instance.Database.Logistics;
            CollectScanColors(ScanColors);
            var room = new Dictionary<CompanyDefinition, int>();
            foreach (var company in ScanColors) room[company] = ScanRoom(company.Items[0]);
            var returnsOpen = ReturnsOpen;
            var revealed = 0;
            foreach (var cargo in pile.Items)
            {
                if (!cargo.Unsorted) continue;
                var total = 0f;
                foreach (var company in ScanColors)
                {
                    if (room[company] > 0) total += company.Weight;
                }
                if (total <= 0f) break; // 넣을 선반이 없음 → 남은 상자는 미분류
                var delay = revealed * config.ScanRevealInterval;
                if (returnsOpen && Random.value < config.ReturnChance)
                {
                    // 찌그러진 상자: 색은 지어진 색 가중치로 (선반 빈자리와 무관 — 반품 팔레트로 간다)
                    cargo.Reveal(PickColor(ScanColors, null).Items[0], CargoState.Return, delay);
                    revealed++;
                    continue;
                }
                var picked = PickColor(ScanColors, room);
                room[picked]--;
                cargo.Reveal(picked.Items[0], CargoState.Normal, delay);
                revealed++;
            }
            return revealed;
        }

        /// <summary>색 가중치 비율로 무작위. room이 있으면 남은 빈자리 ≥ 1인 색만.</summary>
        static CompanyDefinition PickColor(List<CompanyDefinition> colors, Dictionary<CompanyDefinition, int> room)
        {
            var total = 0f;
            foreach (var company in colors)
            {
                if (room == null || room[company] > 0) total += company.Weight;
            }
            var roll = Random.value * total;
            CompanyDefinition last = null;
            foreach (var company in colors)
            {
                if (room != null && room[company] <= 0) continue;
                last = company;
                roll -= company.Weight;
                if (roll < 0f) return company;
            }
            return last;
        }

        /// <summary>
        /// 이 화물을 지금 내줘도 되는가. 하차 칸 공간 = 미분류 상자만, 스캔 팔레트에 자리가 있을 때 (스캔은 스캔 모듈에서만, 2026-11-24).
        /// 그 밖 = 스캔된 상자만 (선반 · 반품 팔레트 · 재포장 입력 여유, 쓰레기는 쓰레기 출력에서만).
        /// </summary>
        bool CanGive(Cargo candidate, APC.Interaction.Interactor who = null, APC.Interaction.InteractionZone at = null) => kind == StorageKind.Pallet
            ? candidate.Unsorted && ScanAllowance() > 0
            : !candidate.Unsorted && candidate.State switch
        {
            CargoState.Normal => ShelfAllowance(candidate.Item) + (who != null ? who.Reserved(at, candidate.Item) : 0) > 0,
            CargoState.Return => kind switch
            {
                StorageKind.Pallet => Allowance(StorageKind.Returns, CargoOrigin.Storage) > 0,
                StorageKind.Returns => Allowance(StorageKind.RepackInput, CargoOrigin.ReturnPallet) > 0,
                _ => false, // 재포장 입력은 다시 주지 않는다
            },
            CargoState.Trash => kind == StorageKind.Trash, // 쓰레기 출력 → 쓰레기장 (M13-11)
            _ => false,
        };

        /// <summary>스캔 팔레트에 더 보낼 수 있는 미분류 상자 수 = 스캔 팔레트 빈자리 합 − 들고 가는 · 벨트 위 미분류.</summary>
        public static int ScanAllowance()
        {
            var free = 0;
            foreach (var storage in Registered)
            {
                if (storage.kind == StorageKind.Scan) free += storage.capacity - storage.pile.Count;
            }
            if (free <= 0) return 0;
            free -= ConveyorBelt.CountOnBelts(_ => true, c => c.Unsorted);
            return free - CarriedIndex.Unsorted;
        }

        /// <summary>그 종류 더미에 더 보낼 수 있는 찌그러진 상자 수 = 빈자리 합 − 이미 꺼내 들고 가는(그 출처) 찌그러진 상자.</summary>
        static int Allowance(StorageKind target, CargoOrigin origin)
        {
            var free = 0;
            foreach (var storage in Registered)
            {
                if (storage.kind == target) free += storage.capacity - storage.pile.Count;
            }
            if (free <= 0) return 0;
            free -= ConveyorBelt.CountOnBelts(_ => true, c => c.State == CargoState.Return && c.Origin == origin); // 벨트로 가는 중 (GDD 7.10)
            return free - CarriedIndex.Returns(origin);
        }

        /// <summary>
        /// 그 품목을 선반에 더 보낼 수 있는 수 = 선반 빈자리 − 이미 들고 가는 수 − 분류 알바 예약
        /// − 선반에서 꺼내 들고 있는 상차용 상자 수(회차하면 되돌릴 자리, M17-1).
        /// </summary>
        public static int ShelfAllowance(ItemDefinition item)
        {
            var warehouse = Warehouse.Instance;
            return warehouse.GetShelfFreeSpace(item) - Warehouse.CountCarried(item, CargoOrigin.Storage) - Warehouse.CountCarried(item, CargoOrigin.Shelf) - warehouse.GetReservedShelfSpace(item);
        }
    }
}
