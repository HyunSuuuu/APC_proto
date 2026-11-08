using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Interaction;
using APC.Logistics;
using R3;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 확장 칸 해금 패드를 관리한다 (GDD 4.2, 9.2, 9.3). SiteGrid 오브젝트에 붙는다.
    /// 잠긴 대표 배치마다 ① 해금 레벨이 됐고 ② 묶음이 열린 칸과 맞닿아 있으면, 맞닿은 열린 칸의 경계 바로 안쪽에 패드를 팝 연출로 놓는다.
    /// 레벨 전에는 패드가 없다 (보이지 않음). 레벨업 · 해금마다 다시 확인한다.
    /// </summary>
    public sealed class ExpansionPads : MonoBehaviour
    {
        // 잠긴 칸에서 본 host(열린 칸)의 위치 — 위 · 왼쪽 · 오른쪽 · 아래 순으로 우선 (x = 열, y = 행)
        static readonly Vector2Int[] HostSides = { new(0, -1), new(-1, 0), new(1, 0), new(0, 1) };
        static readonly float[] EdgeOffsets = { 0f, -2.5f, 2.5f, -3.5f, 3.5f };

        [SerializeField] ExpansionPad padPrefab;
        [SerializeField, Min(0.5f), Tooltip("칸 경계에서 패드 중심까지 (m)")] float edgeInset = 1.4f;
        [SerializeField, Tooltip("패드가 차지하는 반 크기 + 여유 (다른 존과 겹침 확인용)")] Vector2 padHalfExtents = new(1.2f, 1.2f);

        readonly Dictionary<string, ExpansionPad> pads = new();
        SiteGrid grid;
        SiteLayout layout;

        /// <summary>지금 놓여 있는 패드 (테스트 · 디버그용).</summary>
        public IReadOnlyDictionary<string, ExpansionPad> Pads => pads;

        void Start()
        {
            grid = GetComponent<SiteGrid>();
            var gm = GameManager.Instance;
            layout = gm.Database.Site;

            grid.OnUnlocked.Subscribe(_ => Refresh()).AddTo(this);
            gm.Level.LeveledUp.Subscribe(_ => Refresh()).AddTo(this);
            Refresh();
        }

        public void Refresh()
        {
            // 해금된 배치의 패드 정리. 패드로 해금하면 패드가 스스로 사라지지만, 디버그 메뉴 등으로 바로 해금하면 여기서 지운다.
            var stale = new List<string>();
            foreach (var pair in pads)
            {
                if (pair.Value == null || grid.IsUnlocked(pair.Key)) stale.Add(pair.Key);
            }
            foreach (var id in stale)
            {
                var pad = pads[id];
                if (pad != null && pad.enabled) Destroy(pad.gameObject); // 구매 완료한 패드는 enabled = false로 사라지는 중
                pads.Remove(id);
            }

            var level = GameManager.Instance.Level.Level.CurrentValue;
            foreach (var placement in layout.Modules)
            {
                if (placement.IsLinked || layout.IsStartOpen(placement) || grid.IsUnlocked(placement.id) || pads.ContainsKey(placement.id)) continue;
                if (level < placement.unlockLevel || placement.module == null) continue;
                if (!TryFindPadPosition(placement, out var position)) continue; // 열린 칸과 맞닿지 않음 (섬 금지)

                var pad = Instantiate(padPrefab, position, Quaternion.identity, transform);
                pad.name = $"ExpansionPad_{placement.id}";
                pad.Setup(placement.id, layout.GetDisplayName(placement.id), placement.cost);
                pads[placement.id] = pad;
                FacilityPop.Play(pad.transform);
            }
        }

        /// <summary>
        /// 패드 자리: 묶음의 칸과 맞닿은 열린 칸(host)의, 잠긴 칸 쪽 경계 바로 안쪽. 다른 존 · 선반 자리와 겹치지 않는 곳을 고른다.
        /// 맞닿은 열린 칸이 없으면 false.
        /// </summary>
        bool TryFindPadPosition(SiteLayout.ModulePlacement root, out Vector3 position)
        {
            var groupCells = new HashSet<Vector2Int>();
            foreach (var id in layout.GetUnlockGroup(root.id))
            {
                if (layout.TryGetPlacement(id, out var member)) groupCells.UnionWith(SiteLayout.GetCells(member));
            }

            // 후보: (열린 host 칸, host → 잠긴 칸 방향). 위쪽 host가 먼저.
            var candidates = new List<(Vector2Int host, Vector2Int toward)>();
            foreach (var side in HostSides)
            {
                foreach (var cell in groupCells)
                {
                    var host = cell + side;
                    if (groupCells.Contains(host) || !grid.IsCellOpen(host)) continue;
                    if (root.padCell.y >= 1 && host != root.padCell) continue; // config에서 지정한 칸만
                    candidates.Add((host, -side));
                }
            }

            position = default;
            if (candidates.Count == 0) return false;

            var half = layout.CellSize * 0.5f - edgeInset;
            var first = true;
            foreach (var (host, toward) in candidates)
            {
                var center = layout.CellCenter(host.y, host.x);
                var normal = new Vector3(toward.x, 0f, -toward.y);   // 행이 늘면 −z
                var along = new Vector3(normal.z, 0f, normal.x);      // 경계를 따라가는 방향
                foreach (var offset in EdgeOffsets)
                {
                    var candidate = center + normal * half + along * offset;
                    if (first)
                    {
                        position = candidate; // 다 겹치면 첫 후보
                        first = false;
                    }
                    if (!Overlaps(candidate))
                    {
                        position = candidate;
                        return true;
                    }
                }
            }
            return true;
        }

        bool Overlaps(Vector3 center)
        {
            var rect = new Rect(center.x - padHalfExtents.x, center.z - padHalfExtents.y, padHalfExtents.x * 2f, padHalfExtents.y * 2f);
            if (InteractionZone.AnyOverlap(rect)) return true; // 다른 패드도 존이라 여기서 걸린다
            foreach (var slot in ShelfSlot.All)
            {
                // 선반이 선 자리만 (회사마다 자기 구역이라 빈 자리는 계속 비어 있다)
                if (slot.Occupant != null && slot.WorldFootprint.Overlaps(rect)) return true;
            }
            return false;
        }
    }
}
