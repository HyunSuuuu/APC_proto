using DG.Tweening;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 월드에 잠깐 떠올랐다 사라지는 알림 글자 ('공간 가득' 등, GDD 7.2 · 5.3). 원본 월드 글자(폰트 · 외곽선 · Billboard)를 복제해 쓰고,
    /// 재질은 CarryConfig.NoticeMaterial(TMP Overlay)로 바꿔 건물에 가리지 않게 한다 (2026-11-20).
    /// </summary>
    public static class FloatingNotice
    {
        static readonly Color NoticeColor = new(1f, 0.3f, 0.25f);

        public static void Show(TMP_Text template, Vector3 position, string text)
        {
            if (template == null) return;
            var label = Object.Instantiate(template, position, template.transform.rotation);
            // 건물 · 지붕에 가리지 않게 Overlay 재질로 (아틀라스가 다르면 글자가 깨지니 같은 폰트일 때만)
            var overlay = Core.GameManager.Instance != null ? Core.GameManager.Instance.Database.Carry.NoticeMaterial : null;
            if (overlay != null && overlay.mainTexture == label.fontSharedMaterial.mainTexture) label.fontSharedMaterial = overlay;
            label.text = text;
            label.color = NoticeColor;

            var t = label.transform;
            DOTween.Sequence()
                .Append(t.DOMove(position + Vector3.up * 0.8f, 1f).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => label.alpha, a => label.alpha = a, 0f, 0.4f).SetDelay(0.6f))
                .SetLink(label.gameObject)
                .OnComplete(() => Object.Destroy(label.gameObject));
        }

        /// <summary>글자 대신 그림 (예: '넣을 수 없음' = candoit). 카메라를 보고 떠올랐다 사라진다.</summary>
        public static void ShowIcon(Sprite sprite, Vector3 position, float height)
        {
            if (sprite == null) return;
            var go = new GameObject($"Notice_{sprite.name}");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            var t = go.transform;
            t.position = position;
            t.localScale = Vector3.one * (height / sprite.bounds.size.y);
            if (Camera.main != null) t.rotation = Camera.main.transform.rotation;

            DOTween.Sequence()
                .Append(t.DOMove(position + Vector3.up * 0.8f, 1f).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => renderer.color.a, a => renderer.color = new Color(1f, 1f, 1f, a), 0f, 0.4f).SetDelay(0.6f))
                .SetLink(go)
                .OnComplete(() => Object.Destroy(go));
        }
    }
}
