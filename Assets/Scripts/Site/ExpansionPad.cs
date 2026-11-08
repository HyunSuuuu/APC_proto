using APC.Interaction;

namespace APC.Site
{
    /// <summary>확장 칸 해금 패드 (GDD 9.3). 다 내면 그 배치의 묶음 전체를 해금한다. ExpansionPads가 만들고 놓는다.</summary>
    public sealed class ExpansionPad : PurchasePad
    {
        /// <summary>해금할 배치 (묶음의 대표 id).</summary>
        public string PlacementId { get; private set; }

        public void Setup(string placementId, string title, int cost)
        {
            PlacementId = placementId;
            SetupPad(title, cost);
        }

        protected override void OnPurchased(Interactor who) => SiteGrid.Instance.Unlock(PlacementId);
    }
}
