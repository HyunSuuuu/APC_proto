using System.Collections;
using APC.Core;
using APC.Data;
using APC.Economy;
using APC.Staff;
using UnityEngine;
using UnityEngine.AI;

namespace APC.Debugging
{
    /// <summary>
    /// 타이쿤 시험 씬(`TycoonTest.unity`, 메뉴 APC > Test > Build Tycoon 8x8) 전용: 시작하면 하차 · 상차 파트를 둘 다 열고(정산 없음),
    /// 모든 회사가 열리는 레벨까지 올린다. 알바는 기본 0명 — 고용 탭(시작 돈 지급)이나 DEBUG 메뉴 Staff 버튼으로 직접 뽑는다.
    /// 인스펙터에서 직무별 수를 넣으면 시작할 때 그만큼 자동으로 뽑는다 (고용금 · 최대 인원 무시). Game 씬에는 두지 않는다.
    /// </summary>
    public sealed class TycoonTest : MonoBehaviour
    {
        [SerializeField, Min(1)] int targetLevel = 5;
        [SerializeField, Min(0), Tooltip("시작 때 지갑에 넣는 돈 — 고용 탭에서 직접 뽑기용")] int startCash = 100000;
        [SerializeField, Min(0)] int unload;
        [SerializeField, Min(0)] int sort;
        [SerializeField, Min(0)] int pick;
        [SerializeField, Min(0)] int forklift;
        [SerializeField, Min(0f), Tooltip("알바 한 명씩 나오는 간격 (초) — 생성 지점에 한꺼번에 몰리지 않게")] float spawnInterval = 0.1f;

        IEnumerator Start()
        {
            PartService.AlwaysOpen = true;
            Logistics.InboundQueue.FillAtOnce = true; // 줄(칸마다 5대)을 바로 채우고 빠지면 바로 보충 (2026-11-25)
            var level = GameManager.Instance.Level;
            while (level.Level.CurrentValue < targetLevel) level.LevelUpNow();
            GameManager.Instance.Wallet.Add(startCash);

            // 네비메시가 구워질 때까지 (SiteNavMesh)
            yield return new WaitUntil(() => StaffService.Instance != null && NavMesh.SamplePosition(Vector3.zero, out _, 1000f, NavMesh.AllAreas));
            var wait = new WaitForSeconds(spawnInterval);
            foreach (var (role, count) in new[] { (StaffRole.Unload, unload), (StaffRole.Sort, sort), (StaffRole.Pick, pick), (StaffRole.Forklift, forklift) })
            {
                for (var i = 0; i < count; i++)
                {
                    StaffService.Instance.Spawn(role);
                    yield return wait;
                }
            }
            if (StaffService.Instance.Workers.Count > 0) Debug.Log($"[TycoonTest] 알바 {StaffService.Instance.Workers.Count}명");
        }
    }
}
