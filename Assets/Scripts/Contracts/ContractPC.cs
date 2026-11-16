using APC.Core;
using APC.Interaction;
using APC.UI;
using R3;
using UnityEngine;

namespace APC.Contracts
{
    /// <summary>
    /// 사무실 책상 (GDD 4.3). 게이지가 차면 책상 팝업을 연다. 벗어나면 게이지를 되돌린다.
    /// 클립보드 책상 = `고용 | 배치`, 컴퓨터 책상 = `업그레이드 | 모듈` (해금 레벨 전에는 화면이 꺼지고 열리지 않음).
    /// 사무실 모듈 프리팹 안에 있으므로 팝업은 씬에서 찾는다 (ContractPopup.Instance).
    /// </summary>
    public sealed class ContractPC : GaugeZone
    {
        [SerializeField, Tooltip("켜면 컴퓨터 책상 (업그레이드 | 모듈), 끄면 클립보드 책상 (고용 | 배치)")] bool computerDesk;
        [SerializeField, Min(1), Tooltip("컴퓨터 책상이 열리는 레벨")] int unlockLevel = 2;
        [SerializeField, Tooltip("컴퓨터 책상 화면 (해금 전에는 꺼짐, 없어도 됨)")] GameObject screen;

        bool Unlocked => !computerDesk || GameManager.Instance.Level.Level.CurrentValue >= unlockLevel;

        void Start()
        {
            Duration = GameManager.Instance.Database.Logistics.OfficeGaugeDuration;
            if (screen != null) GameManager.Instance.Level.Level.Subscribe(_ => screen.SetActive(Unlocked)).AddTo(this);
        }

        protected override bool CanProgress(Interactor who) => Unlocked;

        protected override void OnCompleted(Interactor who)
        {
            if (ContractPopup.Instance != null) ContractPopup.Instance.Open(computerDesk);
        }

        protected override void OnExit(Interactor who) => ResetGauge();
    }
}
