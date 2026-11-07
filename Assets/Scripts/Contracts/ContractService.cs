using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Logistics;
using R3;
using UnityEngine;

namespace APC.Contracts
{
    /// <summary>
    /// 회사 계약 (GDD 6.3). 계약하면 ① 그 회사 선반이 팝 연출과 함께 생기고 ② 입고 트럭이 오기 시작하고
    /// ③ (M4) 그 회사 주문이 생기기 시작한다. 계약 비용은 없고 레벨로만 제한한다. 씬에 하나 둔다.
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

        public bool TryContract(CompanyDefinition company)
        {
            if (company == null || IsContracted(company) || !IsUnlocked(company)) return false;

            contracted.Add(company);
            foreach (var item in company.ShelvesOnContract) Warehouse.Instance.BuildShelf(item);
            contractedSubject.OnNext(company);
            return true;
        }

        /// <summary>다음 입고 트럭의 회사. M4에서 교착 방지 가중치(GDD 7.8)로 바꾼다.</summary>
        public CompanyDefinition PickInboundCompany() => contracted[Random.Range(0, contracted.Count)];
    }
}
