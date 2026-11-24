using System.Collections.Generic;
using APC.Data;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 품목 1종 전용 분류 선반 (GDD 7.3). 정사각형이고, 칸에 실제 상자가 올라가며 위에 수량(7/10)을 표시한다.
    /// 둘레의 ShelfZone 하나로 넣고 꺼낸다.
    /// </summary>
    public sealed class Shelf : MonoBehaviour, IBeltStart, IBeltEnd
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform boxRoot;
        [SerializeField, Tooltip("선반 위 품목 색 표시")] Renderer iconRenderer;
        [SerializeField] TMP_Text countLabel;

        [Header("칸 배치 (선반 로컬, 한 단 = 가로 boxesPerSide × 세로 boxRows)")]
        [SerializeField, Min(1)] int boxesPerSide = 3;
        [SerializeField, Min(0), Tooltip("한 단의 세로(z) 줄 수. 0이면 boxesPerSide (정사각형)")] int boxRows;
        [SerializeField, Min(0.1f)] float boxSpacing = 0.52f;
        [SerializeField] float firstLevelHeight = 0.13f;
        [SerializeField, Min(0.1f)] float levelHeight = 0.8f;

        readonly ReactiveProperty<int> count = new(0);
        CargoPile pile;
        int capacity;

        public ItemDefinition Item { get; private set; }
        public int Capacity => capacity;
        public ReadOnlyReactiveProperty<int> Count => count;
        public bool HasSpace => pile.Count < capacity;
        public int FreeSpace => Mathf.Max(0, capacity - pile.Count);

        /// <summary>수량 글자 (떠오르는 알림 글자의 원본으로도 쓴다).</summary>
        public TMP_Text CountLabel => countLabel;

        void Awake() => pile = new CargoPile(boxRoot, SlotPosition);

        void OnDestroy() => count.Dispose();

        /// <summary>1단계 용량 (선반 6 · 팔레트 랙 4). 단계 용량은 Warehouse가 이것으로 계산한다.</summary>
        public int BaseCapacity { get; private set; }

        /// <summary>선반 단계 (GDD 7.3): 용량만 바뀌고 모양 · 상자는 그대로.</summary>
        public void SetCapacity(int value)
        {
            capacity = value;
            countLabel.text = $"{count.Value}/{capacity}";
        }

        public void Setup(ItemDefinition item, int shelfCapacity)
        {
            Item = item;
            capacity = shelfCapacity;
            BaseCapacity = shelfCapacity;

            var block = new MaterialPropertyBlock();
            iconRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, item.TapeColor);
            iconRenderer.SetPropertyBlock(block);

            count.Subscribe(n => countLabel.text = $"{n}/{capacity}").AddTo(this);
        }

        public void Store(Cargo cargo)
        {
            pile.Add(cargo);
            count.Value = pile.Count;
        }

        /// <summary>맨 마지막에 넣은 상자를 꺼낸다 (M4 수거 존).</summary>
        public Cargo TakeOne()
        {
            if (pile.Count == 0) return null;
            var cargo = pile.RemoveAt(pile.Count - 1);
            count.Value = pile.Count;
            return cargo;
        }

        /// <summary>같은 보관 구역(모듈)의 같은 품목 선반들 — 벨트는 닿은 선반 먼저, 나머지로 짧게 점프 (GDD 7.10).</summary>
        List<Shelf> Group()
        {
            var module = GetComponentInParent<Site.SiteModule>();
            var group = new List<Shelf> { this };
            foreach (var shelf in Warehouse.Instance.Shelves)
            {
                if (shelf != this && shelf.Item == Item && shelf.GetComponentInParent<Site.SiteModule>() == module) group.Add(shelf);
            }
            return group;
        }

        // 벨트는 D 가구(팔레트 랙)를 다루지 않는다 (GDD 7.10)
        bool BeltAllowed => Item != null && !Warehouse.IsLarge(Item);

        /// <summary>벨트 End: 이 색 일반 상자(하차 칸 공간 · 재포장 팔레트에서), 구역 빈자리 − 이 구역으로 벨트에서 오는 수 > 0.</summary>
        public bool Wants(Cargo cargo)
        {
            if (!BeltAllowed || cargo.Item != Item || cargo.State != CargoState.Normal || cargo.Origin != CargoOrigin.Storage) return false;
            var group = Group();
            var free = 0;
            foreach (var shelf in group) free += shelf.FreeSpace;
            return free - ConveyorBelt.CountOnBelts(b => b.End is Shelf s && group.Contains(s), c => c.Origin == CargoOrigin.Storage) > 0;
        }

        public bool CanTakeFromBelt(Cargo cargo) => Group().Exists(s => s.HasSpace);

        /// <summary>닿은 선반에 자리가 없으면 같은 구역 다른 선반으로 (CargoPile이 거기까지 점프시킴). 회차로 돌아오는 상차용 상자도 여기로.</summary>
        public void ReceiveFromBelt(Cargo cargo)
        {
            var target = Group().Find(s => s.HasSpace) ?? this;
            cargo.Origin = CargoOrigin.Shelf;
            target.Store(cargo);
            if (cargo.Repacked) Warehouse.Instance.NotifyRepackedShelved();
        }

        /// <summary>벨트 Start: 상차 존으로만, 상차 시간 · 주문 수요가 있을 때 구역 선반에서 하나 (닿은 선반 먼저).</summary>
        public Cargo TakeForBelt(IBeltEnd end)
        {
            var orders = Orders.OrderService.Instance;
            if (!BeltAllowed || end is not TruckLoadZone || !Economy.PartService.OutboundOpen || orders == null || orders.GetDemand(Item) <= 0) return null;
            var from = Group().Find(s => s.pile.Count > 0);
            if (from == null) return null;
            var cargo = from.TakeOne();
            if (end.Wants(cargo)) return cargo;
            from.Store(cargo); // shortcut: 꺼내 본 뒤 되돌림 — 수요는 있는데 그 트럭 주문이 아닐 때만 생김
            return null;
        }

        // 상자 칸 (1 · 2단, 실행 중엔 용량만큼)
        void OnDrawGizmos()
        {
            var rows = boxRows > 0 ? boxRows : boxesPerSide;
            var n = Application.isPlaying && capacity > 0 ? capacity : boxesPerSide * rows * 2;
            CargoGizmos.DrawSlots(boxRoot != null ? boxRoot : transform, SlotPosition, n, new Color(0.3f, 0.8f, 1f), CargoGizmos.BoxSize);
        }

        Vector3 SlotPosition(int index)
        {
            var rows = boxRows > 0 ? boxRows : boxesPerSide;
            var perLevel = boxesPerSide * rows;
            var level = index / perLevel;
            var inLevel = index % perLevel;
            var halfX = (boxesPerSide - 1) * 0.5f;
            var halfZ = (rows - 1) * 0.5f;
            return new Vector3((inLevel % boxesPerSide - halfX) * boxSpacing, firstLevelHeight + level * levelHeight, (inLevel / boxesPerSide - halfZ) * boxSpacing);
        }
    }
}
