using System.Collections.Generic;
using UnityEngine;

namespace APC.Interaction
{
    /// <summary>
    /// 바닥 마킹이 있는 상호작용 존의 기반 (GDD 5.2). 영역은 루트 기준 XZ 사각형이며 루트 크기는 1로 둔다.
    /// 존끼리는 겹치지 않게 배치한다 (겹치면 먼저 등록된 존이 우선).
    /// </summary>
    public abstract class InteractionZone : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly List<InteractionZone> All = new();

        [SerializeField] Vector2 size = new(2.5f, 2.5f);

        [Header("바닥 표시")]
        [SerializeField] Renderer floor;
        [SerializeField] Color idleColor = new(1f, 1f, 1f, 1f);
        [SerializeField, Tooltip("상호작용 중 색. 대기(0.5초) 동안 idle → active로 밝아진다")] Color activeColor = new(1f, 1f, 1f, 1f);

        readonly List<Interactor> occupants = new();
        MaterialPropertyBlock block;

        public Vector2 Size => size;
        public IReadOnlyList<Interactor> Occupants => occupants;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public static InteractionZone FindAt(Vector3 worldPosition)
        {
            foreach (var zone in All)
            {
                if (zone.Contains(worldPosition)) return zone;
            }
            return null;
        }

        public bool Contains(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
        }

        /// <summary>
        /// true면 멈춘 뒤 대기 시간(0.5초)이 지나야 Tick이 불린다 (운반 존).
        /// false면 멈춰 있는 동안 바로 Tick이 불린다 — 게이지 존은 게이지 자체가 대기 역할을 한다.
        /// </summary>
        public virtual bool WaitsForReadyDelay => true;

        /// <summary>상호작용 가능한 interactor마다 매 프레임 호출된다.</summary>
        public abstract void Tick(Interactor who, float deltaTime);

        /// <summary>바닥 색 진행도 0~1 (idle → active).</summary>
        protected virtual float FloorProgress(Interactor who) => who.ReadyProgress;

        protected virtual void OnEnable()
        {
            All.Add(this);
            SetFloorColor(idleColor);
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
            occupants.Clear();
        }

        protected virtual void OnEnter(Interactor who) { }
        protected virtual void OnExit(Interactor who) { }

        protected virtual void Update()
        {
            if (occupants.Count == 0) return;
            var ready = 0f;
            foreach (var who in occupants) ready = Mathf.Max(ready, FloorProgress(who));
            SetFloorColor(Color.Lerp(idleColor, activeColor, ready));
        }

        internal void Enter(Interactor who)
        {
            if (!occupants.Contains(who)) occupants.Add(who);
            OnEnter(who);
        }

        internal void Exit(Interactor who)
        {
            occupants.Remove(who);
            if (occupants.Count == 0) SetFloorColor(idleColor);
            OnExit(who);
        }

        void SetFloorColor(Color color)
        {
            if (floor == null) return;
            block ??= new MaterialPropertyBlock();
            floor.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            floor.SetPropertyBlock(block);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(size.x, 0.1f, size.y));
        }
    }
}
