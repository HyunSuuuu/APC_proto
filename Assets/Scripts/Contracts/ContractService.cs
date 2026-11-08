using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Logistics;
using APC.Site;
using R3;
using UnityEngine;

namespace APC.Contracts
{
    /// <summary>계약할 수 없는 이유 (계약 카드 표시용).</summary>
    public enum ContractBlock
    {
        None,

        /// <summary>이미 계약함.</summary>
        Contracted,

        /// <summary>해금 레벨 미달.</summary>
        Level,

        /// <summary>이 회사의 선반 구역이 아직 잠겨 있음 (확장 필요).</summary>
        ShelfArea,

        /// <summary>선반 구역은 열렸지만 빈 자리가 품목 수보다 적음.</summary>
        ShelfSlots,
    }

    /// <summary>
    /// 회사 계약 (GDD 6.3). 계약하면 ① 그 회사 선반 구역에 품목마다 선반 1개가 팝 연출과 함께 생기고 ② 입고 트럭이 오기 시작하고
    /// ③ 그 회사 주문이 생기기 시작한다 (OrderService가 OnContracted를 구독). 계약 비용은 없고 레벨로만 제한한다. 씬에 하나 둔다.
    /// </summary>
    public sealed class ContractService : MonoBehaviour
    {
        public static ContractService Instance { get; private set; }

        readonly List<CompanyDefinition> contracted = new();
        readonly Subject<CompanyDefinition> contractedSubject = new();

        public IReadOnlyList<CompanyDefinition> Contracted => contracted;
        public bool HasAnyContract => contracted.Count > 0;

        /// <summary>계약이 맺어질 때마다 그 회사를 보낸다.</summary>
        public Observable<CompanyDefinition> OnContracted => contractedSubject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[ContractService] 씬에 ContractService가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            contractedSubject.Dispose();
        }

        public bool IsContracted(CompanyDefinition company) => contracted.Contains(company);

        public bool IsUnlocked(CompanyDefinition company)
            => GameManager.Instance.Level.Level.CurrentValue >= company.UnlockLevel;

        /// <summary>계약할 수 없는 이유. None이면 계약할 수 있다.</summary>
        public ContractBlock GetBlock(CompanyDefinition company)
        {
            if (IsContracted(company)) return ContractBlock.Contracted;
            if (!IsUnlocked(company)) return ContractBlock.Level;

            // 회사마다 자기 선반 구역 (GDD 6.3): 그 구역이 열려 있고, 품목 수만큼 빈 자리가 있어야 한다.
            var area = company.ShelfAreaId;
            var grid = SiteGrid.Instance;
            if (!string.IsNullOrEmpty(area) && grid != null && !grid.IsUnlocked(area)) return ContractBlock.ShelfArea;

            var needed = 0;
            foreach (var item in company.Items)
            {
                if (!Warehouse.Instance.HasShelf(item)) needed++;
            }
            return Warehouse.Instance.CountFreeSlots(area) < needed ? ContractBlock.ShelfSlots : ContractBlock.None;
        }

        public bool TryContract(CompanyDefinition company)
        {
            if (company == null || GetBlock(company) != ContractBlock.None) return false;

            contracted.Add(company);
            // 품목마다 선반 1개 (이미 있는 품목은 건너뜀)
            foreach (var item in company.Items)
            {
                if (!Warehouse.Instance.HasShelf(item)) Warehouse.Instance.BuildShelf(item, company.ShelfAreaId);
            }
            contractedSubject.OnNext(company);
            return true;
        }
    }
}
