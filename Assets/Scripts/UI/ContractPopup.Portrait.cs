using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 책상 팝업 세로 배치 (UI 시안 office_clipboard_ui_mobile, 2026-11-26).
    /// 씬에 놓인 배치 = 가로. 화면 방향 플래그(세로 = 높이 > 너비)가 바뀌면 처음 값으로 되돌린 뒤 세로면 아래 값을 덮어쓴다.
    /// 세로: 위 HUD 아래 판(위아래 여백), 위 집게, 겹친 종이, 아래 가로 인덱스 탭(원래 비율, 윗부분이 종이 밑),
    /// 고용 = 가로로 긴 이력서 카드 세로 쌓기 + 아래 도장 받침(설명 | 도장 칸), 스탯 · 트럭 · 설비 = 한 줄, 명부 = 한 열, 모듈 = ModulePanel.Layout(세로 쌓기).
    /// 작은 글씨는 키우고, 버튼은 더 둥글게. 값은 모두 `임시`.
    /// </summary>
    public sealed partial class ContractPopup
    {
        const float PanelSide = 24f;        // 판 좌우 여백
        const float PanelTop = 280f;        // 판이 들어갈 영역 위 여백 (HUD 레벨 · 돈 줄과 집게가 겹치지 않게)
        const float PanelBottom = 110f;     // 판이 들어갈 영역 아래 여백
        const float PanelHeightRatio = 0.8f; // 판 높이 = 영역 × 이 값, 남는 곳은 위아래 반씩
        const float PaperInset = 34f;       // 종이 더미 ↔ 판 좌우 · 아래 가장자리 (가로 배치 22보다 여유 있게)
        const float ClipRoom = 52f;         // 종이 위 집게 자리
        const float ContentInset = 56f;     // 종이 좌우 가장자리 ↔ 내용 (제목 · 카드 · 행 · 받침 · 명부 · 모듈)
        const float ContentDrop = 12f;      // 종이 위 가장자리 쪽 여백 추가 (제목 · 구분선 · 내용을 이만큼 내림)
        const float PageEdge = ContentInset + 22f; // 페이지(종이보다 22 큼) 가장자리 ↔ 내용
        [Header("세로 배치 · 인덱스 탭 (임시, Play 중 바꾸면 바로 반영)")]
        [SerializeField, Min(40f), Tooltip("탭 길이 (화면 세로, 종이 밑으로 들어간 부분 포함)")] float tabLength = 130f;
        [SerializeField, Min(0f), Tooltip("탭 사이. 탭들이 합쳐서 종이 아래 너비를 채운다")] float tabGap = 12f;
        [SerializeField, Range(0.5f, 4f), Tooltip("9슬라이스 pixelsPerUnitMultiplier. 키우면 띠(가로 라인)가 윗변 쪽으로 올라가고 모서리가 작아진다")] float tabRound = 1.4f;
        [SerializeField, Min(0f), Tooltip("선택 탭 윗변이 종이 밑으로 들어간 길이 (안 고른 탭은 +7). 키우면 탭 전체가 올라간다")] float portraitTabTuck = 28f;
        const float FontScale = 1.2f;       // 작은 글씨(SmallFont 이하) 배율
        const float SmallFont = 28f;
        const float ButtonRound = 0.45f;    // 버튼 pixelsPerUnitMultiplier (작을수록 둥긂)
        const float CardsTop = 270f;        // 페이지 위에서 이력서 카드 시작
        const float CardMinHeight = 262f;
        const float CardMaxHeight = 300f;
        const float CardGap = 24f;
        const float TrayTabClearance = 18f; // 받침 아래 ↔ 종이 밑으로 들어간 탭 윗변 (안 고른 탭 +7 기준)

        // 받침 아래 = 종이 아래 가장자리 + 탭이 종이 밑으로 들어간 길이(+7) + 여유 → 탭과 닿지 않음
        float TrayBottom => PaperInset + portraitTabTuck + 7f + TrayTabClearance;
        const float TrayHeight = 240f;
        const float SlotSize = 204f;        // 도장 칸 (도장보다 36 큼)
        const float StampSize = 168f;

        static readonly Vector2 TopLeft = new(0f, 1f);
        static readonly Vector2 TopRight = new(1f, 1f);
        static readonly Vector2 TopCenter = new(0.5f, 1f);
        static readonly Vector2 Center = new(0.5f, 0.5f);
        static readonly Color TrayColor = new(0.94f, 0.898f, 0.812f);     // #f0e5cf
        static readonly Color SlotColor = new(0.906f, 0.831f, 0.702f);    // #e7d4b3
        static readonly Color SlotLine = new(0.749f, 0.635f, 0.455f);     // #bfa274
        static readonly Color TrayTitle = new(0.27f, 0.17f, 0.08f);       // #452b14
        static readonly Color TrayText = new(0.43f, 0.31f, 0.2f);         // #6e4e32

        struct RectState
        {
            public RectTransform Rect;
            public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        struct TextState
        {
            public TMP_Text Text;
            public float Size, Min, Max;
            public bool Auto;
        }

        readonly List<RectState> landscape = new();
        readonly List<TextState> landscapeTexts = new();
        readonly List<(Image image, float round)> buttonImages = new();
        readonly List<(Image image, Image.Type type, float round)> tabImages = new(); // 가로 = Simple
        GridLayoutGroup[] rosterGrids;
        Vector2[] rosterCells;
        int[] rosterColumns;
        HorizontalLayoutGroup candidatesLayout;
        Stamp stamp;
        GameObject tray; // 세로에서만 만드는 도장 받침
        Button[] shownTabs;
        bool portrait;   // 지금 적용된 배치 (세로 = true)
        float portraitStackWidth;

        static bool IsPortrait() => ScreenLayout.Portrait; // 화면 방향 플래그 (M21-1)

        void CaptureLandscape()
        {
            foreach (var r in body.GetComponentsInChildren<RectTransform>(true))
            {
                landscape.Add(new RectState
                {
                    Rect = r, AnchorMin = r.anchorMin, AnchorMax = r.anchorMax, Pivot = r.pivot,
                    Position = r.anchoredPosition, Size = r.sizeDelta, Rotation = r.localRotation, Scale = r.localScale,
                });
            }
            foreach (var t in body.GetComponentsInChildren<TMP_Text>(true))
                landscapeTexts.Add(new TextState { Text = t, Size = t.fontSize, Min = t.fontSizeMin, Max = t.fontSizeMax, Auto = t.enableAutoSizing });
            foreach (var b in body.GetComponentsInChildren<Button>(true))
            {
                if (b.image != null && b.image.type == Image.Type.Sliced) buttonImages.Add((b.image, b.image.pixelsPerUnitMultiplier));
            }
            foreach (var tab in tabs) tabImages.Add((tab.image, tab.image.type, tab.image.pixelsPerUnitMultiplier));
            rosterGrids = rosterPage.GetComponentsInChildren<GridLayoutGroup>(true);
            rosterCells = new Vector2[rosterGrids.Length];
            rosterColumns = new int[rosterGrids.Length];
            for (var i = 0; i < rosterGrids.Length; i++)
            {
                rosterCells[i] = rosterGrids[i].cellSize;
                rosterColumns[i] = rosterGrids[i].constraintCount;
            }
            candidatesLayout = hirePage.GetComponentInChildren<HorizontalLayoutGroup>(true);
            stamp = stampArea.GetComponentInChildren<Stamp>(true);
        }

        /// <summary>처음(가로) 값으로 되돌리고, 세로면 세로 값을 덮어쓴다.</summary>
        void SetLayout(bool toPortrait)
        {
            foreach (var s in landscape)
            {
                if (s.Rect == null) continue;
                s.Rect.anchorMin = s.AnchorMin;
                s.Rect.anchorMax = s.AnchorMax;
                s.Rect.pivot = s.Pivot;
                s.Rect.anchoredPosition = s.Position;
                s.Rect.sizeDelta = s.Size;
                s.Rect.localRotation = s.Rotation;
                s.Rect.localScale = s.Scale;
            }
            foreach (var s in landscapeTexts)
            {
                if (s.Text == null) continue;
                s.Text.fontSize = s.Size;
                s.Text.fontSizeMin = s.Min;
                s.Text.fontSizeMax = s.Max;
                s.Text.enableAutoSizing = s.Auto;
            }
            foreach (var (image, round) in buttonImages) image.pixelsPerUnitMultiplier = round;
            foreach (var (image, type, round) in tabImages)
            {
                image.type = type;
                image.pixelsPerUnitMultiplier = round;
            }
            for (var i = 0; i < rosterGrids.Length; i++)
            {
                rosterGrids[i].cellSize = rosterCells[i];
                rosterGrids[i].constraintCount = rosterColumns[i];
            }
            if (candidatesLayout != null) candidatesLayout.enabled = true;
            if (tray != null) tray.SetActive(false);
            trayRestore?.Invoke();

            portrait = toPortrait;
            if (portrait) ApplyPortrait();
            else LayoutModules();
            if (stamp != null) stamp.SetHome(Rect(stamp).anchoredPosition);
        }

        // Play 중 인스펙터에서 탭 값을 바꾸면 열린 팝업에 바로 반영
        void OnValidate()
        {
            if (!Application.isPlaying || !IsOpen || !portrait || shownTabs == null) return;
            SetLayout(true); // 받침 위치도 탭 값에 따라 바뀐다
            PlaceTabs();
            for (var i = 0; i < tabs.Length; i++) SetTabLook(i, i == current, true);
        }

        /// <summary>
        /// 보이는 탭을 놓는다. 가로 = 오른쪽 세로 줄, 세로 = 종이 아래 가로 줄.
        /// 세로 탭은 가로 탭을 -90° 돌려 짧고 굵게(시안 하단 인덱스 탭): 띠 쪽 윗변이 종이 밑으로 들어가고 아래는 판 밖으로 나온다. 글자는 +90°로 바로 세운다.
        /// </summary>
        void PlaceTabs()
        {
            var n = shownTabs.Length;
            var length = tabLength;
            var thickness = portraitStackWidth / n - tabGap; // n개가 종이 아래 너비를 나눠 채움
            for (var i = 0; i < n; i++)
            {
                var rt = Rect(shownTabs[i]);
                if (!portrait)
                {
                    rt.anchoredPosition = new Vector2(tabX, tabY[i]);
                    continue;
                }
                rt.anchorMin = rt.anchorMax = new Vector2((i + 0.5f) / n, 0f);
                rt.pivot = new Vector2(0f, 0.5f); // 띠 쪽 = 세운 뒤 윗변 가운데
                rt.localRotation = Quaternion.Euler(0f, 0f, -90f);
                rt.sizeDelta = new Vector2(length, thickness);
                var image = shownTabs[i].image;
                image.type = Image.Type.Sliced; // 길게 늘려도 모서리 · 띠가 깨지지 않게 (Index 스프라이트 테두리)
                image.pixelsPerUnitMultiplier = tabRound;
                rt.anchoredPosition = new Vector2(0f, portraitTabTuck);
                if (rt.Find("Label") is RectTransform label)
                {
                    label.anchorMin = label.anchorMax = Center;
                    label.pivot = Center;
                    label.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    label.anchoredPosition = new Vector2(portraitTabTuck * 0.5f, 0f); // 탭 안쪽 x = 화면 아래: 종이 밑 부분을 피해 보이는 곳 가운데
                    label.sizeDelta = new Vector2(thickness - 20f, length - portraitTabTuck - 16f);
                    if (label.GetComponent<TMP_Text>() is { } text)
                    {
                        text.enableAutoSizing = true;
                        text.fontSizeMin = 22f;
                        text.fontSizeMax = 38f;
                    }
                }
            }
        }

        void ApplyPortrait()
        {
            var canvas = ((RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform).rect;
            var panelWidth = canvas.width - PanelSide * 2f;
            var area = canvas.height - PanelTop - PanelBottom;
            var panelHeight = area * PanelHeightRatio;
            var top = PanelTop + (area - panelHeight) * 0.5f;
            var bottom = PanelBottom + (area - panelHeight) * 0.5f;
            portraitStackWidth = panelWidth - PaperInset * 2f;
            var pageWidth = portraitStackWidth + 44f; // 페이지는 앞 종이보다 상하좌우 22씩 크다
            var pageHeight = panelHeight - PaperInset - ClipRoom + 44f;

            // 판 · 그림자 · 종이 더미 (종이는 판 가장자리 22 안까지, 탭은 판 밖으로)
            Stretch(panel, new Vector2(PanelSide, bottom), new Vector2(-PanelSide, -top));
            Stretch(body.transform.Find("BoardShadow") as RectTransform,
                new Vector2(PanelSide - 10f, bottom - 18f), new Vector2(-PanelSide + 10f, -top - 10f));
            Stretch((RectTransform)paperFront.parent, new Vector2(PaperInset, PaperInset), new Vector2(-PaperInset, -ClipRoom));

            // 글씨 · 버튼
            foreach (var s in landscapeTexts)
            {
                if (s.Text == null || s.Size > SmallFont) continue;
                s.Text.fontSize = s.Size * FontScale;
                s.Text.fontSizeMin = s.Min * FontScale;
                s.Text.fontSizeMax = s.Max * FontScale;
            }
            foreach (var (image, round) in buttonImages) image.pixelsPerUnitMultiplier = Mathf.Min(round, ButtonRound);

            // 종이 머리: 제목 · 설명을 닫기 버튼 앞까지 늘림, 긴 설명은 줄여서 한 줄
            Band(sheetTitle.rectTransform, 14f + ContentDrop, 56f, ContentInset, 120f);
            Band(sheetDesc.rectTransform, 62f + ContentDrop, 32f, ContentInset, 120f);
            Band(paperFront.Find("HeaderLine") as RectTransform, 98f + ContentDrop, 2f, ContentInset - 6f, ContentInset - 6f);
            sheetDesc.enableAutoSizing = true;
            sheetDesc.fontSizeMin = 18f;
            sheetDesc.fontSizeMax = sheetDesc.fontSize;

            LayoutHire(pageWidth, pageHeight);
            LayoutRows(upgradePage.transform, "Row0", pageWidth);
            LayoutRows(upgradePage.transform, "Row1", pageWidth);
            LayoutRows(truckPage.transform, "Row0", pageWidth);
            LayoutRows(truckPage.transform, "Row1", pageWidth);
            LayoutRows(facilityPage.transform.Find("RowTemplate"), "", pageWidth);
            LayoutRoster(pageWidth - PageEdge * 2f);
            LayoutModules();
            LayoutStampTray();
        }

        /// <summary>고용: 머리 줄(알바 수 · 교체 시간 · 새로고침) 아래로 가로로 긴 이력서 카드를 세로로 쌓는다. 카드 높이는 도장 받침 위까지 들어가게.</summary>
        void LayoutHire(float pageWidth, float pageHeight)
        {
            var root = hirePage.transform;
            Place(Child(root, "Header"), TopLeft, new Vector2(PageEdge + 270f, -165f - ContentDrop), new Vector2(540f, 60f)); // 오른쪽 교체 시간과 안 겹치게
            Place(Child(root, "Timer"), TopRight, new Vector2(-PageEdge - 150f, -160f - ContentDrop), new Vector2(300f, 56f));
            Place(Child(root, "Refresh"), TopRight, new Vector2(-PageEdge - 130f, -224f - ContentDrop), new Vector2(260f, 70f));

            var list = Child(root, "Candidates");
            if (list == null) return;
            if (candidatesLayout != null) candidatesLayout.enabled = false;
            var count = list.childCount;
            var trayTop = TrayBottom + TrayHeight - (PaperInset - 22f); // 페이지 아래(= 판 아래 + PaperInset − 22)에서 받침 윗변까지
            var cardsTop = CardsTop + ContentDrop;
            var room = pageHeight - cardsTop - trayTop - CardGap;
            var cardHeight = Mathf.Clamp((room - CardGap * (count - 1)) / count, CardMinHeight, CardMaxHeight);
            var cardWidth = pageWidth - PageEdge * 2f;
            var listHeight = count * (cardHeight + CardGap) - CardGap;
            Place(list, TopCenter, new Vector2(0f, -cardsTop), new Vector2(cardWidth, listHeight), TopCenter);
            list.localScale = Vector3.one * Mathf.Min(1f, room / listHeight); // 짧은 화면: 받침과 겹치지 않게 카드 묶음을 줄인다
            for (var i = 0; i < count; i++)
            {
                var card = (RectTransform)list.GetChild(i);
                Place(card, TopCenter, new Vector2(0f, -i * (cardHeight + CardGap)), new Vector2(cardWidth, cardHeight), TopCenter);
                LayoutCard(card, cardWidth);
            }
        }

        /// <summary>
        /// 카드 한 장 (위에서부터): 왼쪽 얼굴 · 이름 · 상태 | 오른쪽 고용금 · 시급 · 사유, 구분선, 이동 속도 | 적재 두 칸 (글자 + 게이지).
        /// </summary>
        static void LayoutCard(Transform card, float width)
        {
            var half = width * 0.5f;
            var column = (width - 100f) * 0.5f;
            var left = -column * 0.5f - 10f;
            var right = column * 0.5f + 10f;
            Place(Child(card, "Avatar"), TopCenter, new Vector2(-half + 85f, -80f), new Vector2(110f, 110f));
            Place(Child(card, "Title"), TopCenter, new Vector2(-half + 330f, -58f), new Vector2(340f, 50f));
            Place(Child(card, "Status"), TopCenter, new Vector2(-half + 330f, -110f), new Vector2(340f, 38f));
            Place(Child(card, "Cost"), TopCenter, new Vector2(half - 175f, -56f), new Vector2(310f, 52f));
            Place(Child(card, "Wage"), TopCenter, new Vector2(half - 175f, -108f), new Vector2(310f, 36f));
            Place(Child(card, "Reason"), TopCenter, new Vector2(half - 175f, -146f), new Vector2(310f, 32f));
            Place(Child(card, "DividerL"), TopCenter, new Vector2(0f, -172f), new Vector2(width - 60f, 2f));
            Place(Child(card, "DividerR"), TopCenter, new Vector2(0f, -172f), new Vector2(0f, 2f)); // 세로는 구분선 하나
            // 글자 · 게이지 왼쪽에 스탯 아이콘(글자의 자식 `Icon`, 42 + 사이 8)이 들어갈 자리를 비운다
            const float iconRoom = 50f;
            var bar = column - iconRoom;
            Place(Child(card, "Speed"), TopCenter, new Vector2(left + iconRoom * 0.5f, -200f), new Vector2(bar, 34f));
            Place(Child(card, "SpeedTrack"), TopCenter, new Vector2(left + iconRoom * 0.5f, -237f), new Vector2(bar, 14f));
            Place(Child(card, "Carry"), TopCenter, new Vector2(right + iconRoom * 0.5f, -200f), new Vector2(bar, 34f));
            Place(Child(card, "CarryTrack"), TopCenter, new Vector2(right + iconRoom * 0.5f, -237f), new Vector2(bar, 14f));
            if (Child(card, "Reason") is { } reason && reason.GetComponent<TMP_Text>() is { } text)
            {
                text.enableAutoSizing = true; // 사유는 오른쪽 칸 안에서 줄여 한 줄
                text.fontSizeMin = 18f;
                text.fontSizeMax = 28f;
            }
        }

        /// <summary>스탯 · 트럭 · 설비 한 칸: 왼쪽 위 이름, 왼쪽 아래 값(한 줄, 길면 줄임) | 오른쪽 [구매]. 칸 자리(y) · 높이는 가로 배치 그대로.</summary>
        static void LayoutRows(Transform root, string prefix, float pageWidth)
        {
            if (root == null) return;
            var box = Child(root, prefix + "Box");
            if (box == null) return;
            var y = box.anchoredPosition.y;
            var center = pageWidth * 0.5f;
            var width = pageWidth - PageEdge * 2f;
            var left = center - width * 0.5f;
            Place(box, TopLeft, new Vector2(center, y), new Vector2(width, box.sizeDelta.y));
            var textWidth = width - 300f; // 구매 버튼 앞까지
            var textX = left + 30f + textWidth * 0.5f;
            Place(Child(root, prefix + "Name"), TopLeft, new Vector2(textX, y + 24f), new Vector2(textWidth, 50f));
            var value = Child(root, prefix + "Value");
            Place(value, TopLeft, new Vector2(textX, y - 26f), new Vector2(textWidth, 44f));
            if (value != null && value.GetComponent<TMP_Text>() is { } valueText)
            {
                valueText.enableAutoSizing = true;
                valueText.fontSizeMin = 18f;
                valueText.fontSizeMax = 32f;
            }
            var buy = Child(root, prefix + "Buy");
            if (buy != null) Place(buy, TopLeft, new Vector2(left + width - 135f, y), new Vector2(230f, buy.sizeDelta.y));
        }

        /// <summary>명부: 직군 칸을 목록 너비로, 칸 안 알바 카드는 한 열.</summary>
        void LayoutRoster(float listWidth)
        {
            Place(Child(rosterPage.transform, "Header"), TopLeft, new Vector2(PageEdge + 350f, -150f - ContentDrop), new Vector2(700f, 60f));
            if (Child(rosterPage.transform, "List") is { } scroll) // 좌우 안쪽 여백, 위 여백만큼 내림
            {
                scroll.sizeDelta = new Vector2(-PageEdge * 2f, scroll.sizeDelta.y);
                scroll.anchoredPosition = new Vector2(0f, scroll.anchoredPosition.y - ContentDrop);
            }
            var content = Child(rosterPage.transform, "List/Content");
            if (content == null) return;
            foreach (RectTransform column in content)
            {
                column.sizeDelta = new Vector2(listWidth, column.sizeDelta.y);
                foreach (RectTransform part in column) part.sizeDelta = new Vector2(listWidth, part.sizeDelta.y);
            }
            foreach (var grid in rosterGrids)
            {
                grid.constraintCount = 1;
                grid.cellSize = new Vector2(listWidth - 40f, grid.cellSize.y);
            }
        }

        /// <summary>
        /// 모듈 탭 배치 (시안 module_placement_ui_mobile, 가로 · 세로 공통 부품) — 판정은 이 팝업의 방향 플래그 portrait.
        /// 페이지(종이 크기 그대로) 안, 종이 머리 구분선 아래부터. 좌우 = 다른 탭 내용과 같은 선(PageEdge), 아래 = 세로는 종이 밑으로 들어간 탭을 피해(도장 받침과 같은 선).
        /// </summary>
        void LayoutModules()
        {
            var bottom = portrait ? TrayBottom - (PaperInset - 22f) : 22f + 30f;
            modulePage.Layout(portrait, paperFront.Find("HeaderLine") as RectTransform, bottom, PageEdge);
        }

        /// <summary>
        /// 고용 탭 아래 도장 받침 (시안 `고용 승인 고무도장` 줄): 둥근 받침 왼쪽에 제목 · 설명(기존 Hint), 오른쪽 둥근 도장 칸 위에 도장.
        /// 받침 · 칸 · 제목은 세로에서 처음 쓸 때 만든다 (stampArea = 판 크기, 고용 탭에서만 켜짐).
        /// </summary>
        void LayoutStampTray()
        {
            var area = (RectTransform)stampArea.transform;
            var bottom = TrayBottom;
            var side = PaperInset + ContentInset; // 카드와 같은 좌우 선
            var mid = TrayHeight * 0.5f;
            if (tray == null) BuildTray(area);
            tray.SetActive(true);
            var trayRect = (RectTransform)tray.transform;
            trayRect.anchorMin = Vector2.zero;
            trayRect.anchorMax = new Vector2(1f, 0f);
            trayRect.offsetMin = new Vector2(side, bottom);
            trayRect.offsetMax = new Vector2(-side, bottom + TrayHeight);

            // 도장 칸 중심 = 받침 오른쪽 안 (판 오른쪽 아래 기준)
            var slotCenter = new Vector2(-side - 20f - SlotSize * 0.5f, bottom + TrayHeight * 0.5f);
            var textLeft = side + 28f;
            var textRight = side + 20f + SlotSize + 28f;
            var hint = stampArea.transform.Find("Hint") as RectTransform;
            if (hint != null)
            {
                hint.anchorMin = Vector2.zero;
                hint.anchorMax = new Vector2(1f, 0f);
                hint.offsetMin = new Vector2(textLeft, bottom + mid - 76f); // 받침 가운데 아래 (제목 밑)
                hint.offsetMax = new Vector2(-textRight, bottom + mid - 4f);
                if (hint.GetComponent<TMP_Text>() is { } hintText)
                {
                    hintText.alignment = TextAlignmentOptions.TopLeft;
                    hintText.color = TrayText;
                }
            }
            var title = (RectTransform)tray.transform.Find("Title");
            title.anchorMin = Vector2.zero;
            title.anchorMax = Vector2.one;
            title.offsetMin = new Vector2(28f, mid + 6f); // 받침 가운데 위
            title.offsetMax = new Vector2(-(20f + SlotSize + 28f), -(TrayHeight - mid - 66f));
            Place((RectTransform)tray.transform.Find("Slot"), new Vector2(1f, 0.5f), new Vector2(-20f - SlotSize * 0.5f, 0f), new Vector2(SlotSize, SlotSize));
            if (stamp != null) Place(Rect(stamp), new Vector2(1f, 0f), slotCenter, new Vector2(StampSize, StampSize));
        }

        void BuildTray(RectTransform area)
        {
            var sprite = panel.GetComponent<Image>().sprite; // 둥근 모서리 UISprite (Sliced)
            tray = NewImage("StampTray", area, sprite, TrayColor, 0.3f).gameObject;
            tray.transform.SetAsFirstSibling(); // 안내 글자 · 도장 뒤

            var slot = NewImage("Slot", tray.transform, sprite, SlotColor, 0.35f);
            var outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = SlotLine;
            outline.effectDistance = new Vector2(3f, -3f);

            var title = new GameObject("Title", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            title.transform.SetParent(tray.transform, false);
            title.font = sheetTitle.font;
            title.text = "고용 승인 도장";
            title.fontSize = 36f;
            title.fontStyle = FontStyles.Bold;
            title.color = TrayTitle;
            title.alignment = TextAlignmentOptions.BottomLeft;
            title.raycastTarget = false;

            // 원래 Hint 글자를 이 받침 설명으로 쓴다 — 가로로 돌아가면 SetLayout이 글자 정렬 · 색은 못 되돌리므로 여기서 기억
            if (stampArea.transform.Find("Hint")?.GetComponent<TMP_Text>() is { } hint)
            {
                var alignment = hint.alignment;
                var color = hint.color;
                trayRestore = () => { hint.alignment = alignment; hint.color = color; };
            }
        }

        System.Action trayRestore;

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color, float round)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = round;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform Child(Transform root, string path) => root.Find(path) as RectTransform;

        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size) => Place(r, anchor, position, size, Center);

        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            if (r == null) return;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        static void Stretch(RectTransform r, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (r == null) return;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
        }

        /// <summary>위쪽 가로 띠: 위에서 top 떨어져 높이 height, 좌우 여백 left · right.</summary>
        static void Band(RectTransform r, float top, float height, float left, float right)
        {
            if (r == null) return;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(left, -top - height);
            r.offsetMax = new Vector2(-right, -top);
        }
    }
}
