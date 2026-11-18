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
        readonly Subject<Unit> movedSubject = new();
        readonly HashSet<Vector2Int> openCells = new(); // (열, 행)
        readonly HashSet<Vector2Int> extraWalkable = new(); // 시작 전 빈 땅 + 입구 (StartSequence, GDD 9.6)
        readonly Dictionary<string, SiteModule> parked = new(); // 씬에 미리 놓였지만 아직 안 지은 시작 모듈 (꺼 둠)
        SiteLayout layout;

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>모듈이 붙을 때마다 그 id를 보낸다 (네비메시 재굽기 · 저장 등). 묶음 해금이면 모듈마다 한 번씩.</summary>
        public Observable<string> OnUnlocked => unlocked;

        /// <summary>모듈 재배치가 적용될 때 (네비메시 재굽기 · 알바 길 다시 찾기 · 발판 자리, GDD 4.4).</summary>
        public Observable<Unit> OnMoved => movedSubject;

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
            movedSubject.Dispose();
        }

        public bool IsUnlocked(string id) => Find(id)?.instance != null;

        /// <summary>모듈 인스턴스의 배치 id (없으면 null).</summary>
        public string IdOf(SiteModule module)
        {
            foreach (var entry in entries)
            {
                if (entry.instance == module) return entry.id;
            }
            return null;
        }

        /// <summary>
        /// 잠긴 모듈을 붙인다 (팝 연출). **같이 해금되는 묶음 전체**(SiteLayout.unlockWith)가 한꺼번에 열린다 —
        /// 묶음 안의 어느 id를 넘겨도 같다. 비용 · 레벨 · 인접 조건은 부르는 쪽(구매 패드, M5)이 대표 배치 기준으로 확인한다.
        /// 새로 붙은 모듈이 없으면 false.
        /// </summary>
        public bool Unlock(string id)
        {
            var attached = false;
            foreach (var member in layout.GetUnlockGroup(layout.GetUnlockRoot(id)))
                attached |= Attach(member);
            return attached;
        }

        /// <summary>모듈 하나를 붙이고 칸을 연다. 이미 열려 있거나 config에 없으면 false.</summary>
        bool Attach(string id)
        {
            var entry = Find(id);
            if (entry == null || entry.instance != null) return false;

            if (!layout.TryGetPlacement(id, out var placement))
            {
                Debug.LogWarning($"[SiteGrid] '{id}' 배치가 config에 없습니다.", this);
                return false;
            }

            var cells = SiteLayout.GetCells(placement);
            if (parked.Remove(id, out var module))
            {
                module.gameObject.SetActive(true); // 씬에 놓인 그대로 (손으로 고친 회전 등 유지)
            }
            else
            {
                if (placement.module == null)
                {
                    Debug.LogWarning($"[SiteGrid] '{id}' 모듈 프리팹이 config에 없습니다.", this);
                    return false;
                }
                var position = layout.GetModuleCenter(placement, placement.module.Size);
                if (cells.Count == 1 && openCells.Contains(cells[0]))
                {
                    // 빈 공간 칸에 놓이는 모듈: 처음 칸이 재배치로 차 있으면 가장 가까운 빈 공간 (GDD 4.2 · 4.4, 발판 자리와 같은 규칙)
                    if (!TryFindEmptyCell(cells[0], out var cell))
                    {
                        Debug.LogWarning($"[SiteGrid] '{id}'를 놓을 빈 공간이 없습니다.", this);
                        return false;
                    }
                    cells[0] = cell;
                    position = layout.CellCenter(cell.y, cell.x);
                }

                HideEmptyTiles(cells);
                module = Instantiate(placement.module, position, SiteLayout.GetRotation(placement), transform);
                module.name = $"Module_{id}";
            }
            entry.instance = module;
            foreach (var cell in cells) openCells.Add(cell);
            FacilityPop.Play(module.transform);
            unlocked.OnNext(id);
            return true;
        }

        /// <summary>그 칸들의 빈 공간 타일을 끈다 (GDD 4.2). 끈 타일도 붙어 있는 것으로 친다 (instance 유지).</summary>
        void HideEmptyTiles(List<Vector2Int> cells)
        {
            foreach (var cell in cells)
            {
                var tile = ModuleAt(cell);
                if (tile != null && tile.Kind == SiteModule.ModuleKind.Empty) tile.gameObject.SetActive(false);
            }
        }

        /// <summary>월드 위치 → 칸 (x = 열, y = 행).</summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            var size = layout.CellSize;
            return new Vector2Int(Mathf.FloorToInt(worldPosition.x / size + 0.5f), 1 + Mathf.FloorToInt(-worldPosition.z / size + 0.5f));
        }

        /// <summary>칸 중심 월드 위치.</summary>
        public Vector3 CellCenter(Vector2Int cell) => layout.CellCenter(cell.y, cell.x);

        /// <summary>
        /// 지금 그 칸에 있는 모듈 (켜진 것만, 1칸 모듈 기준 — 재배치로 config 칸과 다를 수 있어 실제 위치로 찾는다). 없으면 null.
        /// </summary>
        public SiteModule ModuleAt(Vector2Int cell)
        {
            foreach (var entry in entries)
            {
                if (entry.instance == null || !entry.instance.gameObject.activeSelf) continue;
                if (WorldToCell(entry.instance.transform.position) == cell) return entry.instance;
            }
            return null;
        }

        /// <summary>칸이 열린 빈 공간(켜진 빈 공간 타일)인가.</summary>
        public bool IsEmptyTile(Vector2Int cell) => ModuleAt(cell) is { Kind: SiteModule.ModuleKind.Empty };

        /// <summary>preferred가 빈 공간이면 그 칸, 아니면 가장 가까운 빈 공간 칸. 빈 공간이 없으면 false.</summary>
        public bool TryFindEmptyCell(Vector2Int preferred, out Vector2Int cell)
        {
            cell = preferred;
            var best = float.MaxValue;
            foreach (var entry in entries)
            {
                if (entry.instance == null || entry.instance.Kind != SiteModule.ModuleKind.Empty || !entry.instance.gameObject.activeSelf) continue;
                var candidate = WorldToCell(entry.instance.transform.position);
                var distance = (candidate - preferred).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                cell = candidate;
            }
            return best < float.MaxValue;
        }

        /// <summary>
        /// 모듈 재배치 적용 (GDD 4.4, 사무실 PC `모듈` 탭을 닫을 때). 모듈 → 새 칸. 보관 · 반품 · 빈 공간 모듈의 자리 맞바꿈이라 열린 칸은 그대로다.
        /// 모듈 통째로 옮기므로 선반 · 상자 · 작업 지점이 같이 간다. 옮긴 모듈은 팝 연출, 하나라도 옮겼으면 OnMoved.
        /// </summary>
        public void ApplyArrangement(IReadOnlyDictionary<SiteModule, Vector2Int> arrangement)
        {
            var moved = false;
            foreach (var pair in arrangement)
            {
                if (pair.Key == null || WorldToCell(pair.Key.transform.position) == pair.Value) continue;
                pair.Key.transform.position = CellCenter(pair.Value);
                if (pair.Key.Kind != SiteModule.ModuleKind.Empty) FacilityPop.Play(pair.Key.transform);
                moved = true;
            }
            if (moved) movedSubject.OnNext(Unit.Default);
        }

        /// <summary>그 위치가 열린 칸 안인가 (시작 전에는 빈 땅 · 입구 칸도).</summary>
        public bool IsWalkable(Vector3 worldPosition)
        {
            var cell = WorldToCell(worldPosition);
            return openCells.Contains(cell) || extraWalkable.Contains(cell);
        }

        /// <summary>
        /// 시작 순서(StartSequence, GDD 9.6): 씬에 놓인 모듈을 꺼서 잠근다 — 나중에 Unlock하면 새로 만들지 않고 이것을 켠다.
        /// SiteGrid.Awake보다 먼저 불러야 칸이 열리지 않는다.
        /// </summary>
        public void Park(string id)
        {
            var entry = Find(id);
            if (entry?.instance == null) return;
            entry.instance.gameObject.SetActive(false);
            parked[id] = entry.instance;
            entry.instance = null;
        }

        /// <summary>모듈 없이 칸만 연다 (시작 건물: 아직 안 지은 하차 · 보관 · 상차 · 사무실 칸). OnUnlocked는 빈 id로 보낸다.</summary>
        public void OpenCell(Vector2Int cell)
        {
            if (openCells.Add(cell)) unlocked.OnNext(string.Empty);
        }

        /// <summary>열린 칸이 아니어도 걸을 수 있는 칸 (시작 전 빈 땅 · 입구). 빈 목록이면 끈다.</summary>
        public void SetExtraWalkable(IEnumerable<Vector2Int> cells)
        {
            extraWalkable.Clear();
            extraWalkable.UnionWith(cells);
        }

        /// <summary>반지름 radius인 원(네 모서리)이 모두 열린 칸 안인가.</summary>
        public bool IsAreaWalkable(Vector3 center, float radius)
            => IsWalkable(center + new Vector3(radius, 0f, radius))
               && IsWalkable(center + new Vector3(radius, 0f, -radius))
               && IsWalkable(center + new Vector3(-radius, 0f, radius))
               && IsWalkable(center + new Vector3(-radius, 0f, -radius));

        /// <summary>에디터 Build Site 전용.</summary>
        public void SetEntries(List<Entry> baked) => entries = baked;

        /// <summary>칸 (x = 열, y = 행)이 열려 있는가.</summary>
        public bool IsCellOpen(Vector2Int cell) => openCells.Contains(cell);

        /// <summary>열린 칸 전부 (x = 열, y = 행). 벽(SiteWalls)이 바깥 테두리를 찾을 때 쓴다.</summary>
        public IReadOnlyCollection<Vector2Int> OpenedCells => openCells;

        void OpenCells(SiteLayout.ModulePlacement placement)
        {
            foreach (var cell in SiteLayout.GetCells(placement)) openCells.Add(cell);
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
