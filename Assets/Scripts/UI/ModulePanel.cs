using System.Collections.Generic;
using APC.Core;
using APC.Logistics;
using APC.Site;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 계약 PC 팝업의 `모듈` 탭 (GDD 4.4): 6 × 4 부지 미니맵에서 하차 · 보관 · 반품 칸을 끌어다 놓아 자리를 바꾸고, 칸을 눌러 고른 뒤 [회전]으로 90°씩 돌린다.
    /// 빈 공간에 놓으면 이동, 하차 · 보관 · 반품에 놓으면 서로 교체, 그 밖(고정 모듈 · 잠긴 칸 · 바깥)은 실패(흔들림 + 사유).
    /// 하차 모듈은 트럭이 들어오는 변(모듈 로컬 −x)이 **부지 바깥**을 보는 자리 · 방향만 된다 (M20-3).
    /// 바꾼 내용은 미니맵에만 쌓아 두고 **팝업을 닫을 때 한꺼번에** SiteGrid에 적용한다 (ContractPopup이 Begin · Apply를 부름).
    /// [되돌리기] = 팝업을 열었을 때 배치로.
    /// 옮기거나 돌린 모듈(맞바꾸는 상대 포함)에 걸친 컨베이어 벨트는 옮길 때 안내하고, 적용할 때 철거 · 전액 환불 (GDD 7.10, M18-4).
    /// </summary>
    public sealed class ModulePanel : MonoBehaviour
    {
        const int Columns = 6;
        const int Rows = 4;

        [SerializeField, Tooltip("칸 템플릿 (꺼진 채). 위치 = (1,1) 칸")] ModuleCell cellTemplate;
        [SerializeField, Tooltip("칸 간격 (px)")] Vector2 cellStep = new(210f, 160f);
        [SerializeField] Button resetButton;
        [SerializeField, Tooltip("[회전]. 비워 두면 [되돌리기]를 복사해 왼쪽에 만든다")] Button rotateButton;
        [SerializeField] TMP_Text reasonLabel;

        [Header("색")]
        [SerializeField] Color emptyColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color fixedColor = new(0.38f, 0.35f, 0.33f);
        [SerializeField] Color returnsColor = new(0.55f, 0.62f, 0.8f);
        [SerializeField] Color inboundColor = new(0.85f, 0.62f, 0.35f);
        [SerializeField] Color scanColor = new(0.45f, 0.7f, 0.62f);
        [SerializeField] Color lockedColor = new(0.55f, 0.55f, 0.55f, 0.45f);
        [SerializeField] Color darkText = new(0.25f, 0.22f, 0.2f);
        [SerializeField, Tooltip("고른 칸 크기")] float selectedScale = 1.08f;
        [SerializeField, Tooltip("하차장 주차장 쪽 붉은 바 색")] Color truckBarColor = new(0.85f, 0.2f, 0.18f);
        [SerializeField, Min(1f), Tooltip("붉은 바 두께 (px, 칸 바깥으로)")] float truckBarThickness = 14f;

        // 모듈 로컬 −x(트럭 쪽)가 회전 0/90/180/270에서 가리키는 칸 방향 (x = 열, y = 행) — SiteLayout 회전 규칙: 0 = 서, 90 = 북, 180 = 동, 270 = 남
        static readonly Vector2Int[] TruckSide = { Vector2Int.left, Vector2Int.down, Vector2Int.right, Vector2Int.up };

        readonly Dictionary<Vector2Int, ModuleCell> cells = new();
        readonly Dictionary<Vector2Int, SiteModule> staged = new();
        readonly Dictionary<Vector2Int, SiteModule> original = new();
        readonly Dictionary<SiteModule, int> stagedYaw = new();
        readonly Dictionary<SiteModule, int> originalYaw = new();
        readonly HashSet<Vector2Int> siteCells = new();
        Vector2Int? selected;
        Tween reasonTween;

        void Awake()
        {
            EnsureCells();
            resetButton.onClick.AddListener(ResetArrangement);
            if (rotateButton == null)
            {
                // shortcut: [되돌리기] 복사 — 씬에서 버튼 위치 · 모양을 정하면(GDD 4.4 `임시`) rotateButton에 연결
                rotateButton = Instantiate(resetButton, resetButton.transform.parent);
                rotateButton.name = "RotateButton";
                rotateButton.onClick.RemoveAllListeners();
                var rect = (RectTransform)rotateButton.transform;
                rect.anchoredPosition -= new Vector2(rect.rect.width + 20f, 0f);
                var label = rotateButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "회전";
            }
            rotateButton.onClick.AddListener(Rotate);
        }

        /// <summary>팝업을 열 때: 지금 부지 배치를 미니맵에 옮겨 둔다.</summary>
        public void Begin()
        {
            var grid = SiteGrid.Instance;
            if (grid == null) return;
            EnsureCells();
            original.Clear();
            originalYaw.Clear();
            foreach (var cell in cells.Keys)
            {
                var module = grid.ModuleAt(cell);
                original[cell] = module;
                if (module != null) originalYaw[module] = SiteGrid.YawOf(module);
            }
            ResetArrangement();
            reasonLabel.gameObject.SetActive(false);
        }

        /// <summary>팝업을 닫을 때: 미니맵 배치를 실제 부지에 적용한다 (바뀐 모듈만 옮겨지거나 돌아감).</summary>
        public void Apply()
        {
            var grid = SiteGrid.Instance;
            if (grid == null || staged.Count == 0) return;
            var arrangement = new Dictionary<SiteModule, (Vector2Int, int)>();
            foreach (var pair in staged)
            {
                if (pair.Value != null) arrangement[pair.Value] = (pair.Key, stagedYaw[pair.Value]);
            }
            foreach (var belt in BeltsOnMoved()) // 옮기기 전에: 위 물건은 Start로(모듈과 같이 옮겨짐), 전액 환불
            {
                GameManager.Instance.Wallet.Add(belt.Cost);
                belt.Dismantle();
            }
            grid.ApplyArrangement(arrangement);
        }

        /// <summary>source 칸을 target 칸에 놓았다 (ModuleCell). target이 null이면 미니맵 밖.</summary>
        public void Drop(ModuleCell source, ModuleCell target)
        {
            if (target == source)
            {
                Select(source);
                return;
            }
            if (target == null)
            {
                Fail(source, "여기에는 놓을 수 없어요");
                return;
            }

            var moving = staged[source.Cell];
            staged.TryGetValue(target.Cell, out var other);
            if (other == null) Fail(source, "잠긴 칸에는 놓을 수 없어요");
            else if (!CanMove(other)) Fail(source, "옮길 수 없는 칸이에요");
            else if (!TryFitInbound(moving, target.Cell, out var movingYaw) || !TryFitInbound(other, source.Cell, out var otherYaw))
                Fail(source, "하차장은 트럭이 들어올 바깥 테두리 칸에만 놓을 수 있어요");
            else
            {
                staged[target.Cell] = moving; // 빈 공간이면 이동, 하차 · 보관 · 반품이면 교체 (둘 다 자리 맞바꿈)
                staged[source.Cell] = other;
                stagedYaw[moving] = movingYaw;
                stagedYaw[other] = otherYaw;
                selected = target.Cell;
                Render();
                WarnBelts();
                Punch(target.Cell);
            }
        }

        /// <summary>칸을 눌렀다: 옮길 수 있는 모듈이면 회전할 칸으로 고른다.</summary>
        public void Select(ModuleCell cell)
        {
            staged.TryGetValue(cell.Cell, out var module);
            selected = module != null && module.Kind != SiteModule.ModuleKind.Empty && CanMove(module) ? cell.Cell : null;
            Render();
        }

        /// <summary>[회전]: 고른 모듈을 시계 방향 90°. 하차 모듈은 트럭 쪽이 바깥인 다음 방향으로 (없으면 흔들림 + 사유).</summary>
        void Rotate()
        {
            if (selected is not { } cell || !staged.TryGetValue(cell, out var module) || module == null) return;
            var yaw = stagedYaw[module];
            for (var step = 1; step < 4; step++)
            {
                var next = (yaw + step * 90) % 360;
                if (IsInbound(module) && !TruckSideOutside(cell, next)) continue;
                stagedYaw[module] = next;
                Render();
                WarnBelts();
                Punch(cell);
                return;
            }
            Fail(cells[cell], "트럭이 들어올 바깥 변이 하나뿐이에요");
        }

        void ResetArrangement()
        {
            staged.Clear();
            foreach (var pair in original) staged[pair.Key] = pair.Value;
            stagedYaw.Clear();
            foreach (var pair in originalYaw) stagedYaw[pair.Key] = pair.Value;
            selected = null;
            Render();
        }

        /// <summary>끌 수 있는 모듈: 보관 · 반품(Movable), 빈 공간(놓을 자리), 하차장 (M20-3).</summary>
        static bool CanMove(SiteModule module) => module.Kind != SiteModule.ModuleKind.Fixed || IsInbound(module);

        static bool IsInbound(SiteModule module) => module != null && module.GetComponentInChildren<InboundBay>(true) != null;

        /// <summary>
        /// 하차장이 cell에 놓일 방향. 지금 방향이 되면 그대로(GDD 4.4 방향 유지), 안 되면 시계 방향으로 되는 첫 방향 —
        /// 그대로만 두면 서쪽 하차장을 북쪽 테두리로 못 옮긴다 `임시`. 하차장이 아니면 방향 그대로 true.
        /// </summary>
        bool TryFitInbound(SiteModule module, Vector2Int cell, out int yaw)
        {
            yaw = stagedYaw[module];
            if (!IsInbound(module)) return true;
            for (var step = 0; step < 4; step++)
            {
                var candidate = (yaw + step * 90) % 360;
                if (!TruckSideOutside(cell, candidate)) continue;
                yaw = candidate;
                return true;
            }
            return false;
        }

        /// <summary>그 칸에서 yaw 방향 트럭 쪽 이웃 칸이 부지 밖인가 (잠긴 칸도 부지 안으로 친다).</summary>
        bool TruckSideOutside(Vector2Int cell, int yaw) => !siteCells.Contains(cell + TruckSide[yaw / 90 % 4]);

        void WarnBelts()
        {
            var belts = BeltsOnMoved().Count;
            if (belts > 0) ShowReason($"연결된 벨트 {belts}개가 철거됩니다 (전액 환불)", 2.5f);
        }

        void Punch(Vector2Int cell)
        {
            var rect = cells[cell].Rect;
            rect.DOKill(true);
            rect.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
        }

        void Fail(ModuleCell source, string reason)
        {
            Render(); // 제자리로
            source.Rect.DOKill(true);
            source.Rect.DOShakeAnchorPos(0.4f, new Vector2(18f, 0f), 20, 0f).SetUpdate(true).SetLink(gameObject);

            ShowReason(reason, 1.5f);
        }

        void ShowReason(string text, float hold)
        {
            reasonTween?.Kill();
            reasonLabel.text = text;
            reasonLabel.alpha = 1f;
            reasonLabel.gameObject.SetActive(true);
            reasonTween = reasonLabel.DOFade(0f, 0.4f).SetDelay(hold).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>지금 미니맵 배치대로 하면 자리나 방향이 바뀌는 모듈에 걸친 벨트 (Start · End가 있거나 경로가 지나감).</summary>
        List<ConveyorBelt> BeltsOnMoved()
        {
            var result = new List<ConveyorBelt>();
            var grid = SiteGrid.Instance;
            if (grid == null || ConveyorBelt.All.Count == 0) return result;
            var moved = new List<SiteModule>();
            foreach (var pair in staged)
            {
                if (pair.Value == null) continue;
                if (grid.WorldToCell(pair.Value.transform.position) != pair.Key || SiteGrid.YawOf(pair.Value) != stagedYaw[pair.Value]) moved.Add(pair.Value);
            }
            foreach (var belt in ConveyorBelt.All)
            {
                if (moved.Exists(belt.Touches)) result.Add(belt);
            }
            return result;
        }

        void EnsureCells()
        {
            if (cells.Count > 0) return;
            var layout = GameManager.Instance.Database.Site;
            foreach (var placement in layout.Modules) siteCells.UnionWith(Data.SiteLayout.GetCells(placement));

            var origin = cellTemplate.Rect.anchoredPosition;
            for (var row = 1; row <= Rows; row++)
            for (var column = 1; column <= Columns; column++)
            {
                var cell = Instantiate(cellTemplate, cellTemplate.transform.parent);
                cell.name = $"Cell_R{row}C{column}";
                cell.Cell = new Vector2Int(column, row);
                cell.Rect.anchoredPosition = origin + new Vector2((column - 1) * cellStep.x, -(row - 1) * cellStep.y);
                cells[cell.Cell] = cell;
            }
        }

        void Render()
        {
            var grid = SiteGrid.Instance;
            var layout = GameManager.Instance.Database.Site;
            var origin = cellTemplate.Rect.anchoredPosition;
            foreach (var pair in cells)
            {
                var view = pair.Value;
                var cell = pair.Key;
                view.gameObject.SetActive(siteCells.Contains(cell));
                view.Rect.anchoredPosition = origin + new Vector2((cell.x - 1) * cellStep.x, -(cell.y - 1) * cellStep.y);
                view.Rect.localScale = Vector3.one * (selected == cell ? selectedScale : 1f);
                staged.TryGetValue(cell, out var module);
                view.Draggable = module != null && module.Kind != SiteModule.ModuleKind.Empty && CanMove(module);
                if (module == null)
                {
                    view.Show("잠김", lockedColor, Color.white);
                    continue;
                }

                var id = grid != null ? grid.IdOf(module) : null;
                var name = id != null ? layout.GetDisplayName(id) : module.name;
                var inbound = IsInbound(module);
                view.ShowTruckSide(inbound ? TruckSide[stagedYaw[module] / 90 % 4] : null, truckBarColor, truckBarThickness);
                if (inbound)
                {
                    view.Show(name, inboundColor, darkText);
                    continue;
                }
                switch (module.Kind)
                {
                    case SiteModule.ModuleKind.Empty:
                        view.Show(name, emptyColor, darkText);
                        break;
                    case SiteModule.ModuleKind.Fixed:
                        view.Show(name, fixedColor, Color.white);
                        break;
                    default:
                        var color = StoreColor(id);
                        view.Show(name, color, color.grayscale < 0.5f ? Color.white : darkText); // 어두운 회사 색 위에는 흰 글자
                        break;
                }
            }
            rotateButton.interactable = selected != null;
        }

        /// <summary>보관 = 그 회사 품목 띠 색 (id `Store` + 회사 id), 스캔 = 스캔 색, 반품 = 반품 색.</summary>
        Color StoreColor(string id)
        {
            if (id == "Scan") return scanColor;
            if (id == null || !id.StartsWith("Store") || id.Length < 6) return returnsColor;
            var companyId = id.Substring(5, 1);
            foreach (var company in GameManager.Instance.Database.Companies)
            {
                if (company.Id == companyId && company.Items.Count > 0) return company.Items[0].TapeColor;
            }
            return returnsColor;
        }
    }
}
