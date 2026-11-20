using System.Collections.Generic;
using APC.Core;
using APC.Economy;
using APC.Staff;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 하루 정산 = 영수증 출력기 (GDD 9.5, 12.3, UI 샘플 BillScene 방식 2026-11-20). 2파트가 끝나면 열리고 게임이 멈춘다.
    /// 배경이 어두워지고 출력기가 아래에서 올라온 뒤, 영수증이 줄마다 한 칸씩 밀려 올라오며(출력기 떨림 · LED 깜빡임)
    /// 머리(`[일일 물류 정산서]` · DAY N) → 상차 수익 · 알바 시급 · 출고(트럭 · 상자) · 반품 처리 → 순수익 + `정산 완료` 도장 쾅. 출력 중 화면을 누르면 끝까지 건너뛴다.
    /// 출력기 = 배출구 · 몸체를 틈 없이 쌓고, 영수증은 그 위 마스크 안에서 배출구 틈부터 올라온다 (2026-11-20).
    /// 영수증 윗부분은 나온 길이만큼 화면 안쪽(뒤)으로 말려 넘어간다 (ReceiptCurl, UI 샘플과 같음). 줄이 나올 때마다 살짝 펄럭이고, 뜯을 때 더 말린다.
    /// [확인] → 영수증이 위로 뜯겨 날아가고 출력기가 내려간 뒤 PartService.Confirm (저금통 → 지갑, 다음 1파트).
    /// [확인]은 처음부터 보이고, 출력 중에 누르면 바로 완료(넘어가진 않음). 출력이 끝나면 흔들리며 `확인 (5)` 카운트다운 → 0이면 자동 [확인] (M17-2, 2026-11-20).
    /// 이 오브젝트는 항상 켜 두고 자식 body만 켜고 끈다.
    /// </summary>
    public sealed class SettlementPopup : MonoBehaviour
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

        Sequence sequence;
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
            dim.onClick.AddListener(SkipPrinting);
            printerHome = printer.anchoredPosition;
            paperHome = paper.anchoredPosition;
            body.SetActive(false);
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
            tearCurl = 0f;
            foreach (var row in rows) row.alpha = 0f;
            Fade(dim.image, 0f);
            paperGroup.alpha = 1f;
            paper.localRotation = Quaternion.identity;
            SetEmerged(0f);
            stamp.gameObject.SetActive(false);
            ResetConfirmButton();
            confirmButton.gameObject.SetActive(true); // 처음부터 보임 — 출력 중 누르면 바로 완료 (M17-2)
            printer.anchoredPosition = printerHome + Vector2.down * 700f;
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
            sequence.AppendInterval(0.35f).OnComplete(FinishPrinting);
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

        /// <summary>[확인]: 영수증이 위로 뜯겨 날아가고, 출력기가 내려가고, 배경이 밝아진 뒤 다음 파트.</summary>
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

            var top = paper.anchoredPosition;
            flutterTween?.Kill();
            sequence?.Kill();
            sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Insert(0f, DOVirtual.Float(0f, tearCurlDegrees, 0.6f, v => tearCurl = v).SetEase(Ease.OutQuad))
                .Append(paper.DOAnchorPos(top + Vector2.up * 20f, 0.15f).SetEase(Ease.InSine))
                .Join(paper.DOLocalRotate(new Vector3(0f, 0f, 2.2f), 0.15f))
                .Append(paper.DOAnchorPos(top + Vector2.up * 1100f, 0.45f).SetEase(Ease.InCubic))
                .Join(paper.DOLocalRotate(new Vector3(0f, 0f, -10f), 0.45f))
                .Join(paperGroup.DOFade(0f, 0.45f))
                .Append(printer.DOAnchorPos(printerHome + Vector2.down * 700f, 0.35f).SetEase(Ease.InBack))
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
