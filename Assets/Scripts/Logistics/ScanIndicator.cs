using APC.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.Logistics
{
    /// <summary>
    /// 하차 스캔 표시 (GDD 7.2a): 스캔하는 사람 머리 위 원형 게이지 + 말풍선(`스캔 중`). 하차 칸 공간 둘레 존(StorageZone)마다 하나를 실행 중에 만든다.
    /// 월드 캔버스, 카메라를 향해 돌린다. 스프라이트 · 글꼴 · 글자 · 높이는 LogisticsConfig.
    /// </summary>
    public sealed class ScanIndicator : MonoBehaviour
    {
        Image fill;
        Transform target;
        Camera cam;

        public static ScanIndicator Create(Transform owner)
        {
            var config = Core.GameManager.Instance.Database.Logistics;
            var go = new GameObject("ScanIndicator", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(200f, 200f);
            rect.localScale = Vector3.one * 0.018f;
            var view = go.AddComponent<ScanIndicator>();

            // 말풍선 (게이지 위)
            var bubble = AddImage("Bubble", rect, new Vector2(0f, 62f), new Vector2(150f, 52f), Color.white, config.ScanBubbleSprite);
            bubble.type = config.ScanBubbleSprite != null && config.ScanBubbleSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            var tail = AddImage("Tail", rect, new Vector2(0f, 34f), new Vector2(16f, 16f), Color.white, null);
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.transform.SetSiblingIndex(0);
            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(bubble.transform, false);
            if (config.ScanFont != null) text.font = config.ScanFont;
            text.text = config.ScanBubbleText;
            text.fontSize = 32f;
            text.color = new Color(0.15f, 0.15f, 0.18f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;

            // 원형 게이지
            AddImage("RingBack", rect, new Vector2(0f, -20f), new Vector2(76f, 76f), new Color(0f, 0f, 0f, 0.45f), config.ScanRingSprite);
            view.fill = AddImage("RingFill", rect, new Vector2(0f, -20f), new Vector2(76f, 76f), new Color(0.35f, 0.9f, 0.45f, 1f), config.ScanRingSprite);
            view.fill.type = Image.Type.Filled;
            view.fill.fillMethod = Image.FillMethod.Radial360;
            view.fill.fillOrigin = (int)Image.Origin360.Top;
            view.fill.fillClockwise = true;
            AddImage("RingHole", rect, new Vector2(0f, -20f), new Vector2(48f, 48f), Color.white, config.ScanRingSprite);

            go.SetActive(false);
            return view;
        }

        static Image AddImage(string name, Transform parent, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>who 머리 위에 progress(0~1)로 보인다. who가 null이면 숨긴다.</summary>
        public void Show(Interactor who, float progress)
        {
            var show = who != null;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            target = show ? who.transform : null;
            if (!show) return;
            fill.fillAmount = progress;
            Place();
        }

        void LateUpdate()
        {
            if (target != null) Place();
        }

        void Place()
        {
            if (cam == null) cam = Camera.main;
            transform.position = target.position + Vector3.up * Core.GameManager.Instance.Database.Logistics.ScanIndicatorHeight;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
