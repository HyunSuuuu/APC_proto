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
    /// 계약 PC 팝업의 `모듈` 탭 (GDD 4.4): 6 × 4 부지 미니맵에서 하차 · 보관 · 반품 · 스캔 칸을 끌어다 놓거나, 눌러 고른 뒤 다른 칸을 눌러 자리를 바꾼다.
    /// 고른 모듈은 [회전]으로 90°씩 돌린다. 빈 공간에 놓으면 이동, 옮길 수 있는 모듈에 놓으면 서로 교체, 그 밖(고정 · 잠긴 칸 · 부지 밖)은 실패(흔들림 + 사유).
    /// 하차 모듈은 트럭이 들어오는 변(모듈 로컬 −x)이 **부지 바깥**을 보는 자리 · 방향만 된다 (M20-3).
    /// 바꾼 내용은 미니맵에만 쌓아 두고 **[적용]을 누르면** SiteGrid에 적용한다. 적용하지 않고 닫으면 확인창(ConfirmDiscard) → [확인]이면 버린다 (2026-11-26).
    /// [되돌리기] = 마지막으로 적용한(또는 팝업을 열었을 때) 배치로.
    /// 옮기거나 돌린 모듈(맞바꾸는 상대 포함)에 걸친 컨베이어 벨트는 옮길 때 안내하고, 적용할 때 철거 · 전액 환불 (GDD 7.10, M18-4).
    /// 화면 구성(시안 module_placement_ui_mobile, 가로 · 세로)은 ModulePanel.Layout.cs.
    /// </summary>
    public sealed partial class ModulePanel : MonoBehaviour
    {
        const int Columns = 6;
        const int Rows = 4;

        [SerializeField, Tooltip("칸 템플릿 (꺼진 채). 위치 = (1,1) 칸")] ModuleCell cellTemplate;
        [SerializeField, Tooltip("칸 간격 (px)")] Vector2 cellStep = new(210f, 160f);
        [SerializeField] Button resetButton;
        [SerializeField, Tooltip("옛 [회전] 버튼 (있으면 숨김 — [회전]은 설명 카드 안)")] Button rotateButton;
        [SerializeField, Tooltip("[배치 적용]. 비워 두면 [되돌리기]를 복사해 만든다 (자리 · 색은 Layout)")] Button applyButton;
        [SerializeField] TMP_Text reasonLabel;

        [Header("색")]
        [SerializeField] Color emptyColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color fixedColor = new(0.38f, 0.35f, 0.33f);
        [SerializeField] Color returnsColor = new(0.55f, 0.62f, 0.8f);
        [SerializeField] Color inboundColor = new(0.85f, 0.62f, 0.35f);
        [SerializeField] Color scanColor = new(0.45f, 0.7f, 0.62f);
        [SerializeField] Color lockedColor = new(0.55f, 0.55f, 0.55f, 0.45f);
        [SerializeField] Color darkText = new(0.25f, 0.22f, 0.2f);
        [SerializeField, Tooltip("고른 칸 크기")] float selectedScale = 1.04f;
        [SerializeField, Tooltip("하차장 주차장 쪽 붉은 바 색")] Color truckBarColor = new(0.85f, 0.2f, 0.18f);
        [SerializeField, Min(1f), Tooltip("붉은 바 두께 (px, 칸 바깥으로)")] float truckBarThickness = 14f;

        // 시안 색 (칸 글자 · 테두리)
        static readonly Color EmptyText = new(0.52f, 0.45f, 0.36f);         // #85725c
        static readonly Color LockedText = new(0.29f, 0.27f, 0.25f);        // #4a4540
        static readonly Color EmptyBorder = new(0.78f, 0.70f, 0.58f);       // #c7b393
        static readonly Color FixedBorder = new(0.25f, 0.22f, 0.21f);       // #3f3935
        static readonly Color LockedBorder = new(0.47f, 0.44f, 0.42f, 0.6f); // stone-500/60
        static readonly Color ModuleBorder = new(0f, 0f, 0f, 0.15f);
        static readonly Color SelectedRing = new(0.96f, 0.62f, 0.04f);      // #f59e0b
        static readonly Color TargetRing = new(0.85f, 0.47f, 0.02f);        // #d97706
        static readonly Color RoadColor = new(0.89f, 0.84f, 0.74f, 0.35f);  // #e3d6bd / 35%
        static readonly Color RoadText = new(0.70f, 0.62f, 0.49f, 0.6f);    // #b39d7c / 60%
        static readonly Color RoadBorder = new(0.80f, 0.72f, 0.59f, 0.45f); // #cbb896 / 45%

        // 모듈 로컬 −x(트럭 쪽)가 회전 0/90/180/270에서 가리키는 칸 방향 (x = 열, y = 행) — SiteLayout 회전 규칙: 0 = 서, 90 = 북, 180 = 동, 270 = 남
        static readonly Vector2Int[] TruckSide = { Vector2Int.left, Vector2Int.down, Vector2Int.right, Vector2Int.up };

        readonly Dictionary<Vector2Int, ModuleCell> cells = new();
        readonly Dictionary<Vector2Int, SiteModule> staged = new();
        readonly Dictionary<Vector2Int, SiteModule> original = new();
        readonly Dictionary<SiteModule, int> stagedYaw = new();
        readonly Dictionary<SiteModule, int> originalYaw = new();
        readonly HashSet<Vector2Int> siteCells = new();
        readonly Dictionary<Vector2Int, Data.SiteLayout.ModulePlacement> cellPlacement = new(); // 잠긴 칸 이름 · 해금 조건
        Vector2Int? selected; // 고른 칸 (아무 칸 — 설명 카드). 옮길 수 있는 모듈이면 다음 탭 = 그 칸으로 이동, [회전] 가능
        Tween reasonTween;
        bool built;

        // 칸 배치: 스크롤 도면 안 (Layout). portrait = 책상 팝업의 화면 방향 플래그
        Transform gridParent;
        Vector2 gridOrigin, gridStep, gridSize;
        bool portrait;
        Transform hint;
        TMP_Text applyLabel;

        // 확인창 (처음 쓸 때 만듦)
        GameObject confirm;
        System.Action onDiscard;

        void Awake() => Build();

        /// <summary>
        /// [배치 적용] · 화면 구성 UI를 만든다 (한 번만). ContractPopup.Awake가 배치를 기억(CaptureLandscape)하기 전에 부른다.
        /// </summary>
        public void Build()
        {
            if (built) return;
            built = true;
            // shortcut: [되돌리기] 복사 — 씬에서 모양을 정하면 applyButton에 연결 (자리 · 색은 Layout이 정함)
            if (applyButton == null) applyButton = CloneButton(resetButton, transform, "ApplyButton", "배치 적용", null);
            applyLabel = applyButton.GetComponentInChildren<TMP_Text>(true);
            resetButton.onClick.AddListener(ResetArrangement);
            applyButton.onClick.AddListener(ApplyNow);
            hint = transform.Find("Hint");

            BuildSheetUi();
            gridParent = transform; // Layout 전 임시
            gridOrigin = cellTemplate.Rect.anchoredPosition;
            gridStep = cellStep;
            gridSize = cellTemplate.Rect.sizeDelta;
        }

        /// <summary>팝업을 열 때: 지금 부지 배치를 미니맵에 옮겨 둔다.</summary>
        public void Begin()
        {
            var grid = SiteGrid.Instance;
            if (grid == null) return;
            Build();
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
            ScrollToStart();
        }

        /// <summary>적용하지 않은 배치 변경이 있는가 (자리 · 방향).</summary>
        public bool HasChanges => CountChanges() > 0;

        /// <summary>
        /// 적용하지 않은 채 닫으려 할 때 (ContractPopup.Close): "반영되지 않습니다" 확인창. [확인] = 바꾼 배치를 버리고 onConfirm, [취소] = 창만 닫음.
        /// </summary>
        public void ConfirmDiscard(System.Action onConfirm)
        {
            if (confirm == null) BuildConfirm();
            onDiscard = onConfirm;
            confirm.transform.SetAsLastSibling();
            confirm.SetActive(true);
            var box = confirm.transform.GetChild(0);
            box.DOKill(true);
            box.localScale = Vector3.one * 0.85f;
            box.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(confirm);
        }

        /// <summary>[적용]: 미니맵 배치를 실제 부지에 적용하고, 그 배치를 새 기준(되돌리기 대상)으로 삼는다.</summary>
        void ApplyNow()
        {
            var changes = CountChanges();
            if (changes == 0) return;
            Apply();
            Begin();
            ShowReason($"모듈 배치 {changes}건을 적용했어요", 1.5f);
        }

        /// <summary>미니맵 배치를 실제 부지에 적용한다 (바뀐 모듈만 옮겨지거나 돌아감).</summary>
        void Apply()
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

        /// <summary>고른 칸이 옮길 수 있는 모듈인가 (다음 탭 = 이동, [회전] 가능).</summary>
        bool SelectedMovable => selected is { } cell && IsMovableAt(cell);

        bool IsMovableAt(Vector2Int cell) => staged.TryGetValue(cell, out var module) && module != null && module.Kind != SiteModule.ModuleKind.Empty && CanMove(module);

        /// <summary>source 칸을 target 칸에 놓았다 (끌어 놓기 · 고른 뒤 탭). target이 null이면 미니맵 밖.</summary>
        public void Drop(ModuleCell source, ModuleCell target)
        {
            if (target == source)
            {
                Render(); // 제자리 (고른 채)
                return;
            }
            if (target == null || !siteCells.Contains(target.Cell))
            {
                selected = null;
                Fail(source, "여기에는 놓을 수 없어요");
                return;
            }

            var moving = staged[source.Cell];
            staged.TryGetValue(target.Cell, out var other);
            if (other == null) Fail(source, "잠긴 칸에는 놓을 수 없어요");
            else if (!CanMove(other)) Fail(source, "고정 시설에는 놓을 수 없어요");
            else if (!TryFitInbound(moving, target.Cell, out var movingYaw) || !TryFitInbound(other, source.Cell, out var otherYaw))
                Fail(source, "하차장은 트럭이 들어올 바깥 테두리 칸에만 놓을 수 있어요");
            else
            {
                staged[target.Cell] = moving; // 빈 공간이면 이동, 옮길 수 있는 모듈이면 교체 (둘 다 자리 맞바꿈)
                staged[source.Cell] = other;
                stagedYaw[moving] = movingYaw;
                stagedYaw[other] = otherYaw;
                selected = null;
                Render();
                WarnBelts();
                Punch(target.Cell);
            }
        }

        /// <summary>
        /// 칸을 눌렀다 (시안 탭 흐름): 옮길 수 있는 모듈을 고른 상태면 — 같은 칸 = 고르기 취소, 다른 칸 = 그 칸으로 옮기기(Drop).
        /// 아니면 그 칸을 고른다 (설명 카드). 부지 밖(도로) 칸은 무시.
        /// </summary>
        public void Select(ModuleCell cell)
        {
            if (!siteCells.Contains(cell.Cell)) return;
            if (SelectedMovable && selected is { } from)
            {
                if (from == cell.Cell) ClearSelection();
                else Drop(cells[from], cell);
                return;
            }
            selected = cell.Cell;
            Render();
        }

        /// <summary>끌기 시작 (ModuleCell): 그 칸을 고르고 놓을 수 있는 칸을 표시. 칸 자리는 건드리지 않는다 (끄는 칸이 손가락을 따라가는 중).</summary>
        public void BeginDrag(ModuleCell cell)
        {
            selected = cell.Cell;
            RenderMarks();
            RenderPanels(SiteGrid.Instance, GameManager.Instance.Database.Site);
        }

        /// <summary>[취소]: 고르기 해제.</summary>
        void ClearSelection()
        {
            selected = null;
            Render();
        }

        /// <summary>[회전]: 고른 모듈을 시계 방향 90°. 하차 모듈은 트럭 쪽이 바깥인 다음 방향으로 (없으면 흔들림 + 사유).</summary>
        void Rotate()
        {
            if (!SelectedMovable || selected is not { } cell) return;
            var module = staged[cell];
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

        /// <summary>자리나 방향이 바뀐 모듈 수 (빈 공간 제외).</summary>
        int CountChanges()
        {
            var count = 0;
            foreach (var cell in staged.Keys)
            {
                if (IsChanged(cell)) count++;
            }
            return count;
        }

        bool IsChanged(Vector2Int cell)
        {
            if (!staged.TryGetValue(cell, out var module) || module == null || module.Kind == SiteModule.ModuleKind.Empty) return false;
            original.TryGetValue(cell, out var before);
            originalYaw.TryGetValue(module, out var yaw);
            return before != module || stagedYaw[module] != yaw;
        }

        /// <summary>끌 수 있는 모듈: 보관 · 반품 · 스캔(Movable), 빈 공간(놓을 자리), 하차장 (M20-3).</summary>
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
            // 다 사라지면 끈다 — 알파 0만 두면 화면을 돌릴 때 Layout이 색을 다시 칠해 글자가 되살아났다 (2026-11-26)
            reasonTween = reasonLabel.DOFade(0f, 0.4f).SetDelay(hold).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => reasonLabel.gameObject.SetActive(false));
            ShowToastBack(text, hold);
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
            foreach (var placement in layout.Modules)
            {
                foreach (var cell in Data.SiteLayout.GetCells(placement))
                {
                    siteCells.Add(cell);
                    cellPlacement[cell] = placement;
                }
            }

            for (var row = 1; row <= Rows; row++)
            for (var column = 1; column <= Columns; column++)
            {
                var cell = Instantiate(cellTemplate, gridParent);
                cell.name = $"Cell_R{row}C{column}";
                cell.Cell = new Vector2Int(column, row);
                cells[cell.Cell] = cell;
            }
        }

        void Render()
        {
            var grid = SiteGrid.Instance;
            var layout = GameManager.Instance.Database.Site;
            foreach (var pair in cells)
            {
                var view = pair.Value;
                var cell = pair.Key;
                if (view.transform.parent != gridParent) view.transform.SetParent(gridParent, false); // 끌던 칸 · 방향 전환 뒤 제자리 부모로
                var onSite = siteCells.Contains(cell);
                view.gameObject.SetActive(true); // 부지 밖도 흐린 `ROAD` 칸 (시안)
                view.Resize(gridSize);
                view.Rect.anchoredPosition = gridOrigin + new Vector2((cell.x - 1) * gridStep.x, -(cell.y - 1) * gridStep.y);
                view.Rect.localScale = Vector3.one * (selected == cell ? selectedScale : 1f);
                staged.TryGetValue(cell, out var module);
                view.Draggable = IsMovableAt(cell);
                view.ShowTruckSide(module != null && IsInbound(module) ? TruckSide[stagedYaw[module] / 90 % 4] : null, truckBarColor, truckBarThickness);
                if (!onSite) view.Show("<size=60%>ROAD</size>", RoadColor, RoadText);
                else if (module == null) view.Show($"{LockedName(cell, layout)}\n<size=70%>잠김</size>", lockedColor, LockedText);
                else
                {
                    var (name, color, textColor) = Look(module, grid, layout);
                    view.Show(name, color, textColor);
                }
            }
            RenderMarks();

            var changes = CountChanges();
            resetButton.interactable = changes > 0;
            applyButton.interactable = changes > 0;
            RenderPanels(grid, layout);
        }

        /// <summary>칸 테두리: 고른 칸 = 굵은 호박색, 고른 모듈을 놓을 수 있는 칸 = 깜박이는 호박색, 그 밖 = 종류별 얇은 테두리. 바뀐 모듈 = 초록 점.</summary>
        void RenderMarks()
        {
            var movable = SelectedMovable;
            foreach (var pair in cells)
            {
                var cell = pair.Key;
                staged.TryGetValue(cell, out var module);
                var onSite = siteCells.Contains(cell);
                var target = movable && onSite && selected != cell && module != null && CanMove(module);
                Color border;
                float width;
                if (selected == cell) (border, width) = (SelectedRing, 7f);
                else if (target) (border, width) = (TargetRing, 6f);
                else if (!onSite) (border, width) = (RoadBorder, 2f);
                else if (module == null) (border, width) = (LockedBorder, 2f);
                else if (module.Kind == SiteModule.ModuleKind.Empty) (border, width) = (EmptyBorder, 4f);
                else if (module.Kind == SiteModule.ModuleKind.Fixed && !IsInbound(module)) (border, width) = (FixedBorder, 2f);
                else (border, width) = (ModuleBorder, 4f);
                pair.Value.SetMarks(border, width, target, onSite && IsChanged(cell));
            }
        }

        /// <summary>칸에 보일 이름 · 칸 색 · 글자 색.</summary>
        (string name, Color color, Color text) Look(SiteModule module, SiteGrid grid, Data.SiteLayout layout)
        {
            var id = grid != null ? grid.IdOf(module) : null;
            var name = id != null ? layout.GetDisplayName(id) : module.name;
            if (IsInbound(module)) return (name, inboundColor, darkText);
            switch (module.Kind)
            {
                case SiteModule.ModuleKind.Empty: return (name, emptyColor, EmptyText);
                case SiteModule.ModuleKind.Fixed: return (name, fixedColor, Color.white);
                default:
                    var color = StoreColor(id);
                    return (name, color, color.grayscale < 0.5f ? Color.white : darkText); // 어두운 회사 색 위에는 흰 글자
            }
        }

        string LockedName(Vector2Int cell, Data.SiteLayout layout) => cellPlacement.TryGetValue(cell, out var placement) ? layout.GetDisplayName(placement.id) : "잠긴 칸";

        /// <summary>잠긴 칸의 해금 조건 (묶여 해금되는 배치는 대표 배치 조건).</summary>
        string UnlockText(Vector2Int cell, Data.SiteLayout layout)
        {
            if (!cellPlacement.TryGetValue(cell, out var placement)) return "잠김";
            if (layout.TryGetPlacement(layout.GetUnlockRoot(placement.id), out var root)) placement = root;
            return $"Lv.{placement.unlockLevel} / ₩{placement.cost:N0} 해금";
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

        // ── 확인창 ───────────────────────────────────────────

        /// <summary>화면 전체를 어둡게 덮는 확인창 (해고 확인창과 같은 모양). 루트 캔버스 맨 위.</summary>
        void BuildConfirm()
        {
            var canvas = GetComponentInParent<Canvas>(true).rootCanvas;
            var dim = NewImage("ModuleDiscardConfirm", (RectTransform)canvas.transform, new Color(0f, 0f, 0f, 0.45f), true);
            dim.sprite = null;
            Stretch(dim.rectTransform, 0f, 0f);
            confirm = dim.gameObject;

            var box = NewImage("Box", dim.rectTransform, PaperColor, true).rectTransform;
            box.sizeDelta = new Vector2(780f, 380f);
            var label = NewText("Label", box, reasonLabel.font, 40f, TitleColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 30f, 30f, 30f, 150f);
            label.text = "배치가 반영되지 않습니다\n<size=70%><color=#7a5b3e>[적용]하지 않은 배치를 버리고 닫을까요?</color></size>";
            var cancel = CloneButton(resetButton, box, "Cancel", "취소", null);
            var ok = CloneButton(resetButton, box, "Confirm", "확인", null);
            Style(cancel, ResetFill, ResetText, ResetLine);
            Style(ok, new Color(0.8f, 0.33f, 0.25f), Color.white, new Color(0.6f, 0.2f, 0.15f));
            PlaceButton(cancel, new Vector2(0.5f, 0f), new Vector2(-150f, 80f), new Vector2(260f, 96f));
            PlaceButton(ok, new Vector2(0.5f, 0f), new Vector2(150f, 80f), new Vector2(260f, 96f));
            cancel.onClick.AddListener(() => confirm.SetActive(false));
            ok.onClick.AddListener(() =>
            {
                confirm.SetActive(false);
                ResetArrangement(); // 버림 (다음에 열면 Begin이 부지 배치로 다시 채움)
                var then = onDiscard;
                onDiscard = null;
                then?.Invoke();
            });
            confirm.SetActive(false);
        }
    }
}
