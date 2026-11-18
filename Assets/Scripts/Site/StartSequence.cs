using System;
using System.Collections;
using System.Collections.Generic;
using APC.Characters;
using APC.Core;
using APC.Data;
using APC.Economy;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 시작 — 빈 땅에서 건물 세우기 (GDD 9.6). SiteGrid 오브젝트에 붙는다 (SiteGrid.Awake보다 먼저 돌아야 함).
    /// ① 씬에 놓인 시작 모듈(isStartArea)을 모두 꺼 두고(SiteGrid.Park), 13칸 둘레에 공사 울타리(입구 칸만 트임) · 플레이어는 입구 앞 도로변.
    /// ② 발판을 하나씩: 건물 → 하차 1 → A보관 → 상차 1 → 사무실 (비용 EconomyConfig.StartBuildCost, XP 없음).
    ///    건물을 사면 플레이어가 선 칸부터 거리 순서로 칸이 열리며 바닥 · 쓰레기장이 팝 (안 지은 모듈 칸은 빈 바닥 자리표시).
    /// ③ 상차 1을 지으면 하차 트럭 시작(TrucksOpen), 사무실을 지으면 끝(OfficeBuilt): 저금통 · 파트 시계 · 레벨 건설 타일.
    /// 그 전엔 주운 돈이 바로 지갑으로 (Wallet.DirectToCash). 디버그 [시작 건너뛰기] = Skip.
    /// 이 컴포넌트가 없으면(또는 꺼져 있으면) 옛 Lv.1 시작 그대로.
    /// </summary>
    [DefaultExecutionOrder(-950)] // GameManager(-1000) 뒤, SiteGrid(0) 앞
    public sealed class StartSequence : MonoBehaviour
    {
        static readonly string[] StepIds = { null, "In1", "StoreA1", "Out1", "Office" }; // null = 건물 (모듈 없음)
        static readonly string[] StepTitles = { "건물", "하차 구역", "보관 구역 A", "상차 구역", "사무실" };
        const int TruckStep = 3;

        [SerializeField, Tooltip("입구 바깥 칸 (x = 열, y = 행) — 플레이어 시작 · 울타리 트임")] Vector2Int entranceCell = new(0, 2);
        [SerializeField, Tooltip("입구 안쪽 칸 (건물 발판)")] Vector2Int doorCell = new(1, 2);
        [SerializeField, Tooltip("건물 발판 위치: 입구 안쪽 칸 중심에서 (m)")] Vector3 firstPadOffset = new(-2f, 0f, 0f);
        [SerializeField, Min(0.1f), Tooltip("건물 팝 전체 시간 (초)")] float buildDuration = 1f;

        public static StartSequence Instance { get; private set; }

        /// <summary>사무실까지 다 지었는가. 시작 순서가 없으면 처음부터 true.</summary>
        public static bool OfficeBuilt { get; private set; } = true;

        /// <summary>하차 트럭이 와도 되는가 (상차 1을 지은 뒤).</summary>
        public static bool TrucksOpen { get; private set; } = true;

        readonly HashSet<Vector2Int> siteCells = new();
        readonly List<string> startIds = new();
        readonly Dictionary<string, GameObject> placeholders = new();
        readonly List<ConstructionFence> fences = new();
        SiteGrid grid;
        SiteUnlocker unlocker;
        SiteLayout layout;
        ExpansionPad pad;
        GameObject entranceGround;
        Transform player;
        Vector3 playerHome;
        Quaternion playerHomeRotation;
        int step = -1;

        public bool Running => Instance == this && !OfficeBuilt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            OfficeBuilt = true;
            TrucksOpen = true;
        }

        void Awake()
        {
            Instance = this;
            grid = GetComponent<SiteGrid>();
            unlocker = GetComponent<SiteUnlocker>();
            layout = GameManager.Instance.Database.Site;
            foreach (var placement in layout.Modules)
            {
                if (!layout.IsStartOpen(placement)) continue;
                startIds.Add(placement.id);
                siteCells.UnionWith(SiteLayout.GetCells(placement));
                grid.Park(placement.id);
            }
            OfficeBuilt = false;
            TrucksOpen = false;
            GameManager.Instance.Wallet.DirectToCash = true;
            grid.SetExtraWalkable(new HashSet<Vector2Int>(siteCells) { entranceCell });
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var mover = FindFirstObjectByType<PlayerMover>();
            if (mover != null)
            {
                player = mover.transform;
                playerHome = player.position;
                playerHomeRotation = player.rotation;
                var inward = grid.CellCenter(doorCell) - grid.CellCenter(entranceCell);
                Teleport(grid.CellCenter(entranceCell), Quaternion.LookRotation(inward));
            }
            BuildFence();
            PlaceFloors();
            // 모듈이 붙으면 그 칸의 빈 바닥 자리표시를 지운다
            grid.OnUnlocked.Subscribe(id =>
            {
                if (!string.IsNullOrEmpty(id) && placeholders.Remove(id, out var go)) Destroy(go);
            }).AddTo(this);
            NextStep();
        }

        /// <summary>디버그 [시작 건너뛰기]: 시작 모듈을 모두 짓고 옛 Lv.1 시작 상태로 (플레이어 원래 자리).</summary>
        public void Skip()
        {
            if (!Running) return;
            StopAllCoroutines();
            if (pad != null) Destroy(pad.gameObject);
            pad = null;
            ClearFences();
            foreach (var id in startIds) grid.Unlock(id);
            grid.SetExtraWalkable(Array.Empty<Vector2Int>());
            if (entranceGround != null) Destroy(entranceGround);
            if (player != null) Teleport(playerHome, playerHomeRotation);
            Finish();
        }

        void NextStep()
        {
            step++;
            var prefab = unlocker != null ? unlocker.PadPrefab : null;
            if (prefab == null)
            {
                Debug.LogError("[StartSequence] SiteUnlocker 발판 프리팹이 없습니다 — 시작을 건너뜁니다.", this);
                Skip();
                return;
            }
            var id = StepIds[step];
            var position = id == null ? grid.CellCenter(doorCell) + firstPadOffset : CellOf(id);
            pad = Instantiate(prefab, position, Quaternion.identity, transform);
            pad.name = $"StartPad_{step}";
            pad.RewardXp = false;
            pad.Setup(id, StepTitles[step], GameManager.Instance.Database.Economy.StartBuildCost(step));
            pad.Purchased += OnPadPurchased;
            FacilityPop.Play(pad.transform);
        }

        void OnPadPurchased()
        {
            pad = null;
            if (step == 0)
            {
                StartCoroutine(BuildSite());
                return;
            }
            if (step == TruckStep) TrucksOpen = true;
            if (step == StepIds.Length - 1) Finish();
            else NextStep();
        }

        /// <summary>건물: 울타리를 걷고, 플레이어 칸부터 가까운 순서로 칸을 연다 (빈 공간 · 쓰레기장 = 모듈, 나머지 = 빈 바닥 자리표시).</summary>
        IEnumerator BuildSite()
        {
            ClearFences();
            var origin = player != null ? grid.WorldToCell(player.position) : doorCell;
            var cells = new List<Vector2Int>(siteCells);
            cells.Sort((a, b) => (a - origin).sqrMagnitude.CompareTo((b - origin).sqrMagnitude));
            var wait = new WaitForSeconds(buildDuration / Mathf.Max(1, cells.Count));
            foreach (var cell in cells)
            {
                var id = StartIdAt(cell);
                if (id != null && Array.IndexOf(StepIds, id) < 0) grid.Unlock(id); // 자리표시는 OnUnlocked에서 지움
                else
                {
                    if (id != null && placeholders.TryGetValue(id, out var tile)) tile.transform.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6, 0.5f);
                    grid.OpenCell(cell);
                }
                yield return wait;
            }
            grid.SetExtraWalkable(Array.Empty<Vector2Int>());
            if (entranceGround != null) Destroy(entranceGround);
            // 연출 중에 입구 밖으로 나갔으면 안으로 (밖에 있으면 칸 제한이 안 걸림)
            if (player != null && !siteCells.Contains(grid.WorldToCell(player.position)))
                Teleport(grid.CellCenter(doorCell), player.rotation);
            NextStep();
        }

        void Finish()
        {
            OfficeBuilt = true;
            TrucksOpen = true;
            GameManager.Instance.Wallet.DirectToCash = false;
            if (PartService.Instance != null) PartService.Instance.BeginFirstPart();
            if (unlocker != null) unlocker.Refresh(); // 이제 레벨 건설 타일 (B보관 …)
        }

        /// <summary>13칸 바깥 둘레에만 울타리 (칸마다 하나, 안쪽 변 · 입구 변 조각은 끔, 표지 없음).</summary>
        void BuildFence()
        {
            var prefab = unlocker != null ? unlocker.FencePrefab : null;
            if (prefab == null) return;
            var edge = layout.CellSize * 0.4f;
            foreach (var cell in siteCells)
            {
                var fence = Instantiate(prefab, grid.CellCenter(cell), Quaternion.identity, transform);
                fence.name = "StartFence";
                fence.SetLevel(0, false);
                foreach (Transform part in fence.transform)
                {
                    var p = part.localPosition;
                    var keep = false;
                    // 로컬 +z = 위 행(행 −1), +x = 오른쪽 열 (WorldToCell 규칙)
                    if (p.z > edge) keep |= Closed(cell, new Vector2Int(0, -1));
                    if (p.z < -edge) keep |= Closed(cell, new Vector2Int(0, 1));
                    if (p.x > edge) keep |= Closed(cell, new Vector2Int(1, 0));
                    if (p.x < -edge) keep |= Closed(cell, new Vector2Int(-1, 0));
                    if (!keep) part.gameObject.SetActive(false); // 안쪽 변 · 입구 변 · 표지(가장자리 아님)
                }
                fences.Add(fence);
            }
        }

        /// <summary>
        /// 빈 땅 바닥: 13칸 모두 빈 공간 타일 자리표시(바닥 충돌체 포함 — 부지 땅은 y 0보다 낮다), 입구 칸은 보이지 않는 바닥.
        /// 자리표시는 그 칸 모듈이 붙을 때 지운다.
        /// </summary>
        void PlaceFloors()
        {
            var floor = EmptyTilePrefab();
            foreach (var cell in siteCells)
            {
                var id = StartIdAt(cell);
                if (floor == null || id == null || placeholders.ContainsKey(id)) continue;
                var tile = Instantiate(floor, grid.CellCenter(cell), Quaternion.identity, transform);
                tile.name = $"StartFloor_{id}";
                placeholders[id] = tile.gameObject;
            }
            entranceGround = new GameObject("StartEntranceGround");
            entranceGround.transform.SetParent(transform, false);
            entranceGround.transform.position = grid.CellCenter(entranceCell) + Vector3.down * 0.5f;
            entranceGround.AddComponent<BoxCollider>().size = new Vector3(layout.CellSize, 1f, layout.CellSize);
        }

        bool Closed(Vector2Int cell, Vector2Int side)
        {
            var next = cell + side;
            if (cell == doorCell && next == entranceCell) return false;
            return !siteCells.Contains(next);
        }

        void ClearFences()
        {
            foreach (var fence in fences)
            {
                if (fence == null) continue;
                var t = fence.transform;
                t.DOKill();
                t.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetLink(fence.gameObject).OnComplete(() => Destroy(t.gameObject));
            }
            fences.Clear();
        }

        string StartIdAt(Vector2Int cell)
        {
            foreach (var id in startIds)
            {
                if (layout.TryGetPlacement(id, out var placement) && SiteLayout.GetCells(placement).Contains(cell)) return id;
            }
            return null;
        }

        Vector3 CellOf(string id)
            => layout.TryGetPlacement(id, out var placement) ? grid.CellCenter(SiteLayout.GetCells(placement)[0]) : grid.CellCenter(doorCell);

        SiteModule EmptyTilePrefab()
        {
            foreach (var placement in layout.Modules)
            {
                if (placement.module != null && placement.module.Kind == SiteModule.ModuleKind.Empty) return placement.module;
            }
            return null;
        }

        void Teleport(Vector3 position, Quaternion rotation)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false; // 켜져 있으면 위치가 되돌아간다
            player.SetPositionAndRotation(position, rotation);
            if (controller != null) controller.enabled = true;
        }
    }
}
