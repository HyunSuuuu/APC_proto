using APC.Core;
using APC.Interaction;
using APC.UI;
using R3;
using UnityEngine;

namespace APC.Contracts
{
    /// <summary>
    /// 사무실 책상 (GDD 4.3, 2026-11-18 A · B). 게이지가 차면 책상 팝업을 연다. 벗어나면 게이지를 되돌린다.
    /// A책상 = `고용 | 배치 | 스탯`, B책상 = `설비 | 트럭 | 모듈` — B는 해금 레벨 전에는 모델(자식)이 숨겨지고 열리지 않으며, 그 레벨이 되면 팝 연출로 올라온다.
    /// 사무실 모듈 프리팹 안에 있으므로 팝업은 씬에서 찾는다 (ContractPopup.Instance).
    /// </summary>
    public sealed class ContractPC : GaugeZone
    {
        [SerializeField, Tooltip("켜면 B책상 (설비 | 트럭 | 모듈), 끄면 A책상 (고용 | 배치 | 스탯)"), UnityEngine.Serialization.FormerlySerializedAs("computerDesk")] bool deskB;
        [SerializeField, Min(1), Tooltip("B책상이 올라오는 레벨 (GDD 10.3)")] int unlockLevel = 3;

        public bool IsDeskB => deskB;

        bool Unlocked => !deskB || GameManager.Instance.Level.Level.CurrentValue >= unlockLevel;

        void Start()
        {
            Duration = GameManager.Instance.Database.Logistics.OfficeGaugeDuration;
            if (!deskB) return;
            var shown = Unlocked;
            SetModelShown(shown);
            GameManager.Instance.Level.Level.Subscribe(_ =>
            {
                if (shown || !Unlocked) return;
                shown = true;
                SetModelShown(true);
                FacilityPop.Play(transform);
            }).AddTo(this);
        }

        void SetModelShown(bool on)
        {
            foreach (Transform child in transform) child.gameObject.SetActive(on);
        }

        protected override bool CanProgress(Interactor who) => Unlocked;

        protected override void OnCompleted(Interactor who)
        {
            if (ContractPopup.Instance != null) ContractPopup.Instance.Open(deskB);
        }

        protected override void OnExit(Interactor who) => ResetGauge();
    }
}
