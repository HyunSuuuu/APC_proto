using System.Collections.Generic;
using APC.Core;
using APC.Economy;
using APC.Staff;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 하루 정산 = 영수증 출력기 (GDD 9.5, 12.3, UI 샘플 BillScene 방식 2026-11-20). 2파트가 끝나면 열리고 게임이 멈춘다.
    /// 배경이 어두워지고 출력기가 아래에서 올라온 뒤, 영수증이 줄마다 한 칸씩 밀려 올라오며(출력기 떨림 · LED 깜빡임)
    /// 머리(`[일일 물류 정산서]` · DAY N) → 상차 수익 · 알바 시급 · 출고(트럭 · 상자) · 반품 처리 → 순수익 + `정산 완료` 도장 쾅. 출력 중 화면을 누르면 끝까지 건너뛴다.
    /// 출력기 = 배출구 · 몸체를 틈 없이 쌓고, 영수증은 그 위 마스크 안에서 배출구 틈부터 올라온다 (2026-11-20).
    /// 영수증 윗부분은 나온 길이만큼 화면 안쪽(뒤)으로 말려 넘어간다 (ReceiptCurl, UI 샘플과 같음). 줄이 나올 때마다 살짝 펄럭이고, 뜯을 때 더 말린다.
    /// 출력이 끝나면 조금 더 밀려 나오고 배출구 바로 위에 절취선이 나타난다 (ReceiptTear, 2026-11-23).
    /// [확인] = 화면 어디를 눌러도 (배경 · 종이 · 출력기) → 절취선을 따라 왼쪽부터 '드-드-드-득' 뜯기며(단계마다 화면 흔들림, 끊길 때 크게)
    /// 위로 날아가고, 남은 조각은 배출구로 들어가고, 출력기가 내려간 뒤 PartService.Confirm (저금통 → 지갑, 다음 1파트).
    /// [확인]은 처음부터 보이고, 출력 중에 누르면 바로 완료(넘어가진 않음). 출력이 끝나면 흔들리며 `확인 (5)` 카운트다운 → 0이면 자동 [확인] (M17-2, 2026-11-20).
    /// 이 오브젝트는 항상 켜 두고 자식 body만 켜고 끈다.
    /// </summary>
    public sealed class SettlementPopup : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GameObject body;
        [SerializeField, Tooltip("배경 어둡게 + 출력 중 누르면 건너뛰기")] Button dim;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;

        [Header("출력기")]
        [SerializeField, Tooltip("올라오기 · 떨림 대상")] RectTransform printer;
        [SerializeField] Image led;
        [SerializeField, Tooltip("LED 뒤 번짐 (알파 · 크기로 깜빡임)")] Image ledGlow;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Color printingColor = new(1f, 0.72f, 0.15f);
        [SerializeField] Color readyColor = new(0.25f, 0.9f, 0.45f);

        [Header("영수증")]
        [SerializeField, Tooltip("배출구 위로 나온 만큼 보이는 마스크 안, 위 끝 기준(pivot y 1) · 아래 가운데 고정")] RectTransform paper;
        [SerializeField] CanvasGroup paperGroup;
        [SerializeField, Tooltip("머리의 `DAY N`")] TMP_Text titleLabel;
        [SerializeField, Tooltip("출력 순서대로 (머리 · 수익 · 시급 · 출고 · 반품 · 순수익(도장) · 알바 안내 · 뜯기 안내). 줄마다 CanvasGroup")] CanvasGroup[] rows;
        [SerializeField, Tooltip("상차 수익 (= 저금통)")] TMP_Text outboundValue;
        [SerializeField, Tooltip("알바 시급")] TMP_Text wageValue;
        [SerializeField, Tooltip("출고 트럭 · 상자")] TMP_Text shippedValue;
        [SerializeField, Tooltip("반품 처리")] TMP_Text returnsValue;
        [SerializeField, Tooltip("남은 시간 보너스 (M17-3, 반품 처리 다음 줄)")] TMP_Text bonusValue;
        [SerializeField, Tooltip("순수익")] TMP_Text totalValue;
        [SerializeField, Tooltip("미지급 · 퇴사 알바 명단 + 경고 (GDD 11.7)")] TMP_Text staffLabel;
        [SerializeField, Tooltip("`정산 완료` 도장 (CanvasGroup)")] RectTransform stamp;
        [SerializeField] Button confirmButton;
        [SerializeField] Color incomeColor = new(0.02f, 0.59f, 0.41f);
        [SerializeField] Color expenseColor = new(0.88f, 0.11f, 0.28f);
        [SerializeField, Min(0.05f)] float feedDuration = 0.34f;
        [SerializeField, Min(0.1f)] float rowInterval = 0.58f;

        [Header("[확인] 버튼 (M17-2)")]
        [SerializeField, Min(1f), Tooltip("출력이 끝난 뒤 이 시간(초) 안에 누르지 않으면 자동으로 넘어감 `임시`")] float autoConfirmSeconds = 5f;
        [SerializeField, Tooltip("흔들림 간격 (초)")] float shakeInterval = 1f;
        [SerializeField, Tooltip("흔들림 각도 (도)")] float shakeDegrees = 12f;

        [Header("말림")]
        [SerializeField, Tooltip("영수증 루트의 말림 변형기")] ReceiptCurl curl;
        [SerializeField, Range(0f, 260f)] float curlMaxDegrees = 190f;
        [SerializeField, Tooltip("이만큼 나오면 최대 각도 (UI 유닛)")] float curlRampDistance = 560f;
        [SerializeField, Tooltip("줄이 나올 때 펄럭임 (도)")] float feedFlutterDegrees = 18f;
        [SerializeField, Tooltip("뜯을 때 더 말리는 각도")] float tearCurlDegrees = 20f;

        [Header("절취선 뜯기 (2026-11-23)")]
        [SerializeField, Tooltip("영수증 루트의 뜯기 변형기 (비우면 실행 때 붙임)")] ReceiptTear tear;
        [SerializeField, Tooltip("출력이 끝난 뒤 더 밀어내는 길이 — 절취선이 글씨 아래 여백에 오게 `임시`")] float stubMargin = 40f;
        [SerializeField, Tooltip("뜯기 단계마다 화면 흔들림 세기 `임시`")] float stepShake = 5f;
        [SerializeField, Tooltip("마지막으로 끊길 때 화면 흔들림 세기 `임시`")] float snapShake = 16f;
        [SerializeField, Tooltip("끊길 때 출력기가 아래로 튕기는 거리 `임시`")] float snapPrinterKick = 10f;
        [SerializeField, Tooltip("영수증 마스크 최소 폭 — 기울어진 종이가 옆으로 잘리지 않게")] float viewportMinWidth = 1600f;
        [SerializeField, Tooltip("흔들 때 가장자리가 드러나지 않게 배경을 넓히는 여백")] float shakePadding = 80f;

        [Header("영수증 폭 = 배출구 구멍")]
        [SerializeField, Tooltip("배출구 이미지 (출력기 자식 Lip). 비우면 자동으로 찾음")] Image lip;
        [SerializeField, Tooltip("배출구 스프라이트에서 검은 틈의 가로 범위 (px, Receipt_1 기준 89 ~ 447)")] Vector2 slotSpriteRange = new(89f, 447f);
        [SerializeField, Min(0f), Tooltip("영수증 ↔ 틈 양쪽 가장자리 여백 `임시`")] float paperSlotGap = 14f;

        [Header("세로 화면 (임시)")]
        [SerializeField, Min(0f), Tooltip("세로: 출력기 + 영수증 묶음 ↔ 화면 가장자리 최소 여백")] float portraitMargin = 40f;
        [SerializeField, Range(0f, 0.3f), Tooltip("세로: 묶음 가운데를 화면 가운데보다 이만큼(화면 높이 비율) 아래로")] float portraitDrop = 0.02f;

        Sequence sequence;
        Tween screenShake;
        RectTransform bodyRect;
        Tween ledLoop;
        Tween shakeLoop;
        Tween flutterTween;
        Tween buttonShake;
        TMP_Text confirmLabel;
        string confirmText;
        float autoRemaining = -1f; // 0 이상이면 자동 넘김 카운트다운 중
        float paperOut; // 배출구 위로 나온 길이
        float flutter;
        float tearCurl;
        Vector2 printerHome;
        Vector2 paperHome;
        bool open;
        bool printing;

        public bool IsOpen => open;

        void Awake()
        {
            confirmButton.onClick.AddListener(Confirm);
            confirmLabel = confirmButton.GetComponentInChildren<TMP_Text>(true);
            confirmText = confirmLabel != null ? confirmLabel.text : "";
            // 어디를 눌러도 [확인] (출력 중이면 건너뛰기): 배경 · 종이 그래픽 · 그 밖은 OnPointerClick
            dim.onClick.AddListener(Confirm);
            foreach (var graphic in paper.GetComponentsInChildren<ReceiptPaperGraphic>(true)) graphic.Clicked += Confirm;
            FitPaperToSlot();
            printerHome = printer.anchoredPosition;
            paperHome = paper.anchoredPosition;

            if (tear == null && !paper.TryGetComponent(out tear)) tear = paper.gameObject.AddComponent<ReceiptTear>();
            if (paper.parent is RectTransform viewport && viewport.sizeDelta.x < viewportMinWidth)
                viewport.sizeDelta = new Vector2(viewportMinWidth, viewport.sizeDelta.y);
            bodyRect = (RectTransform)body.transform;
            viewportRect = (RectTransform)paper.parent;
            landscape = (printer.anchoredPosition, printer.localScale, viewportRect.anchoredPosition, viewportRect.localScale);
            dim.image.rectTransform.offsetMin = -Vector2.one * shakePadding;
            dim.image.rectTransform.offsetMax = Vector2.one * shakePadding;
            body.SetActive(false);
        }

        /// <summary>처리기가 없는 그래픽(출력기 · 글씨 등)을 누르면 여기로 올라온다.</summary>
        public void OnPointerClick(PointerEventData eventData) => Confirm();

        /// <summary>
        /// 영수증을 배출구 구멍(배출구 스프라이트의 검은 틈)보다 양쪽 paperSlotGap만큼 좁게, 구멍 가운데에 맞춘다 (2026-11-26).
        /// 구멍 = 스프라이트 px 범위(slotSpriteRange)를 9-slice(왼 · 오른 테두리 고정, 가운데 늘어남)로 화면 폭에 옮긴 것. 영수증 안 줄들은 좌우로 늘어난다.
        /// </summary>
        void FitPaperToSlot()
        {
            if (lip == null && printer.Find("Lip") is { } found) lip = found.GetComponent<Image>();
            if (lip == null || lip.sprite == null || slotSpriteRange.y <= slotSpriteRange.x) return;
            var left = SpriteToLipX(slotSpriteRange.x);
            var right = SpriteToLipX(slotSpriteRange.y);
            var viewport = (RectTransform)paper.parent;
            var a = viewport.InverseTransformPoint(lip.rectTransform.TransformPoint(new Vector3(left, 0f, 0f)));
            var b = viewport.InverseTransformPoint(lip.rectTransform.TransformPoint(new Vector3(right, 0f, 0f)));
            paper.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) - paperSlotGap * 2f, paper.sizeDelta.y);
            // 마스크 피벗 = 아래 가운데, 영수증 앵커 = 아래 가운데 → 가운데 기준 x
            paper.anchoredPosition = new Vector2((a.x + b.x) * 0.5f - (0.5f - viewport.pivot.x) * viewport.rect.width, paper.anchoredPosition.y);
        }

        /// <summary>배출구 스프라이트 px x → 배출구 RectTransform 로컬 x (Sliced: 테두리는 그대로, 가운데만 늘어남).</summary>
        float SpriteToLipX(float px)
        {
            var sprite = lip.sprite;
            var ppu = lip.pixelsPerUnit; // 스프라이트 PPU ÷ 캔버스 기준 PPU × 배율
            var width = sprite.rect.width;
            var (borderL, borderR) = (sprite.border.x, sprite.border.z);
            var rect = lip.rectTransform.rect;
            if (lip.type != Image.Type.Sliced) return rect.xMin + px / width * rect.width;
            if (px <= borderL) return rect.xMin + px / ppu;
            if (px >= width - borderR) return rect.xMax - (width - px) / ppu;
            var middle = rect.width - (borderL + borderR) / ppu;
            return rect.xMin + borderL / ppu + (px - borderL) / (width - borderL - borderR) * middle;
        }

        RectTransform viewportRect;
        (Vector2 printerPos, Vector3 printerScale, Vector2 viewportPos, Vector3 viewportScale) landscape; // 씬 배치 = 가로
        bool? layoutPortrait; // 지금 적용된 배치 (null = 아직)

        /// <summary>
        /// 화면 방향 플래그(ScreenLayout.Portrait)에 맞춰 출력기 · 영수증 묶음 배치 (2026-11-26). 가로 = 씬 값 그대로.
        /// 세로 = 묶음(출력기 바닥 ~ 다 나온 영수증 위 끝)을 화면 폭 · 높이에 여백(portraitMargin)만 남기고 최대로 키우고,
        /// 묶음 가운데를 화면 가운데보다 portraitDrop만큼 아래에 둔다. 출력기 · 영수증 마스크는 둘 다 아래 가운데 기준이라 같은 배율 · 같은 원점으로 옮긴다.
        /// </summary>
        void ApplyLayout()
        {
            var portrait = ScreenLayout.Portrait;
            layoutPortrait = portrait;
            var (printerPos, printerScale, viewportPos, viewportScale) = landscape;
            if (!portrait)
            {
                (printer.localScale, viewportRect.anchoredPosition, viewportRect.localScale) = (printerScale, viewportPos, viewportScale);
                printerHome = printerPos;
                return;
            }

            var screen = bodyRect.rect.size;
            // 묶음 크기 (가로 배치 기준): 폭 = 출력기 폭, 높이 = 출력기 바닥 ↔ 영수증 마스크 바닥 + 영수증 길이
            var width = printer.rect.width * printerScale.x;
            var top = viewportPos.y - printerPos.y + (paper.rect.height + stubMargin) * viewportScale.y;
            var height = Mathf.Max(top, printer.rect.height * printerScale.y);
            var k = Mathf.Min((screen.x - portraitMargin * 2f) / width, (screen.y - portraitMargin * 2f) / height);
            var bottom = Mathf.Clamp(screen.y * (0.5f - portraitDrop) - height * k * 0.5f, portraitMargin, screen.y - portraitMargin - height * k);

            printer.localScale = printerScale * k;
            printerHome = new Vector2(printerPos.x * k, bottom);
            viewportRect.localScale = viewportScale * k;
            viewportRect.anchoredPosition = new Vector2(viewportPos.x * k, bottom + (viewportPos.y - printerPos.y) * k);
        }

        /// <summary>출력기가 화면 아래로 완전히 숨는 자리 (올라오기 전 · 내려간 뒤).</summary>
        Vector2 PrinterHidden => new(printerHome.x, -printer.rect.height * printer.localScale.y - 60f);

        void LateUpdate()
        {
            // 열린 채 화면이 돌면 다시 배치 (LateUpdate = CanvasScaler가 새 크기를 잡은 뒤)
            if (!open || layoutPortrait == ScreenLayout.Portrait) return;
            ApplyLayout();
            if (!printing) printer.anchoredPosition = printerHome;
        }

        void Update()
        {
            TickAutoConfirm();
            if (curl == null) return;
            var ramp = curlRampDistance > 0f ? Mathf.Clamp01(paperOut / curlRampDistance) : 1f;
            ramp = ramp * ramp * (3f - 2f * ramp);
            curl.degrees = Mathf.Clamp((curlMaxDegrees + flutter) * ramp + tearCurl, 0f, 300f);
        }

        void Start()
        {
            if (PartService.Instance != null) PartService.Instance.OnSettlementReady.Subscribe(Open).AddTo(this);
        }

        void Open(SettlementResult result)
        {
            if (open) return;
            open = true;
            printing = true;
            GameManager.Instance.OpenPopup();
            body.SetActive(true);

            titleLabel.text = result.Tutorial ? "튜토리얼" : $"DAY {PartService.Instance.Day}";
            shippedValue.text = $"{result.Trucks}대 · {result.Boxes}개";
            returnsValue.text = $"{result.Returns}개";
            outboundValue.text = $"+{result.OutboundIncome:N0}";
            outboundValue.color = incomeColor;
            wageValue.text = $"−{result.Wages:N0}";
            wageValue.color = expenseColor;
            if (bonusValue != null)
            {
                bonusValue.text = $"+{result.TimeBonus:N0}";
                bonusValue.color = incomeColor;
            }
            totalValue.text = $"{(result.Total >= 0 ? "+" : "−")}{Mathf.Abs(result.Total):N0}";
            totalValue.color = result.Total >= 0 ? incomeColor : expenseColor;
            staffLabel.text = StaffText(result.WagePlan);

            sequence?.Kill();
            screenShake?.Kill();
            bodyRect.anchoredPosition = Vector2.zero;
            tear.ResetTear();
            tearCurl = 0f;
            foreach (var row in rows) row.alpha = 0f;
            Fade(dim.image, 0f);
            paperGroup.alpha = 1f;
            paper.localRotation = Quaternion.identity;
            SetEmerged(0f);
            stamp.gameObject.SetActive(false);
            ResetConfirmButton();
            confirmButton.gameObject.SetActive(true); // 처음부터 보임 — 출력 중 누르면 바로 완료 (M17-2)
            ApplyLayout();
            printer.anchoredPosition = PrinterHidden;
            SetPrinting(false);
            statusLabel.text = "";

            sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(dim.image.DOFade(dimAlpha, 0.3f))
                .Join(printer.DOAnchorPos(printerHome, 0.45f).SetEase(Ease.OutBack, 1.2f))
                .InsertCallback(0.5f, () => SetPrinting(true));

            // 줄마다: 종이가 그 줄 아래 끝까지 밀려 나오고 → 줄이 나타남 (값은 튀어나옴)
            var start = 0.55f;
            var emerged = 0f;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (!row.gameObject.activeSelf || (row.transform == staffLabel.transform && staffLabel.text.Length == 0)) continue;
                var rect = (RectTransform)row.transform;
                var from = emerged;
                emerged = -rect.anchoredPosition.y + rect.rect.height * rect.pivot.y + 18f;
                var t = start + rowInterval * i;
                sequence.InsertCallback(t, Flutter);
                sequence.Insert(t, DOVirtual.Float(from, emerged, feedDuration, SetEmerged).SetEase(Ease.OutQuint));
                sequence.Insert(t + feedDuration, row.DOFade(1f, 0.18f));
                var value = ValueIn(row);
                if (value != null)
                {
                    value.localScale = Vector3.one * 0.75f;
                    sequence.Insert(t + feedDuration, value.DOScale(1.45f, 0.22f).SetEase(Ease.OutBack));
                    sequence.Insert(t + feedDuration + 0.22f, value.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
                }
                if (row.transform == stamp.parent) sequence.InsertCallback(t + feedDuration, SlamStamp);
            }
            // 마지막 줄 뒤 여백을 조금 더 밀어내 절취선이 글씨 아래에 오게 (건너뛰기 = Complete로 끝 값까지)
            sequence.Append(DOVirtual.Float(emerged, emerged + stubMargin, 0.22f, SetEmerged).SetEase(Ease.OutQuad))
                .AppendInterval(0.2f)
                .OnComplete(FinishPrinting);
        }

        /// <summary>그 줄의 값 글자 (번 돈 · 시급 · 합계), 없으면 null.</summary>
        RectTransform ValueIn(CanvasGroup row)
        {
            foreach (var value in new[] { outboundValue, wageValue, shippedValue, returnsValue, bonusValue, totalValue })
            {
                if (value != null && value.transform.IsChildOf(row.transform)) return value.rectTransform;
            }
            return null;
        }

        void SetEmerged(float height)
        {
            paperOut = height;
            paper.anchoredPosition = paperHome + Vector2.up * height;
        }

        /// <summary>종이가 밀려 나올 때 말린 끝이 살짝 펄럭임 (사인 × 감쇠).</summary>
        void Flutter()
        {
            flutterTween?.Kill();
            flutterTween = DOVirtual.Float(0f, 1f, 0.6f, t => flutter = feedFlutterDegrees * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * (1f - t))
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnKill(() => flutter = 0f);
        }

        void SlamStamp()
        {
            stamp.gameObject.SetActive(true);
            var group = stamp.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            stamp.localScale = Vector3.one * 2.4f;
            stamp.localEulerAngles = new Vector3(0f, 0f, 18f);
            DOTween.Sequence().SetUpdate(true).SetLink(stamp.gameObject)
                .Append(stamp.DOScale(0.92f, 0.2f).SetEase(Ease.InQuad))
                .Join(group.DOFade(1f, 0.14f))
                .Join(stamp.DOLocalRotate(new Vector3(0f, 0f, 7f), 0.2f))
                .Append(stamp.DOScale(1.06f, 0.075f))
                .Append(stamp.DOScale(1f, 0.06f))
                .Join(stamp.DOLocalRotate(new Vector3(0f, 0f, 8f), 0.06f));
        }

        void FinishPrinting()
        {
            printing = false;
            SetPrinting(false);
            tear.ShowPerforation(SlotY());
            foreach (var row in rows) row.alpha = row.transform == staffLabel.transform && staffLabel.text.Length == 0 ? 0f : 1f;

            // 넘어갈 수 있다는 표시: 1초마다 좌우로 흔들림 + `확인 (5)` 카운트다운 → 0이면 자동 [확인] (M17-2)
            var t = confirmButton.transform;
            buttonShake?.Kill();
            buttonShake = DOTween.Sequence().SetUpdate(true).SetLink(confirmButton.gameObject)
                .Append(t.DOPunchRotation(new Vector3(0f, 0f, shakeDegrees), 0.45f, 8, 0.6f))
                .AppendInterval(Mathf.Max(0f, shakeInterval - 0.45f))
                .SetLoops(-1, LoopType.Restart);
            autoRemaining = autoConfirmSeconds;
            UpdateCountdown();
        }

        /// <summary>출력 중이면 영수증을 바로 다 나온 상태로 (배경 · [확인] 탭, 넘어가지는 않음).</summary>
        void SkipPrinting()
        {
            if (printing) sequence.Complete(true);
        }

        void TickAutoConfirm()
        {
            if (autoRemaining < 0f || !open || printing) return;
            autoRemaining -= Time.unscaledDeltaTime; // 팝업 중엔 게임 시간이 멈춰 있다
            if (autoRemaining <= 0f) Confirm();
            else UpdateCountdown();
        }

        void UpdateCountdown()
        {
            if (confirmLabel != null) confirmLabel.text = $"{confirmText} ({Mathf.CeilToInt(autoRemaining)})";
        }

        /// <summary>흔들림 · 카운트다운을 멈추고 버튼 모양 · 글자를 원래대로.</summary>
        void ResetConfirmButton()
        {
            buttonShake?.Kill();
            buttonShake = null;
            autoRemaining = -1f;
            confirmButton.transform.localRotation = Quaternion.identity;
            confirmButton.transform.localScale = Vector3.one;
            if (confirmLabel != null) confirmLabel.text = confirmText;
        }

        /// <summary>출력 중: 출력기 떨림 + LED 주황, 번짐이 퍼져 나가며 사라짐 반복 (UI 샘플 ping). 끝: LED 초록, 번짐은 일정하게 유지.</summary>
        void SetPrinting(bool on)
        {
            ledLoop?.Kill();
            shakeLoop?.Kill();
            statusLabel.text = on ? "PRINTING" : "DONE";
            var color = on ? printingColor : readyColor;
            led.color = Color.Lerp(Color.white, color, 0.85f);
            if (on)
            {
                ledGlow.color = new Color(color.r, color.g, color.b, 0.85f);
                ledGlow.rectTransform.localScale = Vector3.one;
                ledLoop = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                    .Append(ledGlow.rectTransform.DOScale(2.2f, 0.7f).From(1f).SetEase(Ease.OutCubic))
                    .Join(ledGlow.DOFade(0f, 0.7f).From(0.85f).SetEase(Ease.OutCubic))
                    .SetLoops(-1, LoopType.Restart);
            }
            else
            {
                ledGlow.color = new Color(color.r, color.g, color.b, 0.35f);
                ledGlow.rectTransform.localScale = Vector3.one * 1.6f;
            }
            printer.anchoredPosition = printerHome;
            if (on) shakeLoop = printer.DOShakeAnchorPos(1f, 1.6f, 25, 90f, false, false).SetLoops(-1).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>`미지급: 알바 3, 알바 5 — 다음 정산 때 못 받으면 떠납니다` / `퇴사: 알바 2`. 둘 다 없으면 빈 글자.</summary>
        static string StaffText(WagePlan plan)
        {
            var lines = new List<string>();
            if (plan.Unpaid.Count > 0)
            {
                var names = new List<string>();
                foreach (var (worker, _) in plan.Unpaid) names.Add(worker.DisplayName);
                lines.Add($"미지급: {string.Join(", ", names)} — 다음 정산 때 못 받으면 떠납니다");
            }
            if (plan.Quitting.Count > 0)
            {
                var names = new List<string>();
                foreach (var worker in plan.Quitting) names.Add(worker.DisplayName);
                lines.Add($"퇴사: {string.Join(", ", names)}");
            }
            return string.Join("\n", lines);
        }

        static void Fade(Graphic graphic, float alpha) => graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, alpha);

        /// <summary>배출구 틈(영수증 마스크 아래 끝)의 높이, 종이 로컬 y.</summary>
        float SlotY()
        {
            var viewport = (RectTransform)paper.parent;
            var world = viewport.TransformPoint(new Vector3(0f, viewport.rect.yMin, 0f));
            return paper.InverseTransformPoint(world).y;
        }

        /// <summary>화면(팝업 Body 전체) 흔들림. 앞의 흔들림은 끊고 새로.</summary>
        void ShakeScreen(float strength, float duration)
        {
            screenShake?.Kill();
            bodyRect.anchoredPosition = Vector2.zero;
            screenShake = bodyRect.DOShakeAnchorPos(duration, strength, 30, 90f, false, true)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnKill(() => bodyRect.anchoredPosition = Vector2.zero);
        }

        void OnTearStep(int step) => ShakeScreen(stepShake, 0.1f);

        void OnTearSnap()
        {
            ShakeScreen(snapShake, 0.28f);
            printer.DOPunchAnchorPos(Vector2.down * snapPrinterKick, 0.25f, 8, 0.6f).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>[확인]: 영수증이 절취선을 따라 뜯겨 위로 날아가고, 출력기가 내려가고, 배경이 밝아진 뒤 다음 파트.</summary>
        void Confirm()
        {
            if (!open) return;
            if (printing)
            {
                SkipPrinting(); // 출력 중 [확인] = 바로 완료 (M17-2)
                return;
            }
            open = false;
            ResetConfirmButton();
            confirmButton.gameObject.SetActive(false);
            ledLoop?.Kill();
            shakeLoop?.Kill();

            flutterTween?.Kill();
            sequence?.Kill();
            sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Insert(0f, DOVirtual.Float(0f, tearCurlDegrees, 0.6f, v => tearCurl = v).SetEase(Ease.OutQuad))
                .Insert(0f, tear.CreateSequence(SlotY(), OnTearStep, OnTearSnap))
                .Append(printer.DOAnchorPos(PrinterHidden, 0.35f).SetEase(Ease.InBack))
                .Join(dim.image.DOFade(0f, 0.35f))
                .OnComplete(() =>
                {
                    body.SetActive(false);
                    GameManager.Instance.ClosePopup();
                    PartService.Instance.Confirm(); // 게임이 다시 흐를 때 HUD 카운트가 보이도록 닫은 뒤에
                });
        }
    }
}
