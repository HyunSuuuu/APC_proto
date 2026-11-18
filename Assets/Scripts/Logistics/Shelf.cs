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
    public sealed class Shelf : MonoBehaviour
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
