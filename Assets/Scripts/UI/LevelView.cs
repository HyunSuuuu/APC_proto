using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>HUD 좌상단 레벨 + 경험치 바 (GDD 12.1, 체크리스트 M5-1). XP는 누적으로 보인다 (Lv.1 `0/30` → Lv.2 `30/80`). 레벨이 오르면 튄다.</summary>
    public sealed class LevelView : MonoBehaviour
    {
        [SerializeField] TMP_Text levelLabel;
        [SerializeField, Tooltip("Filled · Horizontal Image")] Image xpFill;
        [SerializeField] TMP_Text xpLabel;
        [SerializeField] RectTransform punchTarget;
        [SerializeField, Min(0.05f)] float fillDuration = 0.3f;

        LevelProgress progress;

        void Start()
        {
            progress = GameManager.Instance.Level;
            xpFill.fillAmount = 0f;

            progress.Level
                .Subscribe(level =>
                {
                    levelLabel.text = $"Lv.{level}";
                    if (level <= 1 || punchTarget == null) return;
                    punchTarget.DOKill(true);
                    punchTarget.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f).SetLink(gameObject);
                })
                .AddTo(this);
            progress.Xp.Subscribe(_ => RefreshXp()).AddTo(this);
            progress.Level.Subscribe(_ => RefreshXp()).AddTo(this);
        }

        /// <summary>글자는 누적 XP (`15/24`), 바는 이번 레벨 안에서 찬 비율.</summary>
        void RefreshXp()
        {
            var xp = progress.Xp.CurrentValue;
            var toNext = progress.XpToNext;
            xpLabel.text = $"{progress.TotalXp}/{progress.NextLevelXp}";
            xpFill.DOKill();
            xpFill.DOFillAmount(toNext > 0 ? (float)xp / toNext : 0f, fillDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
        }
    }
}
