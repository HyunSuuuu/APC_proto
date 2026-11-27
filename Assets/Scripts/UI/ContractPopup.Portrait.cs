using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 책상 팝업 가로 · 세로 화면 바꾸기 (세로 UI 분리 2026-11-27).
    /// 가로 = <c>landscapeView</c>(`Body`), 세로 = <c>portraitView</c>(`BodyPortrait`) — 모양은 씬에서 각각 잡고, 화면 방향 플래그에 맞는 한 벌만 켠다.
    /// 아직 코드로 놓는 것: 탭 자리(책상마다 탭 3 · 4개, 세로 = 종이 아래 가로 줄) · 모듈 탭(ModulePanel.Layout, 세로 UI 분리 5단계).
    /// </summary>
    public sealed partial class ContractPopup
    {
        [Header("세로 인덱스 탭 (임시, Play 중 바꾸면 바로 반영)")]
        [SerializeField, Min(40f), Tooltip("탭 길이 (화면 세로, 종이 밑으로 들어간 부분 포함)")] float tabLength = 130f;
        [SerializeField, Min(0f), Tooltip("탭 사이. 탭들이 합쳐서 종이 아래 너비를 채운다")] float tabGap = 12f;
        [SerializeField, Range(0.5f, 4f), Tooltip("9슬라이스 pixelsPerUnitMultiplier. 키우면 띠(가로 라인)가 윗변 쪽으로 올라가고 모서리가 작아진다")] float tabRound = 1.4f;
        [SerializeField, Min(0f), Tooltip("선택 탭 윗변이 종이 밑으로 들어간 길이 (안 고른 탭은 +7). 키우면 탭 전체가 올라간다")] float portraitTabTuck = 28f;

        // 모듈 탭 세로 배치(ModulePanel.Layout)에 넘기는 값 — 굽기 전 세로 배치와 같은 선 (5단계에서 씬으로 옮기면 지운다)
        const float PaperInset = 34f;       // 종이 더미 ↔ 판 가장자리 (BodyPortrait)
        const float PageEdge = 56f + 22f;   // 페이지 가장자리 ↔ 내용
        const float TrayTabClearance = 18f; // 도장 받침 아래 ↔ 탭 윗변
        float TrayBottom => PaperInset + portraitTabTuck + 7f + TrayTabClearance;

        static readonly Vector2 Center = new(0.5f, 0.5f);

        Stamp stamp;
        Button[] shownTabs;
        bool portrait; // 지금 켠 화면 (세로 = true)

        static bool IsPortrait() => ScreenLayout.Portrait; // 화면 방향 플래그 (M21-1)

        /// <summary>방향에 맞는 화면 한 벌을 고른다 (켜는 것은 ShowView). 세로 화면이 비어 있으면 가로 화면을 그대로 쓴다.</summary>
        void SetView(bool toPortrait)
        {
            portrait = toPortrait && portraitView != null;
            view = portrait ? portraitView : landscapeView;
            SetViewArrays();
            stamp = stampArea.GetComponentInChildren<Stamp>(true);
            if (stamp != null) stamp.SetHome(Rect(stamp).anchoredPosition); // 켜기 전에 — 이 화면 도장의 제자리
        }

        // Play 중 인스펙터에서 탭 값을 바꾸면 열린 팝업에 바로 반영
        void OnValidate()
        {
            if (!Application.isPlaying || !IsOpen || !portrait || shownTabs == null) return;
            LayoutModules(); // 모듈 탭 아래 선이 탭 값에 따라 바뀐다
            PlaceTabs();
            for (var i = 0; i < tabs.Length; i++) SetTabLook(i, i == current, true);
        }

        /// <summary>
        /// 보이는 탭을 놓는다. 가로 = 오른쪽 세로 줄(처음 고용 · 배치 · 스탯 자리), 세로 = 종이 아래 가로 줄.
        /// 세로 탭은 가로 탭을 -90° 돌려 짧고 굵게(시안 하단 인덱스 탭): 띠 쪽 윗변이 종이 밑으로 들어가고 아래는 판 밖으로 나온다. 글자는 +90°로 바로 세운다.
        /// 보이는 탭 수(3 · 4)로 종이 더미 폭을 나눠 채운다 — 화면을 켠 뒤에 불러야 폭이 새 캔버스 크기다.
        /// </summary>
        void PlaceTabs()
        {
            var n = shownTabs.Length;
            var width = 0f;
            if (portrait)
            {
                Canvas.ForceUpdateCanvases();
                width = ((RectTransform)paperFront.parent).rect.width;
            }
            var thickness = width / n - tabGap;
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
                rt.sizeDelta = new Vector2(tabLength, thickness);
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
                    label.sizeDelta = new Vector2(thickness - 20f, tabLength - portraitTabTuck - 16f);
                    if (label.GetComponent<TMP_Text>() is { } text)
                    {
                        text.enableAutoSizing = true;
                        text.fontSizeMin = 22f;
                        text.fontSizeMax = 38f;
                    }
                }
            }
        }

        /// <summary>
        /// 모듈 탭 배치 (시안 module_placement_ui_mobile, 가로 · 세로 공통 부품) — 판정은 지금 화면 방향 portrait.
        /// 페이지 안, 종이 머리 구분선 아래부터. 좌우 = PageEdge, 아래 = 세로는 종이 밑으로 들어간 탭을 피해(도장 받침과 같은 선).
        /// </summary>
        void LayoutModules()
        {
            var bottom = portrait ? TrayBottom - (PaperInset - 22f) : 22f + 30f;
            modulePage.Layout(portrait, paperFront.Find("HeaderLine") as RectTransform, bottom, PageEdge);
        }
    }
}
