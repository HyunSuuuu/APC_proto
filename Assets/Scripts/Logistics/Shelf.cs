using APC.Data;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 품목 1종 전용 분류 선반 (GDD 7.3). 칸에 실제 상자가 올라가고 위에 수량(7/10)을 표시한다.
    /// 앞쪽(-x) 적재 존, 뒤쪽(+x) 수거 존(M4).
    /// </summary>
    public sealed class Shelf : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform boxRoot;
        [SerializeField, Tooltip("선반 위 품목 색 표시")] Renderer iconRenderer;
        [SerializeField] TMP_Text countLabel;

        [Header("칸 배치 (선반 로컬, 길이 방향 = z)")]
        [SerializeField, Min(1)] int boxesPerLevel = 5;
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

        void Awake() => pile = new CargoPile(boxRoot, SlotPosition);

        void OnDestroy() => count.Dispose();

        public void Setup(ItemDefinition item, int shelfCapacity)
        {
            Item = item;
            capacity = shelfCapacity;

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
            var level = index / boxesPerLevel;
            var column = index % boxesPerLevel;
            return new Vector3(0f, firstLevelHeight + level * levelHeight, (column - (boxesPerLevel - 1) * 0.5f) * boxSpacing);
        }
    }
}
