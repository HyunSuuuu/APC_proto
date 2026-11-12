using System.Collections.Generic;
using APC.Core;
using APC.Contracts;
using APC.Interaction;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 반품 검수 테이블 (GDD 7.7, 플레이어 전용). 존에 멈춰 서 있으면 롤테이너의 반품 상자를 하나씩 검수 + 재포장한다:
    /// 게이지(`LogisticsConfig.inspectionDuration`)가 차면 반품 상자가 **선반에 자리가 남은 계약 품목 중 무작위 하나의 일반 상자**(흰 상자)로 바뀌어
    /// 재포장 출구에 쌓이고 XP. 출구가 가득 차거나, 반품이 없거나, 선반에 자리가 남은 품목이 없으면 게이지가 멈춘다. 그 뒤로는 일반 화물 (출구 → 선반, 분류 알바도 나름).
    /// 알바는 CargoZone만 작업 대상으로 찾으므로 이 존을 쓰지 않는다.
    /// </summary>
    public sealed class InspectionTable : GaugeZone
    {
        [SerializeField, Tooltip("반품 대기 롤테이너 (StorageKind.Returns)")] SharedStorage returnBin;
        [SerializeField, Tooltip("재포장 출구 (StorageKind.Repacked)")] SharedStorage output;
        [SerializeField, Tooltip("검수 중인 상자를 올려 둘 곳 (테이블 위)")] Transform workPoint;

        void Start() => Duration = GameManager.Instance.Database.Logistics.InspectionDuration;

        readonly List<Data.ItemDefinition> candidates = new();

        /// <summary>반품이 있고, 출구에 빈자리가 있고, **선반에 자리가 남은 계약 품목**이 있을 때만 게이지가 찬다 (없으면 자리가 빌 때까지 대기).</summary>
        protected override bool CanProgress(Interactor who) => returnBin.HasReturn && output.HasSpace && FindCandidates() > 0;

        protected override void OnCompleted(Interactor who)
        {
            ResetGauge();
            if (FindCandidates() == 0) return;
            var cargo = returnBin.TakeReturn();
            if (cargo == null) return;

            cargo.Repack(candidates[Random.Range(0, candidates.Count)]);
            if (workPoint != null) cargo.transform.position = workPoint.position;
            output.Store(cargo); // 테이블 위에서 출구로 날아간다
            GameManager.Instance.Level.AddXp(GameManager.Instance.Database.Economy.XpReturnInspection);
        }

        /// <summary>
        /// 재포장으로 만들 수 있는 품목 = 계약 품목 중 선반에 자리가 남은 것 (GDD 7.7).
        /// 자리 = 선반 빈자리 − 들고 가는 수 − 분류 알바 예약(`ShelfAllowance`) − 출구에서 이미 기다리는 수.
        /// 플레이어가 예약보다 더 집어 0 이하가 되면 그 품목은 빠진다.
        /// </summary>
        int FindCandidates()
        {
            candidates.Clear();
            var service = ContractService.Instance;
            if (service == null) return 0;
            foreach (var company in service.Contracted)
            {
                foreach (var item in company.Items)
                {
                    if (SharedStorage.ShelfAllowance(item) - WaitingInOutputs(item) > 0) candidates.Add(item);
                }
            }
            return candidates.Count;
        }

        static int WaitingInOutputs(Data.ItemDefinition item)
        {
            var n = 0;
            foreach (var storage in SharedStorage.All)
            {
                if (storage.Kind == StorageKind.Repacked) n += storage.CountOf(item);
            }
            return n;
        }
    }
}
