using APC.Economy;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 파트 교대 배너 (2026-11-20): 1파트(하차)가 끝나 정산 없이 2파트(상차)로 넘어갈 때 화면 위쪽에
    /// `파트 교대 / 파트1(하차) → 파트2(상차)`를 잠깐 띄운다 (PartService.OnShift). 게임을 멈추지 않고 입력도 막지 않는다.
    /// 글자는 씬에 고정. 이 오브젝트는 항상 켜 두고 자식 banner만 켜고 끈다.
    /// </summary>
    public sealed class PartShiftBanner : MonoBehaviour
    {
        [SerializeField] RectTransform banner;
        [SerializeField] CanvasGroup group;
        [SerializeField, Min(0.1f)] float holdSeconds = 1.8f;

        Sequence sequence;

        void Awake() => banner.gameObject.SetActive(false);

        void Start()
        {
            if (PartService.Instance != null) PartService.Instance.OnShift.Subscribe(_ => Show()).AddTo(this);
        }

        void Show()
        {
            sequence?.Kill();
            banner.gameObject.SetActive(true);
            group.alpha = 0f;
            banner.localScale = new Vector3(1.25f, 0.6f, 1f);
            sequence = DOTween.Sequence()
                .Append(group.DOFade(1f, 0.2f))
                .Join(banner.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
                .AppendInterval(holdSeconds)
                .Append(group.DOFade(0f, 0.35f))
                .Join(banner.DOScale(0.9f, 0.35f))
                .OnComplete(() => banner.gameObject.SetActive(false))
                .SetLink(gameObject);
        }
    }
}
