using APC.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 도장(Stamp)을 찍을 수 있는 카드의 바탕 (계약 카드 · 고용 후보 카드, GDD 6.3 · 11.1).
    /// 찍히면 회색 필터 + 붉은 도장 자국이 대각선으로 찍히고, 찍을 수 없으면 좌우로 흔들리며 사유를 잠깐 띄운다.
    /// 연출은 팝업(게임 멈춤) 중에 재생하므로 SetUpdate(true).
    /// </summary>
    public abstract class StampCard : MonoBehaviour
    {
        [SerializeField, Tooltip("찍을 수 없는 이유 (잠깐 표시)")] TMP_Text reasonLabel;
        [SerializeField, Tooltip("찍힘 회색 필터")] Graphic stampFilter;
        [SerializeField, Tooltip("붉은 [ 고용 ] / [ 계약 ] 도장 자국")] RectTransform stampMark;
        [SerializeField, Tooltip("연출 값 (Assets/Data/StampConfig)")] StampConfig config;

        // 사유 글자 · 필터 · 자국은 Build Site가 꺼진 채 만든다 (Awake에서 끄면 부모 OnEnable의 Bind 뒤에 다시 꺼진다)

        Sequence flash;
        float filterAlpha = -1f;

        /// <summary>도장이 이 카드에 놓였다. 찍혔으면 true (도장이 꾹 누르는 연출을 한다).</summary>
        public abstract bool TryStamp();

        /// <summary>찍힘 상태를 연출 없이 맞춘다 (다시 그릴 때).</summary>
        protected void SetStamped(bool stamped)
        {
            if (filterAlpha < 0f) filterAlpha = stampFilter.color.a;
            stampFilter.DOKill();
            stampMark.DOKill();
            stampFilter.gameObject.SetActive(stamped);
            stampMark.gameObject.SetActive(stamped);
            stampFilter.color = WithAlpha(stampFilter.color, filterAlpha);
            stampMark.localScale = Vector3.one;
        }

        /// <summary>도장 찍기: 자국이 크게 나타났다가 쾅 내려앉고, 회색 필터가 서서히 덮인다.</summary>
        protected void PlayStamped()
        {
            var c = StampConfig.OrDefault(config);
            SetStamped(true);
            stampMark.localScale = Vector3.one * c.MarkStartScale;
            stampMark.DOScale(1f, c.MarkDropDuration).SetEase(Ease.InQuad).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => transform.DOPunchScale(Vector3.one * c.CardPunch, c.CardPunchDuration, 6, 0.5f).SetUpdate(true).SetLink(gameObject));
            stampFilter.color = WithAlpha(stampFilter.color, 0f);
            stampFilter.DOFade(filterAlpha, c.FilterFadeDuration).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>찍을 수 없음: 좌우로 흔들리고 사유를 1.2초 띄운다.</summary>
        protected void Flash(string reason)
        {
            reasonLabel.text = reason;
            reasonLabel.gameObject.SetActive(true);

            var c = StampConfig.OrDefault(config);
            flash?.Kill(true);
            var rect = (RectTransform)transform;
            flash = DOTween.Sequence()
                .Append(rect.DOShakeAnchorPos(c.ShakeDuration, new Vector2(c.ShakeStrength, 0f), 18, 0f, false, true))
                .AppendInterval(c.ReasonDuration)
                .OnComplete(() => reasonLabel.gameObject.SetActive(false))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
