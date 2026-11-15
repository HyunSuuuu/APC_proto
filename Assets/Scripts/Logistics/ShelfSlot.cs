using System.Collections.Generic;
using APC.Data;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 선반 자리 (색 보관 구역 모듈의 네 모서리, GDD 4.2 · 7.3). 자기 색(품목)과 선반 모양을 들고 있다가,
    /// 모듈이 붙으면(Start) 그 자리에 선반을 세운다 → 구역 모듈이 생기는 순간 선반 4개가 함께 생긴다.
    /// 모듈이 붙을 때 스스로 등록하므로, 모듈이 어디에 붙든 Warehouse가 찾는다.
    /// </summary>
    public sealed class ShelfSlot : MonoBehaviour
    {
        static readonly List<ShelfSlot> Registered = new();

        [SerializeField, Tooltip("이 자리 선반의 품목 (= 구역 색). 비우면 선반을 세우지 않음")] ItemDefinition item;
        [SerializeField, Tooltip("선반 모양. 비우면 Warehouse 기본 선반 (D 가구 = 팔레트 랙)")] Shelf shelfPrefab;
        [SerializeField, Min(0), Tooltip("선반 용량. 0이면 LogisticsConfig.shelfCapacity")] int capacity;
        [SerializeField, Min(1), Tooltip("이 레벨부터 쓸 수 있는 자리 (기본 모듈은 모두 1)")] int unlockLevel = 1;
        [SerializeField, Min(0), Tooltip("모듈 안 순서")] int order;

        public static IReadOnlyList<ShelfSlot> All => Registered;

        /// <summary>선반 + 둘레 존이 차지할 바닥.</summary>
        public Rect WorldFootprint => APC.Interaction.InteractionZone.FootprintRect(transform, new Vector2(1.6f, 1.6f));

        public ItemDefinition Item => item;
        public Shelf ShelfPrefab => shelfPrefab;
        public int Capacity => capacity;
        public int UnlockLevel => unlockLevel;
        public int Order => order;
        public Shelf Occupant { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);

        void Start()
        {
            if (item != null && Occupant == null && Warehouse.Instance != null) Warehouse.Instance.BuildShelf(this);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Occupant != null ? new Color(0.3f, 0.9f, 0.4f, 0.6f) : new Color(0.9f, 0.9f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 1.7f, 2.7f));
        }
    }
}
