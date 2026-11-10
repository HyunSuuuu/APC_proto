using APC.Data;
using DG.Tweening;
using UnityEngine;

namespace APC.Logistics
{
    public enum CargoState
    {
        Normal,

        /// <summary>반품 상자 (Lv.11~). 분류 선반에 넣을 수 없다 (GDD 7.7).</summary>
        Return,
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

        MaterialPropertyBlock block;

        public ItemDefinition Item { get; private set; }
        public CargoState State { get; private set; }
        public CargoOrigin Origin { get; set; }
        public Transform Visual => visual;

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
        }

        void ApplyLook()
        {
            block ??= new MaterialPropertyBlock();
            tapeRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Item != null ? Item.TapeColor : Color.white);
            tapeRenderer.SetPropertyBlock(block);
            // 반품 스티커는 반품 마일스톤에서 추가한다.
        }
    }
}
