using DG.Tweening;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 월드에 잠깐 떠올랐다 사라지는 알림 글자 ('공간 가득' 등, GDD 7.2 · 5.3). 원본 월드 글자(폰트 · 외곽선 · Billboard)를 복제해 쓴다.
    /// </summary>
    public static class FloatingNotice
    {
        static readonly Color NoticeColor = new(1f, 0.3f, 0.25f);

        public static void Show(TMP_Text template, Vector3 position, string text)
        {
            if (template == null) return;
            var label = Object.Instantiate(template, position, template.transform.rotation);
            label.text = text;
            label.color = NoticeColor;

            var t = label.transform;
            DOTween.Sequence()
                .Append(t.DOMove(position + Vector3.up * 0.8f, 1f).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => label.alpha, a => label.alpha = a, 0f, 0.4f).SetDelay(0.6f))
                .SetLink(label.gameObject)
                .OnComplete(() => Object.Destroy(label.gameObject));
        }
    }
}
