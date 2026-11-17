using APC.Core;
using APC.Data;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>최대 적재 시 머리 위에 MAX 표시 + 바운스 (GDD 5.3, 체크리스트 M2-6).</summary>
    public sealed class CarryMaxIndicator : MonoBehaviour
    {
        [SerializeField] CarryStack stack;
        [SerializeField, Tooltip("MAX 표시 (그림 max · 글자 등). 스택 anchor의 자식으로 두지 않는다. 크기는 자식에서 정한다 (루트는 연출로 1배 기준)")] Component label;
        [SerializeField, Min(0f), Tooltip("캐릭터 발밑에서 MAX 표시까지 높이 (m) — 머리 위 (모델 키 약 1.5m)")] float headHeight = 2.2f;

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
            labelTransform.position = transform.position + Vector3.up * headHeight; // 상자 위가 아니라 머리 위 (2026-11-18)
            if (cam != null) labelTransform.rotation = cam.transform.rotation;
        }
    }
}
