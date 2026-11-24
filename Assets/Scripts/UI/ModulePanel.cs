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
    /// 계약 PC 팝업의 `모듈` 탭 (GDD 4.4): 6 × 4 부지 미니맵에서 보관 · 반품 칸을 끌어다 놓아 자리를 바꾼다.
    /// 빈 공간에 놓으면 이동, 보관 · 반품에 놓으면 서로 교체, 그 밖(고정 모듈 · 잠긴 칸 · 바깥)은 실패(흔들림 + 사유).
    /// 바꾼 내용은 미니맵에만 쌓아 두고 **팝업을 닫을 때 한꺼번에** SiteGrid에 적용한다 (ContractPopup이 Begin · Apply를 부름).
    /// [되돌리기] = 팝업을 열었을 때 배치로.
    /// 옮기는 모듈(맞바꾸는 상대 포함)에 걸친 컨베이어 벨트는 옮길 때 안내하고, 적용할 때 철거 · 전액 환불 (GDD 7.10, M18-4).
    /// </summary>
    public sealed class ModulePanel : MonoBehaviour
    {
        const int Columns = 6;
        const int Rows = 4;

        [SerializeField, Tooltip("칸 템플릿 (꺼진 채). 위치 = (1,1) 칸")] ModuleCell cellTemplate;
        [SerializeField, Tooltip("칸 간격 (px)")] Vector2 cellStep = new(210f, 160f);
        [SerializeField] Button resetButton;
        [SerializeField] TMP_Text reasonLabel;

        [Header("색")]
        [SerializeField] Color emptyColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color fixedColor = new(0.38f, 0.35f, 0.33f);
        [SerializeField] Color returnsColor = new(0.55f, 0.62f, 0.8f);
        [SerializeField] Color lockedColor = new(0.55f, 0.55f, 0.55f, 0.45f);
        [SerializeField] Color darkText = new(0.25f, 0.22f, 0.2f);

        readonly Dictionary<Vector2Int, ModuleCell> cells = new();
        readonly Dictionary<Vector2Int, SiteModule> staged = new();
        readonly Dictionary<Vector2Int, SiteModule> original = new();
        readonly HashSet<Vector2Int> siteCells = new();
        Tween reasonTween;

        void Awake()
        {
            EnsureCells();
            resetButton.onClick.AddListener(ResetArrangement);
        }

        /// <summary>팝업을 열 때: 지금 부지 배치를 미니맵에 옮겨 둔다.</summary>
        public void Begin()
        {
            var grid = SiteGrid.Instance;
            if (grid == null) return;
            EnsureCells();
            original.Clear();
            foreach (var cell in cells.Keys) original[cell] = grid.ModuleAt(cell);
            ResetArrangement();
            reasonLabel.gameObject.SetActive(false);
        }

        /// <summary>팝업을 닫을 때: 미니맵 배치를 실제 부지에 적용한다 (바뀐 모듈만 옮겨짐).</summary>
        public void Apply()
        {
            var grid = SiteGrid.Instance;
            if (grid == null || staged.Count == 0) return;
            var arrangement = new Dictionary<SiteModule, Vector2Int>();
            foreach (var pair in staged)
            {
                if (pair.Value != null) arrangement[pair.Value] = pair.Key;
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
                Render();
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
            else if (other.Kind == SiteModule.ModuleKind.Fixed) Fail(source, "옮길 수 없는 칸이에요");
            else
            {
                staged[target.Cell] = moving; // 빈 공간이면 이동, 보관 · 반품이면 교체 (둘 다 자리 맞바꿈)
                staged[source.Cell] = other;
                Render();
                var belts = BeltsOnMoved().Count;
                if (belts > 0) ShowReason($"연결된 벨트 {belts}개가 철거됩니다 (전액 환불)", 2.5f);
                cells[target.Cell].Rect.DOKill(true);
                cells[target.Cell].Rect.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
            }
        }

        void ResetArrangement()
        {
            staged.Clear();
            foreach (var pair in original) staged[pair.Key] = pair.Value;
            Render();
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

        /// <summary>지금 미니맵 배치대로 옮기면 자리가 바뀌는 모듈에 걸친 벨트 (Start · End가 있거나 경로가 지나감).</summary>
        List<ConveyorBelt> BeltsOnMoved()
        {
            var result = new List<ConveyorBelt>();
            var grid = SiteGrid.Instance;
            if (grid == null || ConveyorBelt.All.Count == 0) return result;
            var moved = new List<SiteModule>();
            foreach (var pair in staged)
            {
                if (pair.Value != null && grid.WorldToCell(pair.Value.transform.position) != pair.Key) moved.Add(pair.Value);
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
                staged.TryGetValue(cell, out var module);
                view.Draggable = module != null && module.Kind == SiteModule.ModuleKind.Movable;
                if (module == null)
                {
                    view.Show("잠김", lockedColor, Color.white);
                    continue;
                }

                var id = grid != null ? grid.IdOf(module) : null;
                var name = id != null ? layout.GetDisplayName(id) : module.name;
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
        }

        /// <summary>보관 = 그 회사 품목 띠 색 (id `Store` + 회사 id), 반품 = 반품 색.</summary>
        Color StoreColor(string id)
        {
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
