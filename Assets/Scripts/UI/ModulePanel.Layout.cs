using System.Collections.Generic;
using APC.Site;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 모듈 탭 화면 구성 (UI 시안 module_placement_ui_mobile.html, 2026-11-26). 가로 · 세로 같은 부품, 배치만 다르다 —
    /// 판정은 책상 팝업의 화면 방향 플래그(ContractPopup.portrait)를 받아 쓴다 (<see cref="Layout"/>).
    /// 부품: 범례 줄(색 = 칸 종류 | 보이는 열) · 도면 패널(6 × 4 좌우 스크롤, 열 번호 · 격자 · 가장자리 그늘 · 화살표) · 위치 바 ·
    /// 벨트 철거 경고 배너(있을 때만) · 설명 카드(고른 칸 배지 · 이름 · 태그 · 안내 | [회전] [취소]) · [되돌리기] [배치 적용] (2 : 3).
    /// 세로 = 위에서부터 한 줄로 쌓음 (시안 그대로). 가로 = 왼쪽 범례 · 도면 · 위치 바 | 오른쪽 설명 카드(세로로 긴) · 경고 · 버튼.
    /// [회전]은 돌릴 수 있는 칸을 골랐을 때만 [취소] 옆에 뜬다. UI는 Build에서 코드로 만든다. 크기 = 시안 px × 약 2.4, 값은 모두 `임시`.
    /// </summary>
    public sealed partial class ModulePanel
    {
        [Header("배치 (임시, 가로 · 세로 공통)")]
        [SerializeField, Min(0f), Tooltip("페이지 내용 바깥 여백에 더하는 안여백 (좌우 · 위 · 아래)")] float contentInset = 28f;
        [SerializeField, Min(0f), Tooltip("도면 패널 안쪽 여백 (붉은 바가 잘리지 않게 바 두께보다 크게)")] float boardPadding = 40f;
        [SerializeField, Min(0f), Tooltip("칸 사이")] float cellGap = 19f;
        [SerializeField, Min(0.5f), Tooltip("칸 가로 ÷ 세로 (시안 1.14)")] float cellAspect = 1.14f;
        [SerializeField, Tooltip("칸 높이 최소 · 최대")] Vector2 cellHeightRange = new(90f, 220f);
        [SerializeField, Range(0.2f, 0.45f), Tooltip("가로 배치: 오른쪽 칸(설명 카드 · 버튼) 너비 비율")] float landscapeSideRatio = 0.32f;
        [SerializeField, Min(0f), Tooltip("끄는 동안 이 거리 안이 도면 가장자리면 자동 스크롤")] float autoScrollEdge = 110f;
        [SerializeField, Min(0f), Tooltip("가장자리 자동 스크롤 속도 (px/초)")] float autoScrollSpeed = 1100f;

        // 시안 색
        static readonly Color PaperColor = new(0.984f, 0.965f, 0.922f);   // #fbf6eb
        static readonly Color TitleColor = new(0.23f, 0.15f, 0.07f);      // #3b2513
        static readonly Color SoftText = new(0.48f, 0.36f, 0.24f);        // #7a5b3e
        static readonly Color LegendText = new(0.43f, 0.31f, 0.2f);       // #6e4e32
        static readonly Color HeaderText = new(0.54f, 0.43f, 0.30f);      // #8a6d4d
        static readonly Color BoardColor = new(0.937f, 0.898f, 0.82f);    // #efe5d1
        static readonly Color BoardLine = new(0.796f, 0.714f, 0.58f);     // #cbb694
        static readonly Color GridLine = new(0.545f, 0.396f, 0.27f, 0.10f);
        static readonly Color Shade = new(0.243f, 0.153f, 0.082f, 0.24f); // 가장자리 그늘
        static readonly Color ArrowFill = new(0.165f, 0.106f, 0.07f, 0.75f);
        static readonly Color Amber100 = new(0.996f, 0.953f, 0.78f);      // #fef3c7
        static readonly Color Amber400 = new(0.984f, 0.749f, 0.141f);     // #fbbf24
        static readonly Color Amber800 = new(0.573f, 0.251f, 0.055f);     // #92400e
        static readonly Color Amber900 = new(0.47f, 0.208f, 0.059f);      // #78350f
        static readonly Color Amber950 = new(0.271f, 0.102f, 0.012f);     // #451a03
        static readonly Color TrackFill = new(0.894f, 0.835f, 0.725f);    // #e4d5b9
        static readonly Color TrackLine = new(0.8f, 0.718f, 0.573f);      // #ccb792
        static readonly Color ThumbFill = new(0.62f, 0.447f, 0.278f);     // #9e7247
        static readonly Color CardFill = new(1f, 0.992f, 0.98f);          // #fffdfa
        static readonly Color CardLine = new(0.839f, 0.769f, 0.647f);     // #d6c4a5
        static readonly Color BadgeIdle = new(0.937f, 0.886f, 0.784f);    // #efe2c8
        static readonly Color ResetFill = new(0.929f, 0.882f, 0.796f);    // #ede1cb
        static readonly Color ResetText = new(0.33f, 0.216f, 0.114f);     // #54371d
        static readonly Color ResetLine = new(0.78f, 0.69f, 0.537f);      // #c7b089
        static readonly Color ApplyFill = new(0.31f, 0.616f, 0.451f);     // #4f9d73
        static readonly Color ApplyLine = new(0.157f, 0.38f, 0.259f);     // #286142
        static readonly Color CancelFill = new(0.961f, 0.961f, 0.957f);   // stone-100
        static readonly Color CancelText = new(0.341f, 0.325f, 0.306f);   // stone-600
        static readonly Color ToastFill = new(0.165f, 0.102f, 0.063f, 0.95f); // #2a1a10

        const float LegendHeight = 34f, SectionGap = 20f, MapGap = 10f, IndicatorHeight = 44f; // 위치 바 = 끄는 터치 높이
        const float WarnHeight = 72f, CardHeight = 184f, BarHeight = 104f, HeaderHeight = 26f, HeaderGap = 14f;
        const float PortraitBarDrop = 24f; // 세로: 버튼 줄을 아래 여백 쪽으로 이만큼 내려 카드 자리를 늘림 (탭 피하는 선은 그대로 넘지 않음)
        const float HeaderClearance = 30f; // 종이 머리 구분선 ↔ 범례
        const float CardPad = 24f, BadgeSize = 86f, CardButton = 110f;

        GameObject sheetUi;
        RectTransform legend, board, content, columnHeaders, indicator, thumb, warning, card, tagPill, toastBack;
        ScrollRect scroll;
        Image fadeLeft, fadeRight, cardBadge;
        Button scrollLeft, scrollRight, cancelButton, cardRotate;
        TMP_Text visibleColumns, warningLabel, refundLabel, badgeLabel, cardTitle, cardTag, cardDesc;
        readonly List<(TMP_Text text, float size)> designFonts = new(); // 세로 팝업이 작은 글씨를 키워도(ContractPopup) 시안 크기로
        RectTransform layoutHeader;
        float layoutBottom, layoutSide;
        bool warningShown;
        float toastMaxWidth = 600f;
        Vector2? dragPoint;
        Tween toastTween;

        /// <summary>끄는 칸의 화면 좌표 (ModuleCell). null = 끌기 끝. 도면 가장자리면 Update가 자동 스크롤.</summary>
        public void SetDragPoint(Vector2? point) => dragPoint = point;

        void Update()
        {
            if (dragPoint is not { } point || board == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, point, null, out var local)) return; // Screen Space Overlay
            var area = board.rect;
            var dir = local.x < area.xMin + autoScrollEdge ? -1f : local.x > area.xMax - autoScrollEdge ? 1f : 0f;
            var range = content.rect.width - area.width;
            if (dir == 0f || range <= 0f) return;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition + dir * autoScrollSpeed * Time.unscaledDeltaTime / range); // 팝업 중 게임 멈춤
        }

        void ScrollToStart()
        {
            if (scroll != null) scroll.horizontalNormalizedPosition = 0f; // 왼쪽(사무실 · 하차)부터
        }

        /// <summary>
        /// 화면 방향에 맞춰 배치한다 (ContractPopup이 열 때 · 방향이 바뀔 때). portrait = 팝업의 방향 플래그.
        /// headerLine = 종이 머리 구분선 (그 아래부터 씀), bottom · side = 페이지 아래 · 좌우 여백 (세로 아래는 종이 밑으로 들어간 탭을 피한 값). 여기에 contentInset을 더한다.
        /// </summary>
        public void Layout(bool portrait, RectTransform headerLine, float bottom, float side)
        {
            Build();
            this.portrait = portrait;
            (layoutHeader, layoutBottom, layoutSide) = (headerLine, bottom, side);
            sheetUi.SetActive(true);
            if (hint != null) hint.gameObject.SetActive(false);                       // 설명 카드가 대신
            if (rotateButton != null) rotateButton.gameObject.SetActive(false);       // [회전] = 카드 안 (돌릴 수 있는 칸을 골랐을 때만)
            reasonLabel.color = new Color(Amber100.r, Amber100.g, Amber100.b, reasonLabel.alpha); // 지금 보이는 정도(사라지는 중)는 유지
            reasonLabel.alignment = TextAlignmentOptions.Center;
            foreach (var (text, size) in designFonts) text.fontSize = size;
            // 자동 크기 범위도 시안 값으로 (세로 팝업이 작은 글씨 최소 · 최대도 1.2배 하므로)
            (cardDesc.fontSizeMin, cardDesc.fontSizeMax) = (18f, 24f);
            (warningLabel.fontSizeMin, warningLabel.fontSizeMax) = (18f, 26f);
            Style(resetButton, ResetFill, ResetText, ResetLine);
            Style(applyButton, ApplyFill, Color.white, ApplyLine);

            var page = ((RectTransform)transform).rect.size;
            side += contentInset;
            bottom += contentInset;
            var top = HeaderBottom(headerLine, page) + HeaderClearance + contentInset;
            var width = page.x - side * 2f;
            var legendTop = page.y - top;
            warning.gameObject.SetActive(warningShown);

            float boardLeft, boardWidth, boardBottom;
            if (portrait)
            {
                // 위에서부터 한 줄: 범례 · 도면 · 위치 바 · (경고) · 카드 · 버튼
                var y = bottom - Mathf.Min(PortraitBarDrop, contentInset); // 안여백 안에서만 내림
                PlaceBar(side, y, width);
                y += BarHeight + SectionGap;
                Put(card, new Vector2(page.x * 0.5f, y + CardHeight * 0.5f), new Vector2(width, CardHeight));
                y += CardHeight + SectionGap;
                if (warningShown)
                {
                    Put(warning, new Vector2(page.x * 0.5f, y + WarnHeight * 0.5f), new Vector2(width, WarnHeight));
                    y += WarnHeight + SectionGap;
                }
                (boardLeft, boardWidth, boardBottom) = (side, width, y);
            }
            else
            {
                // 왼쪽: 범례 · 도면 · 위치 바 | 오른쪽: 설명 카드(남는 높이) · (경고) · 버튼
                var sideWidth = Mathf.Round(width * landscapeSideRatio);
                var gap = SectionGap * 1.5f;
                var right = side + width - sideWidth;
                var y = bottom;
                PlaceBar(right, y, sideWidth);
                y += BarHeight + SectionGap;
                if (warningShown)
                {
                    Put(warning, new Vector2(right + sideWidth * 0.5f, y + WarnHeight * 0.5f), new Vector2(sideWidth, WarnHeight));
                    y += WarnHeight + SectionGap;
                }
                var cardHeight = legendTop - y;
                Put(card, new Vector2(right + sideWidth * 0.5f, y + cardHeight * 0.5f), new Vector2(sideWidth, cardHeight));
                (boardLeft, boardWidth, boardBottom) = (side, width - sideWidth - gap, bottom);
            }
            LayoutCardParts();

            // 범례 · 위치 바 · 도면 (도면 = 범례 아래 ↔ 위치 바 위 전부)
            Put(legend, new Vector2(boardLeft + boardWidth * 0.5f, legendTop - LegendHeight * 0.5f), new Vector2(boardWidth, LegendHeight));
            Put(indicator, new Vector2(boardLeft + boardWidth * 0.5f, boardBottom + IndicatorHeight * 0.5f), new Vector2(boardWidth, IndicatorHeight));
            var y0 = boardBottom + IndicatorHeight + MapGap;
            var boardHeight = legendTop - LegendHeight - SectionGap - y0;
            var center = new Vector2(boardLeft + boardWidth * 0.5f, y0 + boardHeight * 0.5f);
            Put(board, center, new Vector2(boardWidth, boardHeight));

            // 토스트 = 도면 위쪽 가운데 (시안: 화면 위 알약). 길이는 띄울 때 글자에 맞춤 (ShowToastBack)
            var toast = new Vector2(center.x, y0 + boardHeight - 56f);
            toastMaxWidth = boardWidth - 24f;
            Put(reasonLabel.rectTransform, toast, new Vector2(toastMaxWidth - 40f, 64f));
            Put(toastBack, toast, new Vector2(toastMaxWidth, 72f));

            LayoutGrid(boardWidth, boardHeight);
            if (cells.Count > 0) Render();
            UpdateScrollMarks();
        }

        /// <summary>종이 머리 구분선 아랫변이 페이지 위에서 얼마나 내려왔나 (페이지 좌표). 없으면 150.</summary>
        float HeaderBottom(RectTransform headerLine, Vector2 page)
        {
            if (headerLine == null) return 150f;
            var corners = new Vector3[4];
            headerLine.GetWorldCorners(corners); // 0 = 왼쪽 아래
            var self = (RectTransform)transform;
            var local = self.InverseTransformPoint(corners[0]);
            return Mathf.Clamp(self.rect.yMax - local.y, 0f, page.y * 0.5f);
        }

        /// <summary>[되돌리기] 2 : [배치 적용] 3 (x = 왼쪽, y = 아래).</summary>
        void PlaceBar(float x, float y, float width)
        {
            var resetWidth = (width - SectionGap) * 0.4f;
            var applyWidth = width - SectionGap - resetWidth;
            PlaceButton(resetButton, new Vector2(x + resetWidth * 0.5f, y + BarHeight * 0.5f), new Vector2(resetWidth, BarHeight));
            PlaceButton(applyButton, new Vector2(x + width - applyWidth * 0.5f, y + BarHeight * 0.5f), new Vector2(applyWidth, BarHeight));
        }

        /// <summary>카드 배지 · 버튼 자리: 세로(가로로 긴 카드) = 왼쪽 가운데 배지, 오른쪽 가운데 버튼. 가로(세로로 긴 카드) = 왼쪽 위 배지, 오른쪽 아래 버튼.</summary>
        void LayoutCardParts()
        {
            var badge = cardBadge.rectTransform;
            if (portrait) SetLeftMiddle(badge, CardPad, BadgeSize, BadgeSize);
            else SetLeftTop(badge, CardPad, CardPad, BadgeSize, BadgeSize);
            var anchor = portrait ? new Vector2(1f, 0.5f) : new Vector2(1f, 0f);
            var y = portrait ? 0f : CardPad + 36f;
            PlaceButton(cancelButton, anchor, new Vector2(-CardPad - CardButton * 0.5f, y), new Vector2(CardButton, 72f));
            PlaceButton(cardRotate, anchor, new Vector2(-CardPad - CardButton * 1.5f - 12f, y), new Vector2(CardButton, 72f));
        }

        /// <summary>칸: 4행 + 열 번호가 도면 높이에 맞게, 가로가 남으면 칸을 넓혀 채움 (좁으면 넘쳐서 좌우 스크롤).</summary>
        void LayoutGrid(float boardWidth, float boardHeight)
        {
            var gridRoom = boardHeight - boardPadding * 2f - HeaderHeight - HeaderGap;
            var cellHeight = Mathf.Clamp((gridRoom - cellGap * (Rows - 1)) / Rows, cellHeightRange.x, cellHeightRange.y);
            var cellWidth = Mathf.Max(cellHeight * cellAspect, (boardWidth - boardPadding * 2f - cellGap * (Columns - 1)) / Columns);
            var gridHeight = cellHeight * Rows + cellGap * (Rows - 1);
            var contentWidth = cellWidth * Columns + cellGap * (Columns - 1) + boardPadding * 2f;
            var gridTop = (boardHeight - gridHeight - HeaderHeight - HeaderGap) * 0.5f + HeaderHeight + HeaderGap; // 열 번호 + 칸 묶음을 위아래 가운데
            content.sizeDelta = new Vector2(contentWidth, boardHeight);
            gridParent = content;
            gridSize = new Vector2(cellWidth, cellHeight);
            gridStep = gridSize + Vector2.one * cellGap;
            gridOrigin = new Vector2(boardPadding + cellWidth * 0.5f, -gridTop - cellHeight * 0.5f);
            for (var i = 0; i < columnHeaders.childCount; i++)
            {
                var header = (RectTransform)columnHeaders.GetChild(i);
                header.anchorMin = header.anchorMax = new Vector2(0f, 1f);
                header.anchoredPosition = new Vector2(gridOrigin.x + i * gridStep.x, -gridTop + HeaderGap + HeaderHeight * 0.5f);
                header.sizeDelta = new Vector2(cellWidth, HeaderHeight);
            }
        }

        /// <summary>Render 끝: 경고 배너 · 설명 카드 · [배치 적용] 글자.</summary>
        void RenderPanels(SiteGrid grid, Data.SiteLayout layout)
        {
            if (sheetUi == null || !sheetUi.activeSelf) return; // 아직 Layout 전
            var changes = CountChanges();
            // 가로는 오른쪽 칸 버튼이 좁아 건수를 다음 줄에 작게
            if (applyLabel != null)
                applyLabel.text = changes == 0 ? "배치 적용" : portrait ? $"배치 적용 ({changes}건 변경)" : $"배치 적용\n<size=75%>({changes}건 변경)</size>";

            var belts = BeltsOnMoved();
            if (belts.Count > 0)
            {
                var refund = 0;
                foreach (var belt in belts) refund += belt.Cost;
                warningLabel.text = $"연결된 벨트 {belts.Count}개 철거 예정";
                refundLabel.text = $"+₩{refund:N0} 전액 환불";
                FitBadge(refundLabel, 14f);
            }
            if (belts.Count > 0 != warningShown)
            {
                warningShown = belts.Count > 0;
                Layout(portrait, layoutHeader, layoutBottom, layoutSide); // 배너가 생기거나 없어진 만큼 다시 배치 (다시 Render)
                return;
            }
            RenderCard(grid, layout);
        }

        /// <summary>설명 카드 (시안 인스펙터): 아무것도 안 고름 = 대기 안내, 옮길 수 있는 모듈 = 이동 안내 + [회전] [취소], 그 밖 = 칸 설명 + [취소].</summary>
        void RenderCard(SiteGrid grid, Data.SiteLayout layout)
        {
            var movable = SelectedMovable;
            cancelButton.gameObject.SetActive(selected != null);
            cardRotate.gameObject.SetActive(movable);

            string title, tag, desc, letter;
            Color badge, badgeText, tagFill, tagText;
            if (selected is not { } cell)
            {
                (title, tag, desc, letter) = ("도면을 좌우로 밀어 탐색하세요", "대기", "칸을 탭해 고르고, 다른 칸을 탭하거나 끌어 놓아 옮겨요.", "< >");
                (badge, badgeText, tagFill, tagText) = (BadgeIdle, ResetText, new Color(0.906f, 0.898f, 0.894f), CancelText);
            }
            else if (!staged.TryGetValue(cell, out var module) || module == null)
            {
                (title, tag, desc, letter) = (LockedName(cell, layout), UnlockText(cell, layout), "해금 전인 부지예요. 해금하면 모듈을 놓을 수 있어요.", "잠");
                (badge, badgeText, tagFill, tagText) = (new Color(0.839f, 0.827f, 0.82f), new Color(0.267f, 0.251f, 0.235f), new Color(0.839f, 0.827f, 0.82f), new Color(0.267f, 0.251f, 0.235f));
            }
            else
            {
                var (name, color, textColor) = Look(module, grid, layout);
                (title, letter, badge, badgeText) = (name, name.Length > 0 ? name.Substring(0, 1) : "?", color, textColor);
                if (movable)
                {
                    tag = "이동 선택됨";
                    desc = IsInbound(module)
                        ? "노란 테두리 칸을 탭해 옮기세요. 바깥 테두리에만 놓여요. [회전] = 트럭 쪽(붉은 바)."
                        : "노란 테두리 칸을 탭해 옮기세요. [회전]으로 90°씩 돌려요.";
                    (tagFill, tagText) = (new Color(0.992f, 0.902f, 0.541f), Amber900);
                }
                else if (module.Kind == SiteModule.ModuleKind.Empty)
                {
                    (tag, desc) = ("빈 부지", "보관 · 반품 · 스캔 · 하차 모듈을 옮겨 놓을 수 있어요.");
                    (tagFill, tagText) = (new Color(0.922f, 0.89f, 0.827f), new Color(0.431f, 0.341f, 0.243f));
                }
                else
                {
                    (tag, desc) = ("고정 시설", "고정 시설이라 옮길 수 없어요.");
                    (tagFill, tagText) = (new Color(0.341f, 0.325f, 0.306f), Color.white);
                }
            }

            cardBadge.color = badge;
            badgeLabel.text = letter;
            badgeLabel.color = badgeText;
            cardTag.text = tag;
            cardTag.color = tagText;
            tagPill.GetComponent<Image>().color = tagFill;
            cardDesc.text = desc;
            cardTitle.text = title;

            var left = CardPad + BadgeSize + 20f;
            var tagWidth = cardTag.GetPreferredValues(tag).x + 24f;
            var size = card.rect.size;
            if (portrait)
            {
                // 가로로 긴 카드: 제목 줄 = 이름(길면 줄임) + 태그 알약, 아래 안내 — 오른쪽 버튼 앞까지
                var buttons = (selected != null ? CardButton + CardPad : 0f) + (movable ? CardButton + 12f : 0f);
                var room = size.x - left - CardPad - buttons;
                var titleWidth = Mathf.Min(cardTitle.GetPreferredValues(title).x + 4f, room - tagWidth - 12f);
                SetLeftTop(cardTitle.rectTransform, left, 26f, titleWidth, 44f);
                SetLeftTop(tagPill, left + titleWidth + 12f, 31f, tagWidth, 34f);
                SetLeftTop(cardDesc.rectTransform, left, 76f, room, size.y - 76f - 18f);
            }
            else
            {
                // 세로로 긴 카드: 배지 오른쪽에 이름 / 태그, 아래 안내(여러 줄), 맨 아래 버튼
                var room = size.x - left - CardPad;
                SetLeftTop(cardTitle.rectTransform, left, CardPad + 4f, room, 44f);
                SetLeftTop(tagPill, left, CardPad + 52f, Mathf.Min(tagWidth, room), 34f);
                var descTop = CardPad + BadgeSize + 22f;
                SetLeftTop(cardDesc.rectTransform, CardPad, descTop, size.x - CardPad * 2f, size.y - descTop - CardPad - 72f - 16f);
            }
        }

        /// <summary>스크롤이 움직일 때: 가장자리 그늘 · 화살표 · 위치 바 · `n~m열 보기`.</summary>
        void UpdateScrollMarks()
        {
            if (scroll == null) return;
            var view = board.rect.width;
            var range = content.rect.width - view;
            var position = range > 0f ? Mathf.Clamp01(scroll.horizontalNormalizedPosition) : 0f;
            var canLeft = range > 1f && position > 0.01f;
            var canRight = range > 1f && position < 0.99f;
            fadeLeft.gameObject.SetActive(canLeft);
            scrollLeft.gameObject.SetActive(canLeft);
            fadeRight.gameObject.SetActive(canRight);
            scrollRight.gameObject.SetActive(canRight);

            if (gridStep.x <= 0f) return; // 손잡이는 Scrollbar가 움직인다
            var offset = position * Mathf.Max(0f, range) - boardPadding;
            var first = Mathf.Clamp(Mathf.FloorToInt((offset + cellGap) / gridStep.x) + 1, 1, Columns);
            var last = Mathf.Clamp(Mathf.CeilToInt((offset + view - boardPadding) / gridStep.x), first, Columns);
            visibleColumns.text = $"{first}~{last}열 보기";
        }

        /// <summary>화살표: 한 열만큼 부드럽게 밀기.</summary>
        void ScrollByColumn(int dir)
        {
            var range = content.rect.width - board.rect.width;
            if (range <= 0f) return;
            var target = Mathf.Clamp01(scroll.horizontalNormalizedPosition + dir * gridStep.x / range);
            scroll.StopMovement();
            DOTween.Kill(scroll);
            DOTween.To(() => scroll.horizontalNormalizedPosition, v => scroll.horizontalNormalizedPosition = v, target, 0.25f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(scroll).SetLink(gameObject);
        }

        /// <summary>사유 글자 뒤 어두운 알약 — 길이는 글자에 맞춤(도면 폭까지, 넘으면 글자를 줄임), 글자와 같이 사라진다.</summary>
        void ShowToastBack(string text, float hold)
        {
            if (toastBack == null) return;
            reasonLabel.enableAutoSizing = false;
            reasonLabel.fontSize = 30f;
            var width = Mathf.Min(toastMaxWidth, reasonLabel.GetPreferredValues(text).x + 70f);
            toastBack.sizeDelta = new Vector2(width, 72f);
            reasonLabel.rectTransform.sizeDelta = new Vector2(width - 40f, 64f);
            reasonLabel.enableAutoSizing = true; // 도면 폭보다 길면 줄여서 한 줄
            reasonLabel.fontSizeMin = 18f;
            reasonLabel.fontSizeMax = 30f;
            var image = toastBack.GetComponent<Image>();
            toastTween?.Kill();
            toastBack.gameObject.SetActive(true);
            image.color = ToastFill;
            toastTween = image.DOFade(0f, 0.4f).SetDelay(hold).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => toastBack.gameObject.SetActive(false));
        }

        // ── 만들기 ───────────────────────────────────────────

        void BuildSheetUi()
        {
            var title = reasonLabel.font;
            var body = hint != null && hint.GetComponent<TMP_Text>() is { } h ? h.font : title;

            var root = new GameObject("SheetUi", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            root.transform.SetSiblingIndex(cellTemplate.transform.GetSiblingIndex()); // 버튼 · 사유 글자보다 뒤
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect, 0f, 0f);
            sheetUi = root;

            BuildLegend(rootRect, title);
            BuildBoard(rootRect, title);
            BuildIndicator(rootRect, title);
            BuildWarning(rootRect, title);
            BuildCard(rootRect, title, body);

            toastBack = NewImage("ToastBack", rootRect, ToastFill, false).rectTransform;
            toastBack.GetComponent<Image>().pixelsPerUnitMultiplier = 0.25f; // 알약
            AddLine(toastBack.GetComponent<Image>(), new Color(0.984f, 0.749f, 0.141f, 0.7f), 2f);
            toastBack.gameObject.SetActive(false);

            foreach (var text in rootRect.GetComponentsInChildren<TMP_Text>(true))
            {
                if (designFonts.Exists(f => f.text == text)) continue;
                designFonts.Add((text, text.fontSize));
            }
            designFonts.Add((resetButton.GetComponentInChildren<TMP_Text>(true), 30f));
            designFonts.Add((applyButton.GetComponentInChildren<TMP_Text>(true), 30f));
            root.SetActive(false);
        }

        /// <summary>범례: 이동 가능 · 하차장 · 빈칸 · 고정 · 잠김 색 견본 | 오른쪽 `n~m열 보기`.</summary>
        void BuildLegend(RectTransform parent, TMP_FontAsset font)
        {
            legend = new GameObject("Legend", typeof(RectTransform)).GetComponent<RectTransform>();
            legend.SetParent(parent, false);
            var items = new (string text, Color fill, Color line)[]
            {
                ("이동가능", SelectedRing, ModuleBorder),
                ("하차장", inboundColor, ModuleBorder),
                ("빈칸", emptyColor, new Color(0.71f, 0.62f, 0.49f)),
                ("고정", fixedColor, FixedBorder),
                ("잠김", lockedColor, LockedBorder),
            };
            var x = 0f;
            foreach (var (text, fill, line) in items)
            {
                var swatch = NewImage("Swatch", legend, fill, false);
                swatch.pixelsPerUnitMultiplier = 2f;
                AddLine(swatch, line, 2f);
                SetLeftMiddle(swatch.rectTransform, x, 24f, 24f);
                var label = NewText(text, legend, font, 24f, LegendText, TextAlignmentOptions.MidlineLeft);
                label.text = text;
                var w = label.GetPreferredValues(text).x + 2f;
                SetLeftMiddle(label.rectTransform, x + 32f, w, LegendHeight);
                x += 32f + w + 22f;
            }
            visibleColumns = NewText("VisibleColumns", legend, font, 24f, new Color(0.47f, 0.21f, 0.06f, 0.8f), TextAlignmentOptions.MidlineRight);
            var columns = visibleColumns.rectTransform;
            columns.anchorMin = columns.anchorMax = columns.pivot = new Vector2(1f, 0.5f);
            columns.anchoredPosition = Vector2.zero;
            columns.sizeDelta = new Vector2(160f, LegendHeight);
        }

        /// <summary>도면 패널: 둥근 판(테두리) · 격자 무늬 · 스크롤 content(열 번호 + 칸) · 좌우 그늘 · 화살표.</summary>
        void BuildBoard(RectTransform parent, TMP_FontAsset font)
        {
            var image = NewImage("Board", parent, BoardColor, true);
            image.pixelsPerUnitMultiplier = 0.6f;
            AddLine(image, BoardLine, 5f);
            image.gameObject.AddComponent<RectMask2D>();
            board = image.rectTransform;

            var grid = new GameObject("GridPattern", typeof(RectTransform)).AddComponent<Image>();
            grid.transform.SetParent(board, false);
            grid.sprite = GridSprite();
            grid.type = Image.Type.Tiled;
            grid.raycastTarget = false;
            Stretch(grid.rectTransform, 0f, 0f);

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(board, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            columnHeaders = new GameObject("ColumnHeaders", typeof(RectTransform)).GetComponent<RectTransform>();
            columnHeaders.SetParent(content, false);
            Stretch(columnHeaders, 0f, 0f);
            for (var c = 1; c <= Columns; c++)
            {
                var header = NewText($"Col{c}", columnHeaders, font, 22f, HeaderText, TextAlignmentOptions.Center);
                header.text = $"{c}열";
            }

            scroll = image.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = board;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.1f;
            scroll.scrollSensitivity = 0f;
            scroll.onValueChanged.AddListener(_ => UpdateScrollMarks());

            var fade = FadeSprite();
            fadeLeft = NewImage("FadeLeft", board, Shade, false);
            fadeRight = NewImage("FadeRight", board, Shade, false);
            foreach (var (f, side) in new[] { (fadeLeft, 0f), (fadeRight, 1f) })
            {
                f.sprite = fade;
                f.type = Image.Type.Simple;
                var r = f.rectTransform;
                r.anchorMin = new Vector2(side, 0f);
                r.anchorMax = new Vector2(side, 1f);
                r.pivot = new Vector2(0f, 0.5f);
                r.sizeDelta = new Vector2(67f, 0f); // 시안 28px
                r.anchoredPosition = Vector2.zero;
                r.localScale = new Vector3(side == 0f ? 1f : -1f, 1f, 1f); // 오른쪽은 뒤집어 가장자리가 진하고 안쪽으로 옅어짐
            }

            scrollLeft = CloneButton(resetButton, board, "ScrollLeft", "<", ArrowFill);
            scrollRight = CloneButton(resetButton, board, "ScrollRight", ">", ArrowFill);
            foreach (var (button, side, dir) in new[] { (scrollLeft, 0f, -1), (scrollRight, 1f, 1) })
            {
                AddLine(button.image, new Color(0.984f, 0.749f, 0.141f, 0.4f), 2f);
                PlaceButton(button, new Vector2(side, 0.5f), new Vector2(side == 0f ? 38f : -38f, 0f), new Vector2(68f, 96f));
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.color = Amber100;
                label.fontSize = 40f;
                button.onClick.AddListener(() => ScrollByColumn(dir));
            }
        }

        /// <summary>위치 바: `< 사무실·하차` [트랙 + 손잡이 — 끌거나 눌러 스크롤] `상차·확장 >`.</summary>
        void BuildIndicator(RectTransform parent, TMP_FontAsset font)
        {
            indicator = new GameObject("ScrollBar", typeof(RectTransform)).GetComponent<RectTransform>();
            indicator.SetParent(parent, false);
            var color = new Color(0.49f, 0.373f, 0.255f); // #7d5f41
            var left = NewText("Left", indicator, font, 22f, color, TextAlignmentOptions.MidlineLeft);
            left.text = "< 사무실·하차";
            var right = NewText("Right", indicator, font, 22f, color, TextAlignmentOptions.MidlineRight);
            right.text = "상차·확장 >";
            var leftWidth = left.GetPreferredValues(left.text).x + 4f;
            var rightWidth = right.GetPreferredValues(right.text).x + 4f;
            SetLeftMiddle(left.rectTransform, 0f, leftWidth, IndicatorHeight);
            var r = right.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(rightWidth, IndicatorHeight);

            // 끌 수 있는 스크롤바 (2026-11-26): 보이지 않는 터치 영역(줄 높이 전체) 안에 가는 트랙 · 손잡이. 값 · 손잡이 크기는 ScrollRect가 맞춘다
            var hit = NewImage("TrackHit", indicator, Color.clear, true);
            hit.sprite = null;
            var hitRect = hit.rectTransform;
            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.offsetMin = new Vector2(leftWidth + 14f, 0f);
            hitRect.offsetMax = new Vector2(-rightWidth - 14f, 0f);
            var trackImage = NewImage("Track", hitRect, TrackFill, false);
            trackImage.pixelsPerUnitMultiplier = 0.2f;
            AddLine(trackImage, TrackLine, 2f);
            var track = trackImage.rectTransform;
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.offsetMin = new Vector2(0f, -10f);
            track.offsetMax = new Vector2(0f, 10f);
            var thumbImage = NewImage("Thumb", track, ThumbFill, false);
            thumbImage.pixelsPerUnitMultiplier = 0.2f;
            thumb = thumbImage.rectTransform;
            thumb.offsetMin = thumb.offsetMax = Vector2.zero;

            var bar = hit.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = thumb; // 손잡이가 움직이는 범위 = 부모(트랙)
            bar.targetGraphic = thumbImage;
            bar.transition = Selectable.Transition.None;
            bar.direction = Scrollbar.Direction.LeftToRight;
            bar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None }; // APC.Navigation 네임스페이스와 이름이 겹침
            scroll.horizontalScrollbar = bar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        /// <summary>벨트 철거 경고 배너: 호박색 바탕 · 왼쪽 안내 | 오른쪽 환불 배지.</summary>
        void BuildWarning(RectTransform parent, TMP_FontAsset font)
        {
            var image = NewImage("BeltWarning", parent, Amber100, false);
            AddLine(image, Amber400, 2f);
            warning = image.rectTransform;
            warningLabel = NewText("Label", warning, font, 26f, Amber950, TextAlignmentOptions.MidlineLeft);
            warningLabel.enableAutoSizing = true; // 가로 배치의 좁은 오른쪽 칸에서도 한 줄
            warningLabel.fontSizeMin = 18f;
            warningLabel.fontSizeMax = 26f;
            Stretch(warningLabel.rectTransform, 26f, 250f);
            var badge = NewImage("Refund", warning, Amber800, false);
            badge.pixelsPerUnitMultiplier = 1.5f;
            var b = badge.rectTransform;
            b.anchorMin = b.anchorMax = b.pivot = new Vector2(1f, 0.5f);
            b.anchoredPosition = new Vector2(-18f, 0f);
            refundLabel = NewText("Label", b, font, 24f, Amber100, TextAlignmentOptions.Center);
            Stretch(refundLabel.rectTransform, 0f, 0f);
            warning.gameObject.SetActive(false);
        }

        /// <summary>설명 카드: 색 배지(글자) · 이름 · 태그 알약 · 안내 · [회전] [취소]. 자리는 LayoutCardParts · RenderCard.</summary>
        void BuildCard(RectTransform parent, TMP_FontAsset title, TMP_FontAsset body)
        {
            var image = NewImage("InfoCard", parent, CardFill, false);
            AddLine(image, CardLine, 2f);
            card = image.rectTransform;

            cardBadge = NewImage("Badge", card, BadgeIdle, false);
            AddLine(cardBadge, new Color(0f, 0f, 0f, 0.1f), 2f);
            badgeLabel = NewText("Letter", cardBadge.rectTransform, title, 38f, ResetText, TextAlignmentOptions.Center);
            Stretch(badgeLabel.rectTransform, 0f, 0f);

            cardTitle = NewText("Title", card, title, 30f, TitleColor, TextAlignmentOptions.MidlineLeft);
            cardTitle.textWrappingMode = TextWrappingModes.NoWrap;
            cardTitle.overflowMode = TextOverflowModes.Truncate; // 폰트에 말줄임표(…) 글자가 없음
            var pill = NewImage("Tag", card, Amber100, false);
            pill.pixelsPerUnitMultiplier = 1.5f;
            tagPill = pill.rectTransform;
            cardTag = NewText("Label", tagPill, title, 22f, Amber900, TextAlignmentOptions.Center);
            Stretch(cardTag.rectTransform, 0f, 0f);
            cardDesc = NewText("Desc", card, body, 24f, SoftText, TextAlignmentOptions.TopLeft);
            cardDesc.enableAutoSizing = true; // 칸보다 길면 줄여서 칸 안에 (최소 18)
            cardDesc.fontSizeMin = 18f;
            cardDesc.fontSizeMax = 24f;

            cancelButton = CloneButton(resetButton, card, "Cancel", "취소", null);
            Style(cancelButton, CancelFill, CancelText, new Color(0.84f, 0.83f, 0.82f));
            cancelButton.onClick.AddListener(ClearSelection);
            cardRotate = CloneButton(resetButton, card, "Rotate", "회전", null);
            Style(cardRotate, Amber100, Amber900, Amber400);
            cardRotate.onClick.AddListener(Rotate);
            foreach (var button in new[] { cancelButton, cardRotate }) button.GetComponentInChildren<TMP_Text>(true).fontSize = 26f;
        }

        /// <summary>18px(시안) 격자 한 칸 = 43px 타일 (위 · 왼쪽 선).</summary>
        static Sprite GridSprite()
        {
            const int size = 43;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                var x = i % size;
                var y = i / size;
                pixels[i] = x < 2 || y >= size - 2 ? GridLine : Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>왼쪽 진함 → 오른쪽 투명 (가장자리 그늘).</summary>
        static Sprite FadeSprite()
        {
            var texture = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var x = 0; x < 32; x++) texture.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - x / 31f));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 32, 1), new Vector2(0.5f, 0.5f));
        }

        // ── 도우미 ───────────────────────────────────────────

        static Button CloneButton(Button source, Transform parent, string name, string text, Color? color)
        {
            var button = Instantiate(source, parent);
            button.name = name;
            button.onClick.RemoveAllListeners();
            button.interactable = true;
            if (color is { } c && button.image != null) button.image.color = c;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
            return button;
        }

        /// <summary>버튼 바탕 · 글자 색 + 테두리.</summary>
        static void Style(Button button, Color fill, Color text, Color line)
        {
            button.image.color = fill;
            if (button.GetComponentInChildren<TMP_Text>(true) is { } label) label.color = text;
            AddLine(button.image, line, 3f);
        }

        static void AddLine(Graphic graphic, Color color, float width)
        {
            if (!graphic.TryGetComponent(out Outline outline)) outline = graphic.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.useGraphicAlpha = false;
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
        }

        Image NewImage(string name, RectTransform parent, Color color, bool raycast)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = cellTemplate.GetComponent<Image>().sprite; // 둥근 모서리 UISprite (Sliced)
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static TMP_Text NewText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>환불 배지처럼 글자 너비에 맞춰 늘어나는 알약.</summary>
        static void FitBadge(TMP_Text label, float padding)
        {
            var rect = (RectTransform)label.transform.parent;
            rect.sizeDelta = new Vector2(label.GetPreferredValues(label.text).x + padding * 2f, 44f);
        }

        /// <summary>부모를 채우되 좌 · 우 · 위 · 아래 여백.</summary>
        static void Stretch(RectTransform r, float left, float right, float top = 0f, float bottom = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>부모 왼쪽 가운데 기준: 왼쪽 x, 너비 · 높이.</summary>
        static void SetLeftMiddle(RectTransform r, float x, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(x, 0f);
            r.sizeDelta = new Vector2(width, height);
        }

        /// <summary>부모 왼쪽 위 기준: 왼쪽 x, 위에서 y, 너비 · 높이.</summary>
        static void SetLeftTop(RectTransform r, float x, float y, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        /// <summary>페이지 왼쪽 아래 기준 가운데 자리 · 크기.</summary>
        static void Put(RectTransform r, Vector2 center, Vector2 size) => Put(r, Vector2.zero, center, size);

        static void Put(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        static void PlaceButton(Button button, Vector2 center, Vector2 size) => PlaceButton(button, Vector2.zero, center, size);

        /// <summary>버튼 자리 · 크기 + 글자 칸도 버튼 크기에 맞춤 (DialogButton 글자는 240 × 80 고정).</summary>
        static void PlaceButton(Button button, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Put((RectTransform)button.transform, anchor, position, size);
            if (button.GetComponentInChildren<TMP_Text>(true) is { } label) label.rectTransform.sizeDelta = size - new Vector2(16f, 8f);
        }
    }
}
