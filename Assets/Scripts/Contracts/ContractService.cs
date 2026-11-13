using System.Collections;
using System.Collections.Generic;
using APC.Core;
using APC.Data;
using APC.Logistics;
using R3;
using UnityEngine;

namespace APC.Contracts
{
    /// <summary>
    /// 회사 입고 (GDD 6.3). 계약 시스템은 삭제했다 (2026-11-13): 회사는 **입고 시작 레벨이 되면 자동으로** 들어온다.
    /// 들어오면 ① 입고 트럭이 그 회사도 고르기 시작하고(가중치, StockBalancer) ② 그 회사 주문이 생기기 시작한다 (OrderService가 OnContracted를 구독).
    /// `Contracted` · `OnContracted` 이름은 기존 구독자를 위해 그대로 둔다 (= 입고 중인 회사). 씬에 하나 둔다.
    /// </summary>
    public sealed class ContractService : MonoBehaviour
    {
        public static ContractService Instance { get; private set; }

        readonly List<CompanyDefinition> contracted = new();
        readonly Subject<CompanyDefinition> contractedSubject = new();

        /// <summary>입고가 시작된 회사 (레벨 순).</summary>
        public IReadOnlyList<CompanyDefinition> Contracted => contracted;
        public bool HasAnyContract => contracted.Count > 0;

        /// <summary>회사 입고가 시작될 때마다 그 회사를 보낸다.</summary>
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

        IEnumerator Start()
        {
            // 한 프레임 기다린다: 구독자(OrderService · PartService)가 Start에서 OnContracted를 구독한 뒤에 Lv.1 회사를 보낸다.
            yield return null;
            GameManager.Instance.Level.Level.Subscribe(Activate).AddTo(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            contractedSubject.Dispose();
        }

        void Activate(int level)
        {
            foreach (var company in GameManager.Instance.Database.Companies)
            {
                if (company.UnlockLevel > level || contracted.Contains(company)) continue;
                contracted.Add(company);
                contractedSubject.OnNext(company);
            }
            BuildMissingShelves();
        }

        // shortcut: 회사별 선반 모듈(M11-5) 전까지 임시 — 입고 중인 회사 품목에 선반이 없으면 그 회사 선반 구역(없으면 아무 곳)의 빈 자리에 1개 세운다. 자리가 없으면 다음 레벨업 때 다시 시도.
        void BuildMissingShelves()
        {
            foreach (var company in contracted)
            {
                foreach (var item in company.Items)
                {
                    if (!Warehouse.Instance.HasShelf(item) && Warehouse.Instance.BuildShelf(item, company.ShelfAreaId) == null)
                        Warehouse.Instance.BuildShelf(item, null);
                }
            }
        }
    }
}
