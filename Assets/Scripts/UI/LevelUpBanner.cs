using System.Collections.Generic;
using APC.Core;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// 레벨업 연출 v1 (GDD 10.2, 체크리스트 M5-6): 화면 중앙에 `LEVEL UP! Lv.N` + 해금 내용 한 줄이 잠깐 떴다 사라지고,
    /// 플레이어 위로 축하 파티클이 터진다. 게임은 멈추지 않고 입력도 막지 않는다. 여러 레벨이 한꺼번에 오르면 차례로 보여준다.
    /// 정식 팝업은 M10.
    /// </summary>
    public sealed class LevelUpBanner : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform panel;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text noteLabel;
        [SerializeField, Tooltip("플레이어 위 축하 파티클 (없어도 됨)")] ParticleSystem burst;
        [SerializeField, Min(0.1f)] float holdDuration = 2f;

        readonly Queue<int> pending = new();
        Sequence playing;

        void Start()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            panel.gameObject.SetActive(false);
            GameManager.Instance.Level.LeveledUp.Subscribe(Enqueue).AddTo(this);
        }

        void Enqueue(int level)
        {
            pending.Enqueue(level);
            if (playing == null) PlayNext();
        }

        void PlayNext()
        {
            if (pending.Count == 0)
            {
                playing = null;
                panel.gameObject.SetActive(false);
                return;
            }

            var level = pending.Dequeue();
            titleLabel.text = $"LEVEL UP!  Lv.{level}";
            var note = GameManager.Instance.Database.Levels.GetUnlockNote(level);
            foreach (var company in GameManager.Instance.Database.Companies)
            {
                if (company.UnlockLevel == level) note += $"{(string.IsNullOrEmpty(note) ? "" : "\n")}새 트럭: {company.DisplayName}"; // GDD 6.3
            }
            noteLabel.text = note;
            noteLabel.gameObject.SetActive(!string.IsNullOrEmpty(note));
            if (burst != null) burst.Play();

            panel.gameObject.SetActive(true);
            panel.localScale = Vector3.one * 0.6f;
            group.alpha = 0f;
            playing = DOTween.Sequence()
                .Append(group.DOFade(1f, 0.15f))
                .Join(panel.DOScale(1f, 0.35f).SetEase(Ease.OutBack, 2.5f))
                .AppendInterval(holdDuration)
                .Append(group.DOFade(0f, 0.3f))
                .SetLink(gameObject)
                .OnComplete(PlayNext);
        }
    }
}
