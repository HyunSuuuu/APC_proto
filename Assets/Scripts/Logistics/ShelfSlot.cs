using System.Collections.Generic;
using APC.Data;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 보관 구역 자리 (색 보관 구역 모듈 가운데, GDD 4.2 · 7.3). 자기 색(품목) · 용량을 들고 있다가,
    /// 모듈이 붙으면(Start) 같은 오브젝트의 보관함(`Shelf`, 선반 모델 4개 통합 M20-2)을 설정 · 등록한다 → 구역 모듈이 생기는 순간 선반 4개가 함께 생긴다.
    /// 모듈이 붙을 때 스스로 등록하므로, 모듈이 어디에 붙든 Warehouse가 찾는다.
    /// </summary>
    public sealed class ShelfSlot : MonoBehaviour
    {
        static readonly List<ShelfSlot> Registered = new();

        [SerializeField, Tooltip("이 자리 선반의 품목 (= 구역 색). 비우면 선반을 세우지 않음")] ItemDefinition item;
        [SerializeField, Min(0), Tooltip("선반 용량. 0이면 LogisticsConfig.shelfCapacity")] int capacity;
        [SerializeField, Min(1), Tooltip("이 레벨부터 쓸 수 있는 자리 (기본 모듈은 모두 1)")] int unlockLevel = 1;
        [SerializeField, Min(0), Tooltip("모듈 안 순서")] int order;

        public static IReadOnlyList<ShelfSlot> All => Registered;

        /// <summary>선반 모델 4개 + 가운데 존이 차지할 바닥.</summary>
        public Rect WorldFootprint => APC.Interaction.InteractionZone.FootprintRect(transform, new Vector2(3.6f, 3.6f)); // 반 길이: 모서리 선반(±2.4) + 선반 반 폭

        public ItemDefinition Item => item;
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
            if (item == null || Occupant != null || Warehouse.Instance == null) return;
            var shelf = GetComponentInChildren<Shelf>(true);
            if (shelf == null)
            {
                Debug.LogWarning($"[ShelfSlot] {name}에 보관함(Shelf)이 없습니다.", this);
                return;
            }
            Warehouse.Instance.SetupShelf(this, shelf);
        }
    }
}
