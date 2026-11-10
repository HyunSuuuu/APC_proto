using System.Collections.Generic;
using APC.Core;
using APC.Data;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 레벨로 부지를 확장한다 (GDD 4.1, 9.3). SiteGrid 오브젝트에 붙는다.
    /// ① 잠긴 대표 배치가 해금 레벨이 됐고 열린 칸과 맞닿아 있으면(섬 금지) 바로 해금한다 — 비용 · 패드 없음.
    /// ② 다음에 해금될 구역 1곳(맞닿은 잠긴 배치 중 해금 레벨이 가장 낮은 것)을 공사장 울타리 + `잠금해제 : LV N`으로 보여준다.
    /// </summary>
    public sealed class SiteUnlocker : MonoBehaviour
    {
        [SerializeField] ConstructionFence fencePrefab;

        readonly List<ConstructionFence> fences = new();
        SiteGrid grid;
        SiteLayout layout;
        string fencedId;

        /// <summary>지금 울타리가 쳐진 배치 id (없으면 null).</summary>
        public string FencedId => fencedId;

        void Start()
        {
            grid = GetComponent<SiteGrid>();
            layout = GameManager.Instance.Database.Site;

            GameManager.Instance.Level.Level.Subscribe(_ => Refresh()).AddTo(this); // 구독하자마자 한 번 불린다
            grid.OnUnlocked.Subscribe(_ => RefreshFence()).AddTo(this);             // 디버그 해금 등
        }

        public void Refresh()
        {
            var level = GameManager.Instance.Level.Level.CurrentValue;

            // 해금되면 새로 맞닿는 칸이 생기므로 더 열 것이 없을 때까지 반복한다.
            var opened = true;
            while (opened)
            {
                opened = false;
                foreach (var placement in LockedRoots())
                {
                    if (placement.unlockLevel > level || !IsAdjacentToOpen(placement)) continue;
                    if (grid.Unlock(placement.id)) opened = true;
                }
            }
            RefreshFence();
        }

        void RefreshFence()
        {
            // 다음 해금 구역: 열린 칸과 맞닿은 잠긴 대표 배치 중 해금 레벨이 가장 낮은 것
            SiteLayout.ModulePlacement? next = null;
            foreach (var placement in LockedRoots())
            {
                if (!IsAdjacentToOpen(placement)) continue;
                if (next == null || placement.unlockLevel < next.Value.unlockLevel) next = placement;
            }

            var nextId = next?.id;
            if (nextId == fencedId) return;

            ClearFences();
            fencedId = nextId;
            if (next == null || fencePrefab == null) return;

            var first = true;
            foreach (var id in layout.GetUnlockGroup(next.Value.id))
            {
                if (!layout.TryGetPlacement(id, out var member)) continue;
                foreach (var cell in SiteLayout.GetCells(member))
                {
                    var fence = Instantiate(fencePrefab, layout.CellCenter(cell.y, cell.x), Quaternion.identity, transform);
                    fence.name = $"Fence_{next.Value.id}";
                    fence.SetLevel(next.Value.unlockLevel, first); // 표지는 한 곳에만
                    first = false;
                    fences.Add(fence);
                    FacilityPop.Play(fence.transform);
                }
            }
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
    }
}
