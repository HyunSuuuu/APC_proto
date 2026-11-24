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

        /// <summary>붙어 있는 모든 공용 보관소 (재고 집계용). 모듈 안에 있으므로 스스로 등록한다.</summary>
        public static IReadOnlyList<SharedStorage> All => Registered;

        public ReadOnlyReactiveProperty<int> Count => count;
        public int Capacity => capacity;
        public IReadOnlyList<Cargo> Items => pile.Items;
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
                if (cargo.Item == item && cargo.State == CargoState.Normal) n++; // 반품은 재고가 아니다
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
                if (!CanGive(candidate)) continue;
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

        public void Store(Cargo cargo)
        {
            pile.Add(cargo);
            count.Value = pile.Count;
        }

        /// <summary>
        /// 선반에 빈자리가 남은 품목만 꺼낸다 (선반 빈자리 − 이미 들고 가는 수, GDD 7.2 확정 규칙).
        /// 찌그러진 상자는 반품 팔레트에 빈자리가 남았을 때만 (GDD 7.7). 최근에 들어온 것부터 찾는다. 없으면 null.
        /// </summary>
        public Cargo TakeForShelves()
        {
            var i = FindForShelves();
            if (i < 0) return null;

            var cargo = pile.RemoveAt(i);
            cargo.Origin = OriginOnTake(cargo);
            count.Value = pile.Count;
            return cargo;
        }

        CargoOrigin OriginOnTake(Cargo cargo) => cargo.State == CargoState.Trash ? CargoOrigin.Trash : kind == StorageKind.Returns ? CargoOrigin.ReturnPallet : CargoOrigin.Storage;

        /// <summary>TakeForShelves로 꺼낼 화물이 있는가 (꺼내지 않음, 분류 알바용).</summary>
        public bool HasCargoForShelves() => FindForShelves() >= 0;

        /// <summary>
        /// 분류 알바 예약용: 지금 집으면 선반에 갈 화물을 품목별로 최대 max개까지 센다 (꺼내지 않음).
        /// TakeForShelves와 같은 순서(최근 것부터) · 같은 조건.
        /// </summary>
        public Dictionary<ItemDefinition, int> PlanForShelves(int max)
        {
            var plan = new Dictionary<ItemDefinition, int>();
            var total = 0;
            for (var i = pile.Count - 1; i >= 0 && total < max; i--)
            {
                var item = pile.Items[i].Item;
                if (pile.Items[i].State != CargoState.Normal) continue;
                plan.TryGetValue(item, out var taken);
                if (ShelfAllowance(item) - taken <= 0) continue;
                plan[item] = taken + 1;
                total++;
            }
            return plan;
        }

        int FindForShelves()
        {
            for (var i = pile.Count - 1; i >= 0; i--)
            {
                if (CanGive(pile.Items[i])) return i;
            }
            return -1;
        }

        /// <summary>이 화물을 지금 내줘도 되는가 (선반 · 반품 팔레트 · 재포장 입력 여유, 쓰레기는 쓰레기 출력에서만).</summary>
        bool CanGive(Cargo candidate) => candidate.State switch
        {
            CargoState.Normal => ShelfAllowance(candidate.Item) > 0,
            CargoState.Return => kind switch
            {
                StorageKind.Pallet => Allowance(StorageKind.Returns, CargoOrigin.Storage) > 0,
                StorageKind.Returns => Allowance(StorageKind.RepackInput, CargoOrigin.ReturnPallet) > 0,
                _ => false, // 재포장 입력은 다시 주지 않는다
            },
            CargoState.Trash => kind == StorageKind.Trash, // 쓰레기 출력 → 쓰레기장 (M13-11)
            _ => false,
        };

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
            foreach (var who in APC.Interaction.Interactor.All)
            {
                if (who.Stack == null) continue;
                foreach (var cargo in who.Stack.Items)
                {
                    if (cargo.State == CargoState.Return && cargo.Origin == origin) free--;
                }
            }
            return free;
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
