using System;
using APC.Interaction;

namespace APC.Site
{
    /// <summary>
    /// 모듈 해금 발판 (GDD 9.2 · 9.3). 다 내면 그 배치의 묶음 전체를 해금한다 (+ XP, PurchasePad). SiteUnlocker가 만들고 놓는다.
    /// 시작 순서(StartSequence, GDD 9.6)도 쓴다: XP 없음, 배치 id가 없으면(건물 발판) 해금 없이 Purchased만 보낸다.
    /// </summary>
    public sealed class ExpansionPad : PurchasePad
    {
        /// <summary>해금할 배치 (묶음의 대표 id). null이면 해금하지 않는다.</summary>
        public string PlacementId { get; private set; }

        /// <summary>false면 다 내도 XP 없음 (시작 순서 발판).</summary>
        public bool RewardXp { get; set; } = true;

        /// <summary>다 냈을 때 (해금 뒤).</summary>
        public event Action Purchased;

        public void Setup(string placementId, string title, int cost)
        {
            PlacementId = placementId;
            SetupPad(title, cost);
        }

        protected override bool GivesXp => RewardXp;

        protected override void OnPurchased(Interactor who)
        {
            if (!string.IsNullOrEmpty(PlacementId)) SiteGrid.Instance.Unlock(PlacementId);
            Purchased?.Invoke();
        }
    }
}
