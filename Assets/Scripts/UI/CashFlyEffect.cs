using System;
using System.Collections.Generic;
using APC.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 주운 현금이 HUD 저금통 아이콘으로 날아가는 연출 (GDD 7.6 · 12.1). 달러 아이콘(`Art/Temp/Dollor`)이 플레이어 위치에서
    /// 포물선(2차 베지어)을 그리며 저금통 아이콘으로 날아가고, 도착하면 콜백(저금통 += 금액)과 아이콘 튐.
    /// DOTween으로 움직인다 (파티클은 움직이는 UI 목표 · 도착 콜백을 다루기 어려움). 값은 CashFlyConfig (속도 · 크기 그래프 포함).
    /// HUD Canvas(Screen Space Overlay)에 둔다.
    /// </summary>
    public sealed class CashFlyEffect : MonoBehaviour
    {
        [SerializeField, Tooltip("달러 아이콘 원본 (꺼진 채, 풀에서 복제)")] Image iconTemplate;
        [SerializeField, Tooltip("날아갈 곳 = HUD 저금통 아이콘")] RectTransform target;
        [SerializeField, Tooltip("연출 값 (Assets/Data/CashFlyConfig) — 시간 · 속도 그래프 · 포물선 · 크기 그래프")] CashFlyConfig config;

        readonly Stack<Image> pool = new();
        Canvas canvas;
        Camera worldCamera;

        public static CashFlyEffect Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
            iconTemplate.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>월드 위치에서 저금통 아이콘으로 날린다. 도착하면 onArrive.</summary>
        public void Fly(Vector3 worldPosition, Action onArrive)
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null || target == null)
            {
                onArrive?.Invoke();
                return;
            }

            var c = CashFlyConfig.OrDefault(config);
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 start = worldCamera.WorldToScreenPoint(worldPosition);
            Vector2 end = target.position; // Screen Space Overlay: 월드 = 화면 좌표
            var control = (start + end) * 0.5f + Vector2.up * (c.ArcHeight * scale);

            var icon = Get();
            var rect = icon.rectTransform;
            rect.sizeDelta = Vector2.one * c.Size;
            rect.position = start;
            rect.localScale = Vector3.one * c.EvaluateScale(0f);
            // 시간은 일정하게 흐르고(Linear), 속도 · 크기는 그래프로 정한다
            DOVirtual.Float(0f, 1f, c.Duration, time =>
                {
                    var t = c.EvaluateMove(time);
                    var a = Vector2.LerpUnclamped(start, control, t);
                    var b = Vector2.LerpUnclamped(control, end, t);
                    rect.position = Vector2.LerpUnclamped(a, b, t);
                    rect.localScale = Vector3.one * c.EvaluateScale(time);
                })
                .SetEase(Ease.Linear)
                .SetLink(icon.gameObject)
                .OnComplete(() =>
                {
                    Release(icon);
                    onArrive?.Invoke();
                    if (c.TargetPunch <= 0f) return;
                    target.DOKill(true);
                    target.DOPunchScale(Vector3.one * c.TargetPunch, c.TargetPunchDuration, 6, 0.5f).SetLink(target.gameObject);
                });
        }

        Image Get()
        {
            var icon = pool.Count > 0 ? pool.Pop() : Instantiate(iconTemplate, iconTemplate.transform.parent);
            icon.gameObject.SetActive(true);
            icon.transform.SetAsLastSibling();
            return icon;
        }

        void Release(Image icon)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(false);
            pool.Push(icon);
        }
    }
}
