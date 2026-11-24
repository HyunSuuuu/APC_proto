using DG.Tweening;
using UnityEngine;

namespace APC.BrokeCash
{
    /// <summary>
    /// DOTween 버전 저금통 몸체 (BrokeCashScene). 이 오브젝트 = 사슬 고정점(화면 위 밖), 자식 이미지 한 장을 통째로 움직인다.
    /// 내려옴 = DOMoveY(OutBack) · 출렁 = DOLocalRotate 진자(감쇠하는 왕복 Sequence) · 쩌적/깨짐 = 이미지 DOPunchPosition.
    /// </summary>
    public sealed class PiggyBodyTween : PiggyBody
    {
        [SerializeField, Tooltip("통째로 흔들 이미지 (자식)")] Transform body;
        [SerializeField, Min(0f), Tooltip("내려오기 시작 높이 (유닛, 제자리 위로)")] float lowerFrom = 8f;
        [SerializeField, Tooltip("멈출 때 OutBack 넘침")] float lowerOvershoot = 1.6f;
        [SerializeField] Vector3 crackPunch = new(0.06f, 0.03f, 0f);
        [SerializeField] Vector3 breakPunch = new(0.11f, -0.05f, 0f);

        Vector3 restPosition;
        Vector3 bodyRest;
        bool cached;

        void Cache()
        {
            if (cached) return;
            cached = true;
            restPosition = transform.position;
            bodyRest = body.localPosition;
        }

        protected override void OnPrepare(BrokeCashPlan plan)
        {
            Cache();
            transform.position = restPosition + Vector3.up * lowerFrom;
            transform.localRotation = Quaternion.identity;
            body.localPosition = bodyRest;
        }

        public override Tween Lower(BrokeCashPlan plan)
        {
            var lower = Config.LowerSeconds;
            return DOTween.Sequence().SetUpdate(true).SetId(this).SetLink(gameObject)
                .Append(transform.DOMoveY(restPosition.y, lower).SetEase(Ease.OutBack, lowerOvershoot))
                .InsertCallback(lower * 0.8f, () => Sway(plan.SwayDegrees));
        }

        /// <summary>진자: 0 → +A → −0.62A → +0.38A … → 0 (Sine 왕복).</summary>
        void Sway(float degrees)
        {
            var half = Config.SwayPeriod * 0.5f;
            var seq = DOTween.Sequence().SetUpdate(true).SetId(this).SetLink(gameObject);
            var amp = degrees;
            seq.Append(transform.DOLocalRotate(new Vector3(0f, 0f, amp), half * 0.5f).SetEase(Ease.OutSine));
            for (var i = 1; i < 6; i++)
            {
                amp *= -0.62f;
                seq.Append(transform.DOLocalRotate(new Vector3(0f, 0f, amp), half).SetEase(Ease.InOutSine));
            }
            seq.Append(transform.DOLocalRotate(Vector3.zero, half * 0.5f).SetEase(Ease.InSine));
        }

        public override void Crack(BrokeCashPlan plan)
        {
            base.Crack(plan);
            body.DOKill(true);
            body.DOPunchPosition(crackPunch, 0.16f, 12, 0.6f).SetUpdate(true).SetId(this).SetLink(gameObject);
        }

        public override void Break(BrokeCashPlan plan)
        {
            base.Break(plan);
            body.DOKill(true);
            body.DOPunchPosition(breakPunch, 0.25f, 9, 0.6f).SetUpdate(true).SetId(this).SetLink(gameObject);
        }

        public override void SnapBroken()
        {
            Cache();
            base.SnapBroken();
            body.DOKill();
            transform.position = restPosition;
            transform.localRotation = Quaternion.identity;
            body.localPosition = bodyRest;
        }
    }
}
