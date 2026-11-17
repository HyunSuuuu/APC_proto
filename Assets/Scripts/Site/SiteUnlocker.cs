using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Interaction;
using APC.Logistics;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 부지 확장 (GDD 4.1, 9.2, 9.3). SiteGrid 오브젝트에 붙는다.
    /// ① 열린 칸과 맞닿은 잠긴 칸 구역 중 다음 해금 레벨 것 모두(+ 레벨이 됐는데 아직 안 지은 것)를 공사장 울타리 + `잠금해제 : LV N`으로 보여준다.
    /// ② 레벨이 된 해금 배치(빈 공간 칸 모듈 포함)마다 해금 발판(ExpansionPad)이 팝 연출로 나타난다 (같은 레벨 여러 개 동시, M13-8) —
    ///    빈 공간 칸이면 그 칸 중앙, 잠긴 칸이면 울타리 옆 열린 칸 경계 안쪽. 돈을 다 채우면 해금 + XP.
    /// 레벨 자동 해금은 없다 (2026-11-14). 섬 금지: 열린 칸과 맞닿은 배치만.
    /// </summary>
    public sealed class SiteUnlocker : MonoBehaviour
    {
        // 잠긴 칸에서 본 host(열린 칸)의 위치 — 위 · 왼쪽 · 오른쪽 · 아래 순으로 우선 (x = 열, y = 행)
        static readonly Vector2Int[] HostSides = { new(0, -1), new(-1, 0), new(1, 0), new(0, 1) };
        static readonly float[] EdgeOffsets = { 0f, -2.5f, 2.5f, -3.5f, 3.5f };

        [SerializeField] ConstructionFence fencePrefab;
        [SerializeField] ExpansionPad padPrefab;
        [SerializeField, Min(0.5f), Tooltip("칸 경계에서 발판 중심까지 (m)")] float edgeInset = 1.4f;
        [SerializeField, Tooltip("발판 반 크기 + 여유 (다른 존과 겹침 확인용)")] Vector2 padHalfExtents = new(1.2f, 1.2f);

        readonly Dictionary<string, List<ConstructionFence>> fences = new();
        readonly Dictionary<string, ExpansionPad> pads = new();
        SiteGrid grid;
        SiteLayout layout;

        void Start()
        {
            grid = GetComponent<SiteGrid>();
            layout = GameManager.Instance.Database.Site;

            GameManager.Instance.Level.Level.Subscribe(_ => Refresh()).AddTo(this); // 구독하자마자 한 번 불린다
            grid.OnUnlocked.Subscribe(_ => Refresh()).AddTo(this);                  // 발판 구매 · 디버그 해금
            grid.OnMoved.Subscribe(_ => Refresh()).AddTo(this);                     // 모듈 재배치 → 발판 자리 (GDD 4.4)
        }

        public void Refresh()
        {
            var level = GameManager.Instance.Level.Level.CurrentValue;
            var open = new List<SiteLayout.ModulePlacement>();
            var nextLevel = int.MaxValue;
            foreach (var placement in LockedRoots())
            {
                if (!IsAdjacentToOpen(placement)) continue;
                open.Add(placement);
                if (placement.unlockLevel > level) nextLevel = Mathf.Min(nextLevel, placement.unlockLevel);
            }

            // 울타리 = 잠긴 칸이 있는 배치 중 레벨이 된 것 + 다음 해금 레벨 것 모두, 발판 = 레벨이 된 것 모두 (GDD 9.3, 같은 레벨 여러 개)
            var fenceIds = new HashSet<string>();
            var padTargets = new Dictionary<string, SiteLayout.ModulePlacement>();
            foreach (var placement in open)
            {
                var reached = placement.unlockLevel <= level;
                if (reached) padTargets[placement.id] = placement;
                if ((reached || placement.unlockLevel == nextLevel) && !AllCellsOpen(placement)) fenceIds.Add(placement.id);
            }
            RefreshFences(fenceIds);
            RefreshPads(padTargets);
        }

        /// <summary>묶음의 칸이 모두 이미 열려 있는가 (= 빈 공간 칸에 놓이는 해금 모듈, GDD 4.2).</summary>
        bool AllCellsOpen(SiteLayout.ModulePlacement root)
        {
            foreach (var id in layout.GetUnlockGroup(root.id))
            {
                if (!layout.TryGetPlacement(id, out var member)) continue;
                foreach (var cell in SiteLayout.GetCells(member))
                {
                    if (!grid.IsCellOpen(cell)) return false;
                }
            }
            return true;
        }

        /// <summary>잠긴 칸이 있는 구역에만 울타리 (빈 공간 칸 해금은 울타리 없이 발판만). 구역마다 표지 하나.</summary>
        void RefreshFences(HashSet<string> targetIds)
        {
            foreach (var id in new List<string>(fences.Keys))
            {
                if (targetIds.Contains(id)) continue;
                ClearFences(fences[id]);
                fences.Remove(id);
            }
            if (fencePrefab == null) return;
            foreach (var rootId in targetIds)
            {
                if (fences.ContainsKey(rootId) || !layout.TryGetPlacement(rootId, out var root)) continue;
                var list = new List<ConstructionFence>();
                var first = true;
                foreach (var id in layout.GetUnlockGroup(rootId))
                {
                    if (!layout.TryGetPlacement(id, out var member)) continue;
                    foreach (var cell in SiteLayout.GetCells(member))
                    {
                        var fence = Instantiate(fencePrefab, layout.CellCenter(cell.y, cell.x), Quaternion.identity, transform);
                        fence.name = $"Fence_{rootId}";
                        fence.SetLevel(root.unlockLevel, first); // 표지는 구역마다 한 곳에만
                        first = false;
                        list.Add(fence);
                        FacilityPop.Play(fence.transform);
                    }
                }
                fences[rootId] = list;
            }
        }

        /// <summary>
        /// 레벨이 된 해금 배치마다 발판을 놓는다. 대상에서 빠진 배치(해금됨)의 발판은 지운다.
        /// 빈 공간 칸에 놓이는 모듈이면 그 칸 중앙, 잠긴 칸이면 울타리 옆 열린 칸 가장자리 (GDD 4.2).
        /// </summary>
        void RefreshPads(Dictionary<string, SiteLayout.ModulePlacement> targets)
        {
            foreach (var id in new List<string>(pads.Keys))
            {
                if (targets.ContainsKey(id)) continue;
                var old = pads[id];
                if (old != null && old.enabled) Destroy(old.gameObject); // 구매 완료한 발판은 enabled = false로 스스로 사라지는 중
                pads.Remove(id);
            }
            if (padPrefab == null) return;

            foreach (var placement in targets.Values)
            {
                var onEmptyCell = AllCellsOpen(placement);
                if (pads.TryGetValue(placement.id, out var pad) && pad != null)
                {
                    // 빈 공간 칸 발판: 재배치로 그 칸이 찼으면 가장 가까운 빈 공간으로 옮긴다 (채운 돈 유지, GDD 4.4). 빈 공간이 없으면 숨김
                    if (!onEmptyCell || !pad.enabled) continue;
                    var found = grid.TryFindEmptyCell(grid.WorldToCell(pad.transform.position), out var padCell);
                    pad.gameObject.SetActive(found);
                    if (found) pad.transform.position = grid.CellCenter(padCell);
                    continue;
                }

                Vector3 position;
                if (onEmptyCell)
                {
                    // 처음 놓일 칸이 빈 공간이면 그 칸 중앙, 재배치로 차 있으면 가장 가까운 빈 공간 (GDD 4.2). SiteGrid.Attach도 같은 칸을 고른다
                    // shortcut: 빈 공간 칸 발판 두 개가 같은 빈 칸을 고르면 겹친다 — 재배치로 빈 칸이 모자랄 때만, 생기면 다른 발판 칸을 빼고 찾기
                    if (!grid.TryFindEmptyCell(SiteLayout.GetCells(placement)[0], out var cell)) continue;
                    position = grid.CellCenter(cell);
                }
                else if (!TryFindPadPosition(placement, out position)) continue;

                pad = Instantiate(padPrefab, position, Quaternion.identity, transform);
                pad.name = $"ExpansionPad_{placement.id}";
                pad.Setup(placement.id, layout.GetDisplayName(placement.id), placement.cost);
                pads[placement.id] = pad;
                FacilityPop.Play(pad.transform);
            }
        }

        static void ClearFences(List<ConstructionFence> list)
        {
            foreach (var fence in list)
            {
                if (fence == null) continue;
                var t = fence.transform;
                t.DOKill();
                t.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetLink(fence.gameObject).OnComplete(() => Destroy(t.gameObject));
            }
        }

        /// <summary>아직 잠긴 대표 배치 (묶인 배치 · 시작 부지 제외).</summary>
        IEnumerable<SiteLayout.ModulePlacement> LockedRoots()
        {
            foreach (var placement in layout.Modules)
            {
                if (placement.IsLinked || layout.IsStartOpen(placement) || placement.module == null || grid.IsUnlocked(placement.id)) continue;
                yield return placement;
            }
        }

        /// <summary>묶음의 칸 중 하나라도 열린 칸과 상하좌우로 맞닿아 있는가.</summary>
        bool IsAdjacentToOpen(SiteLayout.ModulePlacement root)
        {
            foreach (var id in layout.GetUnlockGroup(root.id))
            {
                if (!layout.TryGetPlacement(id, out var member)) continue;
                foreach (var cell in SiteLayout.GetCells(member))
                {
                    if (grid.IsCellOpen(cell + Vector2Int.up) || grid.IsCellOpen(cell + Vector2Int.down)
                        || grid.IsCellOpen(cell + Vector2Int.left) || grid.IsCellOpen(cell + Vector2Int.right)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 발판 자리: 묶음의 칸과 맞닿은 열린 칸(host)의, 잠긴 칸 쪽 경계 바로 안쪽. 다른 존 · 선반과 겹치지 않는 곳을 고른다.
        /// 다 겹치면 첫 후보. 맞닿은 열린 칸이 없으면 false.
        /// </summary>
        bool TryFindPadPosition(SiteLayout.ModulePlacement root, out Vector3 position)
        {
            var groupCells = new HashSet<Vector2Int>();
            foreach (var id in layout.GetUnlockGroup(root.id))
            {
                if (layout.TryGetPlacement(id, out var member)) groupCells.UnionWith(SiteLayout.GetCells(member));
            }

            position = default;
            var found = false;
            var half = layout.CellSize * 0.5f - edgeInset;
            foreach (var side in HostSides)
            {
                foreach (var cell in groupCells)
                {
                    var host = cell + side;
                    if (groupCells.Contains(host) || !grid.IsCellOpen(host)) continue;

                    var center = layout.CellCenter(host.y, host.x);
                    var normal = new Vector3(-side.x, 0f, side.y); // host → 잠긴 칸 (행이 늘면 −z)
                    var along = new Vector3(normal.z, 0f, normal.x);
                    foreach (var offset in EdgeOffsets)
                    {
                        var candidate = center + normal * half + along * offset;
                        if (!found)
                        {
                            position = candidate;
                            found = true;
                        }
                        if (Overlaps(candidate)) continue;
                        position = candidate;
                        return true;
                    }
                }
            }
            return found;
        }

        bool Overlaps(Vector3 center)
        {
            var rect = new Rect(center.x - padHalfExtents.x, center.z - padHalfExtents.y, padHalfExtents.x * 2f, padHalfExtents.y * 2f);
            if (InteractionZone.AnyOverlap(rect)) return true;
            foreach (var slot in ShelfSlot.All)
            {
                if (slot.Occupant != null && slot.WorldFootprint.Overlaps(rect)) return true;
            }
            return false;
        }
    }
}
