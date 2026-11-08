using System;
using System.Collections.Generic;
using APC.Core;
using APC.Data;
using R3;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 씬의 부지 (GDD 4.2, 9.3). SiteLayout config대로 붙은 모듈을 관리한다.
    /// 잠긴 칸에는 아무것도 없고(보이지 않음 · 들어갈 수 없음), 해금(Unlock)하면 모듈 프리팹이 팝 연출과 함께 나타난다.
    /// 플레이어 · 알바는 열린 칸 안에서만 움직인다 (IsAreaWalkable).
    /// </summary>
    public sealed class SiteGrid : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            [Tooltip("붙어 있는 모듈. 잠겨 있으면 비어 있다")] public SiteModule instance;
        }

        public static SiteGrid Instance { get; private set; }

        [SerializeField] List<Entry> entries = new();

        readonly Subject<string> unlocked = new();
        readonly HashSet<Vector2Int> openCells = new(); // (열, 행)
        SiteLayout layout;

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>모듈이 해금될 때마다 그 id를 보낸다 (네비메시 재굽기 · 저장 등).</summary>
        public Observable<string> OnUnlocked => unlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[SiteGrid] 씬에 SiteGrid가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            layout = GameManager.Instance.Database.Site;

            foreach (var entry in entries)
            {
                if (entry.instance != null && layout.TryGetPlacement(entry.id, out var placement)) OpenCells(placement);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            unlocked.Dispose();
        }

        public bool IsUnlocked(string id) => Find(id)?.instance != null;

        /// <summary>
        /// 잠긴 모듈을 붙인다 (팝 연출). 비용 · 레벨 · 인접 조건은 부르는 쪽(구매 패드, M5)이 확인한다.
        /// 이미 열려 있거나 config에 없으면 false.
        /// </summary>
        public bool Unlock(string id)
        {
            var entry = Find(id);
            if (entry == null || entry.instance != null) return false;

            if (!layout.TryGetPlacement(id, out var placement) || placement.module == null)
            {
                Debug.LogWarning($"[SiteGrid] '{id}' 배치나 모듈 프리팹이 config에 없습니다.", this);
                return false;
            }

            var module = Instantiate(placement.module, layout.GetModuleCenter(placement, placement.module.Size),
                SiteLayout.GetRotation(placement), transform);
            module.name = $"Module_{id}";
            entry.instance = module;
            OpenCells(placement);
            FacilityPop.Play(module.transform);
            unlocked.OnNext(id);
            return true;
        }

        /// <summary>그 위치가 열린 칸 안인가.</summary>
        public bool IsWalkable(Vector3 worldPosition)
        {
            var size = layout.CellSize;
            var column = Mathf.FloorToInt(worldPosition.x / size + 0.5f);
            var row = 1 + Mathf.FloorToInt(-worldPosition.z / size + 0.5f);
            return openCells.Contains(new Vector2Int(column, row));
        }

        /// <summary>반지름 radius인 원(네 모서리)이 모두 열린 칸 안인가.</summary>
        public bool IsAreaWalkable(Vector3 center, float radius)
            => IsWalkable(center + new Vector3(radius, 0f, radius))
               && IsWalkable(center + new Vector3(radius, 0f, -radius))
               && IsWalkable(center + new Vector3(-radius, 0f, radius))
               && IsWalkable(center + new Vector3(-radius, 0f, -radius));

        /// <summary>에디터 Build Site 전용.</summary>
        public void SetEntries(List<Entry> baked) => entries = baked;

        void OpenCells(SiteLayout.ModulePlacement placement)
        {
            var size = SiteLayout.RotatedSize(placement.module != null ? placement.module.Size : Vector2Int.one, placement.rotation);
            for (var r = 0; r < size.y; r++)
            for (var c = 0; c < size.x; c++)
                openCells.Add(new Vector2Int(placement.column + c, placement.row + r));
        }

        Entry Find(string id)
        {
            foreach (var entry in entries)
            {
                if (entry.id == id) return entry;
            }
            return null;
        }
    }
}
