using APC.Data;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 색 보관 구역 하나의 보관함 (GDD 7.3, M20-2 통합). 네 모서리 선반 모델(`ShelfModel`, 바로 아래 자식)에 상자가 실제로 올라가고,
    /// 수량(7/24)은 구역에 하나만 표시한다. 가운데 ShelfZone 하나로 넣고 꺼낸다.
    /// 칸 순서 = 모델을 돌아가며 하나씩 → 빈 선반에 하나씩 채워진다.
    /// </summary>
    public sealed class Shelf : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("상자 부모 (Shelf 로컬 원점 · 회전 · 크기 그대로인 자식)")] Transform boxRoot;
        [SerializeField] TMP_Text countLabel;

        readonly ReactiveProperty<int> count = new(0);
        CargoPile pile;
        ShelfModel[] models;
        int capacity;

        public ItemDefinition Item { get; private set; }
        public int Capacity => capacity;
        public ReadOnlyReactiveProperty<int> Count => count;
        public bool HasSpace => pile.Count < capacity;
        public int FreeSpace => Mathf.Max(0, capacity - pile.Count);

        /// <summary>선반 모델 수 (A~C 4 · D 랙 4).</summary>
        public int ModelCount => models.Length;

        /// <summary>수량 글자 (떠오르는 알림 글자의 원본으로도 쓴다).</summary>
        public TMP_Text CountLabel => countLabel;

        void Awake()
        {
            models = GetComponentsInChildren<ShelfModel>(true);
            if (models.Length == 0) Debug.LogWarning($"[Shelf] {name} 아래에 선반 모델이 없습니다.", this);
            pile = new CargoPile(boxRoot, SlotPosition);
        }

        void OnDestroy() => count.Dispose();

        /// <summary>1단계 용량 (구역 선반 24 · 팔레트 랙 16). 단계 용량은 `CapacityAt`.</summary>
        public int BaseCapacity { get; private set; }

        /// <summary>단계 용량 (GDD 7.3): 단계마다 모델 1개당 `shelfStageStepPerModel`(2)칸씩 → 선반 24 · 32 · 40, 랙 16 · 24 · 32.</summary>
        public int CapacityAt(int stage) => BaseCapacity + Core.GameManager.Instance.Database.Logistics.ShelfStageStepPerModel * ModelCount * (stage - 1);

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
            foreach (var model in models)
            {
                if (model.IconRenderer == null) continue;
                model.IconRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, item.TapeColor);
                model.IconRenderer.SetPropertyBlock(block);
            }

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

        // 모델을 돌아가며 한 칸씩 (빈 선반에 하나씩). boxRoot는 Shelf 로컬 원점이라 모델 로컬 TRS로 바로 계산한다
        Vector3 SlotPosition(int index)
        {
            if (models.Length == 0) return Vector3.zero;
            return models[index % models.Length].SlotInParent(index / models.Length);
        }
    }
}
