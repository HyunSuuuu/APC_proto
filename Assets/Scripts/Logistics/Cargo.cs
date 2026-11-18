using APC.Data;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    public enum CargoState
    {
        Normal,

        /// <summary>찌그러진 상자 (반품 구역을 지은 뒤). 보관 선반에 넣을 수 없고 반품 팔레트 → 재포장 테이블 입력으로만 간다 (GDD 7.7).</summary>
        Return,

        /// <summary>포장 쓰레기 (재포장 1번 = 1개). 재포장 테이블 쓰레기 출력 → 쓰레기장 (GDD 7.9).</summary>
        Trash,

        /// <summary>금덩이 (황금 트럭, GDD 6.4). 하차 칸 공간 칸을 차지하고, 근처를 지나는 사람이 흡수 → 저금통. 옮기지 않는다. 숫자 고정 — 끝에만 더한다.</summary>
        Gold,
    }

    /// <summary>화물을 마지막으로 꺼낸 곳. 존이 받을 수 있는지 판단할 때 쓴다 (예: 트럭 → 선반 직행 금지, GDD 7.2).</summary>
    public enum CargoOrigin
    {
        None,

        /// <summary>입고 트럭에서 꺼냄 → 공용 보관소에만 놓을 수 있다.</summary>
        Truck,

        /// <summary>공용 보관소에서 꺼냄 → 분류 선반에 넣을 수 있다.</summary>
        Storage,

        /// <summary>분류 선반에 있거나 거기서 꺼냄 → 출고 트럭 상차 존만 받는다.</summary>
        Shelf,

        /// <summary>반품 팔레트에서 꺼낸 찌그러진 상자 → 재포장 테이블 입력만 받는다 (GDD 7.7).</summary>
        ReturnPallet,

        /// <summary>쓰레기 출력에서 꺼낸 쓰레기 → 쓰레기장만 받는다. 작업 고정으로 상자와 섞이지 않는다 (GDD 5.3 · 7.9).</summary>
        Trash,
    }

    /// <summary>
    /// 화물 1개 (GDD 6.1). 크기는 모두 같다 (운반 슬롯 1칸). 오브젝트 풀에서만 만들고 돌려준다.
    /// 루트는 위치 이동(DOTween), 자식 Visual은 흔들림 오프셋·펀치 연출을 받는다.
    /// </summary>
    public sealed class Cargo : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("상자 + 테이프. 흔들림·펀치는 여기에만 적용")] Transform visual;
        [SerializeField] Renderer tapeRenderer;
        [SerializeField, Tooltip("상자 몸통 (찌그러진 상자 = 회색)")] Renderer boxRenderer;
        [SerializeField] Color returnBoxColor = new(0.55f, 0.55f, 0.58f);
        [SerializeField, Tooltip("포장 쓰레기 (납작한 회갈색 판)")] Color trashColor = new(0.55f, 0.47f, 0.38f);
        [SerializeField, Tooltip("금덩이 모델 (Prop/Gold, 꺼진 채). 금이면 상자 · 테이프 대신 켠다 (GDD 6.4)")] GameObject goldModel;

        MaterialPropertyBlock block;

        public ItemDefinition Item { get; private set; }
        public CargoState State { get; private set; }

        /// <summary>찌그러진 상자를 재포장한 상자 (같은 색 일반 화물, 겉모습도 일반). 선반에 들어가면 프로토타입 목표 (GDD 1.3).</summary>
        public bool Repacked { get; private set; }
        public CargoOrigin Origin { get; set; }

        /// <summary>트럭에서 꺼낸 화물이 가야 할 하차 칸 공간 (하차 칸마다 따로, GDD 7.2). 그 밖에는 null.</summary>
        public SharedStorage Home { get; set; }
        public Transform Visual => visual;

        /// <summary>상자 높이 대비 배율 (쓰레기 0.25 — 임시 모양, 쌓을 때 간격에도 쓴다).</summary>
        public float HeightScale => State == CargoState.Trash ? 0.25f : 1f;

        internal CargoPool Pool { get; set; }

        public void Setup(ItemDefinition item, CargoState state)
        {
            Item = item;
            State = state;
            ApplyLook();
        }

        public void SetState(CargoState state)
        {
            State = state;
            ApplyLook();
        }

        /// <summary>재포장 (GDD 7.7): 같은 색(품목 그대로) 일반 상자가 된다.</summary>
        public void Repack()
        {
            State = CargoState.Normal;
            Repacked = true;
            ApplyLook();
        }

        /// <summary>풀로 돌려준다. 진행 중인 트윈은 모두 멈춘다 (TechStack 5.2).</summary>
        public void Release()
        {
            if (Pool != null) Pool.Release(this);
            else Destroy(gameObject);
        }

        internal void ResetForPool()
        {
            transform.DOKill();
            visual.DOKill();
            transform.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            Item = null;
            State = CargoState.Normal;
            Origin = CargoOrigin.None;
            Home = null;
            Repacked = false;
            ApplyLook();
        }

        /// <summary>
        /// 테이프 = 품목 색 (찌그러진 상자도 원래 색 띠, 쓰레기는 띠 없음). 상자 = 기본 재질, 찌그러진 = 회색,
        /// 쓰레기 = 회갈색 · 높이 0.25배 (GDD 7.7 · 7.9 임시 모양).
        /// </summary>
        void ApplyLook()
        {
            block ??= new MaterialPropertyBlock();
            var isGold = State == CargoState.Gold;
            if (goldModel != null) goldModel.SetActive(isGold);
            if (boxRenderer != null) boxRenderer.enabled = !isGold;
            tapeRenderer.enabled = State != CargoState.Trash && !isGold;
            tapeRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Item != null ? Item.TapeColor : Color.white);
            tapeRenderer.SetPropertyBlock(block);
            visual.localScale = new Vector3(1f, HeightScale, 1f);

            if (boxRenderer == null) return;
            if (State == CargoState.Normal)
            {
                boxRenderer.SetPropertyBlock(null);
                return;
            }
            block.Clear();
            block.SetColor(BaseColorId, State == CargoState.Trash ? trashColor : returnBoxColor);
            boxRenderer.SetPropertyBlock(block);
        }
    }
}
