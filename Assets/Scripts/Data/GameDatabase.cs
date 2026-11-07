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

        public IReadOnlyList<CompanyDefinition> Companies => companies;
        public PlayerConfig Player => player;
        public ControlsConfig Controls => controls;
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
                : companies.Count == 0 || companies.Contains(null) ? nameof(companies)
                : null;
            return error == null;
        }
    }
}
