using System.Collections.Generic;
using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 레벨업 연출 v2 (GDD 10.2, 2026-11-25 자물쇠 해금 연출로 교체). 게임은 멈추지 않고 입력도 막지 않는다.
    /// 여러 레벨이 한꺼번에 오르면 차례로 보여준다.
    ///
    /// 순서 (초, DOTween Sequence 하나): 배너 팝인 0 → 열쇠가 아래에서 올라와 꽂힘 0.2 · 0.65 → 자물쇠 1~2회 흔들림 0.8
    /// → 열쇠 90° 돌림(두께 보임) 1.13 → 고리가 위로 뽑힘(오른쪽 긴 다리는 몸통에 남음) 1.28
    /// → 뱃지 펀치 · 플래시 · 고리 2개 · 별 버스트 · 플레이어 위 색종이 1.4 → `LEVEL UP! Lv.N` 1.46 → 해금 칩 순차 1.64
    /// → <see cref="holdDuration"/> 뒤 위로 떠오르며 사라짐.
    ///
    /// 그림은 전부 정점 도형(<see cref="UIShape"/> · <see cref="LevelUpKeyGraphic"/> · <see cref="LevelUpShackleGraphic"/> ·
    /// <see cref="LevelUpBurst"/>)이라 텍스처가 기본 흰색 하나 → 같은 Canvas(배너 루트의 하위 Canvas) 안에서 묶인다.
    /// 파티클은 오브젝트 없이 <see cref="LevelUpBurst"/> 메시 하나. 해금 칩은 미리 만든 칸을 다시 쓴다.
    /// 구조는 SiteBuilder.BuildLevelUpBanner가 만든다.
    /// </summary>
    public sealed class LevelUpBanner : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] CanvasGroup group;
        [SerializeField, Tooltip("배너 + 뱃지 (팝인 · 퇴장 때 통째로 움직임)")] RectTransform popup;

        [Header("Banner")]
        [SerializeField] RectTransform banner;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField, Tooltip("해금 칩 칸 (미리 만들어 둔 것, 모자라면 뒤는 생략)")] RectTransform[] chips;

        [Header("Badge")]
        [SerializeField] RectTransform badge;
        [SerializeField] Graphic flash;
        [SerializeField, Tooltip("흔들림 축 (자물쇠 몸통 아래쪽)")] RectTransform lockPivot;
        [SerializeField] RectTransform shackle;
        [SerializeField, Tooltip("열쇠 위치 (피벗 = 열쇠 끝)")] RectTransform keyPos;
        [SerializeField] LevelUpKeyGraphic key;
        [SerializeField] UIShape[] rings;
        [SerializeField] LevelUpBurst stars;

        [Header("World")]
        [SerializeField, FormerlySerializedAs("burst"), Tooltip("플레이어 위 색종이 (없어도 됨)")] ParticleSystem confetti;

        [Header("Tuning")]
        [SerializeField, Min(0.1f), Tooltip("칩이 다 나온 뒤 머무는 시간 `임시`")] float holdDuration = 2f;
        [SerializeField, Tooltip("뱃지 설계 단위 1 = UI 몇 px (그래픽들과 같게)")] float unit = 1.5f;

        // 칩 레이아웃 (px)
        const float TitleBottom = 168f, ChipGapTop = 14f, ChipHeight = 38f, ChipGapX = 9f, ChipGapY = 10f, ChipPadX = 18f, BottomPad = 30f, SidePad = 34f;
        static readonly Color ChipFill = new Color32(0xFF, 0xF3, 0xCF, 0xFF);
        static readonly Color ChipLine = new Color32(0xBF, 0x7B, 0x33, 0xFF);
        static readonly Color ChipText = new Color32(0x5A, 0x2D, 0x00, 0xFF);
        static readonly Color LabelFill = new Color32(0xCD, 0x61, 0x00, 0xFF);
        static readonly Color LabelLine = new Color32(0x8A, 0x3F, 0x00, 0xFF);

        readonly Queue<int> pending = new();
        readonly List<(string text, bool label)> chipItems = new();
        Sequence playing;

        Vector2 popupHome, shackleHome, keyHome;
        CanvasGroup[] chipGroups;
        UIShape[] chipShapes;
        TMP_Text[] chipLabels;
        Vector2[] chipHome;

        void Awake()
        {
            if (popup == null || key == null || stars == null || chips == null)
            {
                // v1 구조로 저장된 씬 (Build Site를 다시 돌리지 않은 씬)
                Debug.LogWarning("[LevelUpBanner] 새 배너 구조가 아님 — APC/Setup/Build Site로 다시 만들어야 레벨업 연출이 나온다.", this);
                enabled = false;
                return;
            }
            popupHome = popup.anchoredPosition;
            shackleHome = shackle.anchoredPosition;
            keyHome = keyPos.anchoredPosition;
            chipGroups = new CanvasGroup[chips.Length];
            chipShapes = new UIShape[chips.Length];
            chipLabels = new TMP_Text[chips.Length];
            chipHome = new Vector2[chips.Length];
            for (int i = 0; i < chips.Length; i++)
            {
                chipGroups[i] = chips[i].GetComponent<CanvasGroup>();
                chipShapes[i] = chips[i].GetComponent<UIShape>();
                chipLabels[i] = chips[i].GetComponentInChildren<TMP_Text>(true);
            }
        }

        void Start()
        {
            if (!enabled) return;
            group.blocksRaycasts = false;
            group.interactable = false;
            Hide();
            GameManager.Instance.Level.LeveledUp.Subscribe(Enqueue).AddTo(this);
        }

        /// <summary>떠 있는 배너 · 기다리는 배너를 바로 끈다. 연출은 게임 시간이라 시간이 멈추는 화면(벨트 설치)에서 멈춘 채 남지 않게.</summary>
        public void Dismiss()
        {
            if (popup == null || stars == null) return; // v1 구조 씬
            pending.Clear();
            playing?.Kill();
            playing = null;
            Hide();
        }

        void Hide()
        {
            group.alpha = 0f;
            stars.Time = -1f;
            popup.gameObject.SetActive(false);
        }

        void Enqueue(int level)
        {
            pending.Enqueue(level);
            if (playing == null) PlayNext();
        }

        void PlayNext()
        {
            if (pending.Count == 0)
            {
                playing = null;
                Hide();
                return;
            }

            int level = pending.Dequeue();
            titleLabel.text = $"LEVEL UP! <color=#CD6100>Lv.{level}</color>";
            int shown = FillChips(level);

            popup.gameObject.SetActive(true);
            ResetPose(shown);
            playing = BuildSequence(shown).SetLink(gameObject).OnComplete(PlayNext);
        }

        // ------------------------------------------------------------------ 연출

        void ResetPose(int shown)
        {
            group.alpha = 0f;
            popup.anchoredPosition = popupHome;
            popup.localScale = Vector3.one * 0.7f;

            lockPivot.localRotation = Quaternion.identity;
            shackle.anchoredPosition = shackleHome;
            keyPos.anchoredPosition = keyHome + new Vector2(0f, -64f * unit); // 아래에서 올라옴
            key.Turn = 0f;
            key.canvasRenderer.SetAlpha(0f);
            badge.localScale = Vector3.one;
            flash.canvasRenderer.SetAlpha(0f);
            foreach (var ring in rings)
            {
                ring.rectTransform.localScale = Vector3.one;
                ring.RingWidth = 5f;
                ring.canvasRenderer.SetAlpha(0f);
            }
            stars.Restart();

            titleLabel.alpha = 0f;
            titleLabel.rectTransform.localScale = Vector3.one * 1.6f;
            for (int i = 0; i < shown; i++)
            {
                chipGroups[i].alpha = 0f;
                chips[i].localScale = Vector3.one * 0.8f;
                chips[i].anchoredPosition = chipHome[i] + new Vector2(0f, -16f);
            }
        }

        Sequence BuildSequence(int shown)
        {
            var s = DOTween.Sequence();
            const float Burst = 1.40f;

            // 배너 팝인 0.7 → 1.06 → 1
            s.Insert(0f, group.DOFade(1f, 0.2f));
            s.Insert(0f, popup.DOScale(1.06f, 0.224f).SetEase(Ease.OutQuad));
            s.Insert(0.224f, popup.DOScale(1f, 0.096f).SetEase(Ease.InOutSine));

            // 열쇠: 아래에서 올라와(조금 덜 들어간 자리) → 4 더 밀어 넣기
            s.Insert(0.2f, keyPos.DOAnchorPos(keyHome + new Vector2(0f, -4f * unit), 0.45f).SetEase(Ease.OutQuart));
            s.Insert(0.2f, DOVirtual.Float(0f, 1f, 0.45f, a => key.canvasRenderer.SetAlpha(a)).SetEase(Ease.OutQuart));
            s.Insert(0.65f, keyPos.DOAnchorPos(keyHome, 0.13f).SetEase(Ease.OutQuad));

            // 돌리기 전 1~2회 가볍게 흔들림 (몸통 아래쪽 축): 0 → 7° → −6° → 2° → 0
            s.Insert(0.80f, lockPivot.DOLocalRotate(new Vector3(0f, 0f, 7f), 0.075f).SetEase(Ease.InOutSine));
            s.Insert(0.875f, lockPivot.DOLocalRotate(new Vector3(0f, 0f, -6f), 0.09f).SetEase(Ease.InOutSine));
            s.Insert(0.965f, lockPivot.DOLocalRotate(new Vector3(0f, 0f, 2f), 0.075f).SetEase(Ease.InOutSine));
            s.Insert(1.04f, lockPivot.DOLocalRotate(Vector3.zero, 0.06f).SetEase(Ease.InOutSine));

            // 열쇠 90° 돌림 → 고리가 위로 뽑힘
            s.Insert(1.13f, DOVirtual.Float(0f, 1f, 0.22f, t => key.Turn = t).SetEase(Ease.InOutCubic));
            s.Insert(1.28f, shackle.DOAnchorPosY(shackleHome.y + 13f * unit, 0.16f).SetEase(Ease.OutBack));

            // 해금 순간: 뱃지 펀치 + 밝기 플래시, 열쇠 사라짐, 고리 2개, 별 버스트, 플레이어 위 색종이
            s.Insert(Burst, badge.DOScale(1.22f, 0.084f).SetEase(Ease.OutQuad));
            s.Insert(Burst + 0.084f, badge.DOScale(1f, 0.156f).SetEase(Ease.OutQuad));
            s.Insert(Burst, DOVirtual.Float(0f, 0.5f, 0.084f, a => flash.canvasRenderer.SetAlpha(a)));
            s.Insert(Burst + 0.084f, DOVirtual.Float(0.5f, 0f, 0.156f, a => flash.canvasRenderer.SetAlpha(a)));
            s.Insert(Burst, DOVirtual.Float(1f, 0f, 0.19f, a => key.canvasRenderer.SetAlpha(a)).SetEase(Ease.Linear));
            for (int i = 0; i < rings.Length; i++)
            {
                var ring = rings[i];
                float t = Burst + 0.06f * i;
                s.Insert(t, ring.rectTransform.DOScale(1.75f, 0.48f).SetEase(Ease.OutQuart));
                s.Insert(t, DOVirtual.Float(0f, 1f, 0.48f, p =>
                {
                    ring.canvasRenderer.SetAlpha(0.9f * (1f - p));
                    ring.RingWidth = Mathf.Lerp(5f, 1f, p);
                }).SetEase(Ease.OutQuart));
            }
            s.Insert(Burst, DOVirtual.Float(0f, stars.TotalDuration, stars.TotalDuration, t => stars.Time = t).SetEase(Ease.Linear));
            s.InsertCallback(Burst + stars.TotalDuration, () => stars.Time = -1f);
            if (confetti != null) s.InsertCallback(Burst, () => confetti.Play());

            // 글자 1.6 → 0.95 → 1
            s.Insert(1.46f, DOVirtual.Float(0f, 1f, 0.216f, a => titleLabel.alpha = a));
            s.Insert(1.46f, titleLabel.rectTransform.DOScale(0.95f, 0.216f).SetEase(Ease.OutQuad));
            s.Insert(1.676f, titleLabel.rectTransform.DOScale(1f, 0.144f).SetEase(Ease.OutQuad));

            // 해금 칩 70ms 간격
            for (int i = 0; i < shown; i++)
            {
                float t = 1.64f + 0.07f * i;
                s.Insert(t, chipGroups[i].DOFade(1f, 0.3f));
                s.Insert(t, chips[i].DOScale(1f, 0.3f).SetEase(Ease.OutBack));
                s.Insert(t, chips[i].DOAnchorPos(chipHome[i], 0.3f).SetEase(Ease.OutBack));
            }

            // 머문 뒤 위로 떠오르며 사라짐
            float settled = shown > 0 ? 1.64f + 0.07f * (shown - 1) + 0.3f : 1.82f;
            float exit = settled + holdDuration;
            s.Insert(exit, popup.DOAnchorPosY(popupHome.y + 67f, 0.42f).SetEase(Ease.InQuad));
            s.Insert(exit, group.DOFade(0f, 0.42f).SetEase(Ease.InQuad));
            return s;
        }

        // ------------------------------------------------------------------ 해금 칩

        /// <summary>해금 문구를 칩으로 나눠 채우고 줄 바꿈 배치 · 배너 높이를 맞춘다. 보이는 칩 수를 돌려준다.</summary>
        int FillChips(int level)
        {
            chipItems.Clear();
            AddNote(GameManager.Instance.Database.Levels.GetUnlockNote(level));
            foreach (var company in GameManager.Instance.Database.Companies)
                if (company.UnlockLevel == level) AddNote($"새 트럭: {company.DisplayName}"); // GDD 6.3

            int shown = Mathf.Min(chipItems.Count, chips.Length);
            float maxWidth = banner.rect.width - SidePad * 2f;
            for (int i = 0; i < chips.Length; i++)
            {
                bool on = i < shown;
                chips[i].gameObject.SetActive(on);
                if (!on) continue;
                var (text, label) = chipItems[i];
                chipLabels[i].text = text;
                chipLabels[i].color = label ? Color.white : ChipText;
                chipShapes[i].SetColors(label ? LabelFill : ChipFill, label ? LabelLine : ChipLine);
                float width = Mathf.Min(maxWidth, chipLabels[i].GetPreferredValues(text).x + ChipPadX * 2f);
                chips[i].sizeDelta = new Vector2(width, ChipHeight);
            }

            // 줄 나누기 → 줄마다 가운데 정렬
            int rows = 0, rowStart = 0;
            float rowWidth = 0f;
            for (int i = 0; i <= shown; i++)
            {
                bool flush = i == shown || (i > rowStart && rowWidth + ChipGapX + chips[i].sizeDelta.x > maxWidth);
                if (flush && i > rowStart)
                {
                    float x = -rowWidth * 0.5f;
                    float y = -(TitleBottom + ChipGapTop + rows * (ChipHeight + ChipGapY) + ChipHeight * 0.5f);
                    for (int k = rowStart; k < i; k++)
                    {
                        float w = chips[k].sizeDelta.x;
                        chipHome[k] = new Vector2(x + w * 0.5f, y);
                        x += w + ChipGapX;
                    }
                    rows++;
                    rowStart = i;
                    rowWidth = 0f;
                }
                if (i == shown) break;
                rowWidth += (rowWidth > 0f ? ChipGapX : 0f) + chips[i].sizeDelta.x;
            }

            float height = rows > 0
                ? TitleBottom + ChipGapTop + rows * ChipHeight + (rows - 1) * ChipGapY + BottomPad
                : TitleBottom + BottomPad;
            banner.sizeDelta = new Vector2(banner.sizeDelta.x, height);
            return shown;
        }

        /// <summary>"A · B\n건설: C · D" → [A][B][건설][C][D]. "이름: 내용"은 이름을 진한 라벨 칩으로.</summary>
        void AddNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note)) return;
            foreach (var line in note.Split('\n'))
            {
                foreach (var raw in line.Split('·'))
                {
                    var part = raw.Trim();
                    if (part.Length == 0) continue;
                    int colon = part.IndexOf(':');
                    if (colon > 0)
                    {
                        chipItems.Add((part.Substring(0, colon).Trim(), true));
                        part = part.Substring(colon + 1).Trim();
                        if (part.Length == 0) continue;
                    }
                    chipItems.Add((part, false));
                }
            }
        }
    }
}
