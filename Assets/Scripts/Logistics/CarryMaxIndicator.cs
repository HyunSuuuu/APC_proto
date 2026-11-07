using APC.Core;
using APC.Data;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>최대 적재 시 스택 위에 MAX 표시 + 바운스 (GDD 5.3, 체크리스트 M2-6).</summary>
    public sealed class CarryMaxIndicator : MonoBehaviour
    {
        [SerializeField] CarryStack stack;
        [SerializeField, Tooltip("월드 TextMeshPro. 스택 anchor의 자식으로 두지 않는다")] TMP_Text label;
        [SerializeField, Min(0f), Tooltip("맨 위 상자와 글자 사이 간격 (m)")] float gap = 0.5f;

        CarryConfig config;
        Camera cam;
        Transform labelTransform;

        void Start()
        {
            config = GameManager.Instance.Database.Carry;
            cam = Camera.main;
            labelTransform = label.transform;
            label.gameObject.SetActive(false);

            stack.Count
                .CombineLatest(stack.Capacity, (count, capacity) => count >= capacity)
                .DistinctUntilChanged()
                .Subscribe(SetVisible)
                .AddTo(this);
        }

        void SetVisible(bool full)
        {
            labelTransform.DOKill();
            if (!full)
            {
                label.gameObject.SetActive(false);
                return;
            }

            label.gameObject.SetActive(true);
            labelTransform.localScale = Vector3.zero;
            labelTransform.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 3f).OnComplete(() =>
                labelTransform.DOScale(config.MaxPulseScale, config.MaxPulsePeriod * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo));
        }

        void LateUpdate()
        {
            if (!label.gameObject.activeSelf) return;
            labelTransform.position = stack.Anchor.position + Vector3.up * (stack.Items.Count * stack.BoxHeight + gap);
            if (cam != null) labelTransform.rotation = cam.transform.rotation;
        }
    }
}
