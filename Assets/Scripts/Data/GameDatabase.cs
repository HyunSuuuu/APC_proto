using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>모든 데이터 테이블의 진입점. GameManager가 이 에셋 하나만 참조한다.</summary>
    [CreateAssetMenu(menuName = "APC/Data/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        [SerializeField] List<CompanyDefinition> companies = new();
        [SerializeField] LevelTable levels;
        [SerializeField] EconomyConfig economy;
        [SerializeField] OrderRules orders;
        [SerializeField] SiteLayout site;
        [SerializeField] StaffRules staff;
        [SerializeField] PlayerConfig player;
        [SerializeField] ControlsConfig controls;
        [SerializeField] CarryConfig carry;
        [SerializeField] LogisticsConfig logistics;
        [SerializeField, Tooltip("황금 트럭 확률 · 값 · 연출. 비면 기본값")] GoldenTruckConfig goldenTruck;
        [SerializeField, Tooltip("벽 반투명 (플레이어를 가린 벽 · 주차장). 비면 안 함")] WallFadeConfig wallFade;

        public IReadOnlyList<CompanyDefinition> Companies => companies;
        public LogisticsConfig Logistics => logistics;
        public GoldenTruckConfig GoldenTruck => GoldenTruckConfig.OrDefault(goldenTruck);
        public PlayerConfig Player => player;
        public ControlsConfig Controls => controls;
        public CarryConfig Carry => carry;
        public WallFadeConfig WallFade => wallFade;

        /// <summary>품목이 속한 회사. 없으면 null.</summary>
        public CompanyDefinition GetCompanyOf(ItemDefinition item)
        {
            foreach (var company in companies)
            {
                if (company == null) continue;
                foreach (var candidate in company.Items)
                {
                    if (candidate == item) return company;
                }
            }
            return null;
        }
        public LevelTable Levels => levels;
        public EconomyConfig Economy => economy;
        public OrderRules Orders => orders;
        public SiteLayout Site => site;
        public StaffRules Staff => staff;

        /// <summary>비어 있는 참조를 찾는다. 문제가 없으면 true.</summary>
        public bool Validate(out string error)
        {
            error = levels == null ? nameof(levels)
                : economy == null ? nameof(economy)
                : orders == null ? nameof(orders)
                : site == null ? nameof(site)
                : staff == null ? nameof(staff)
                : player == null ? nameof(player)
                : controls == null ? nameof(controls)
                : carry == null ? nameof(carry)
                : logistics == null ? nameof(logistics)
                : companies.Count == 0 || companies.Contains(null) ? nameof(companies)
                : null;
            return error == null;
        }
    }
}
