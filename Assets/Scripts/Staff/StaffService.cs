using System.Collections.Generic;
using APC.Core;
using APC.Data;
using UnityEngine;
using UnityEngine.AI;

namespace APC.Staff
{
    /// <summary>
    /// 알바 명단 · 생성 (GDD 11.1, 11.3). 씬에 하나 둔다 (Site/Logistics).
    /// M6-5 고용 데스크 전까지는 디버그 메뉴가 Spawn을 부른다 (고용금 · 최대 인원 없음).
    /// </summary>
    public sealed class StaffService : MonoBehaviour
    {
        public static StaffService Instance { get; private set; }

        static readonly StaffRole[] RoleOrder = { StaffRole.Unload, StaffRole.Sort, StaffRole.Pick };

        [SerializeField] StaffWorker workerPrefab;

        readonly List<StaffWorker> workers = new(); // 고용 순서

        /// <summary>고용 순서대로 (시급 지급 순서, GDD 11.7).</summary>
        public IReadOnlyList<StaffWorker> Workers => workers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[StaffService] 씬에 StaffService가 두 개 있습니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>빈 역할 (하차 → 분류 → 피킹 순, GDD 11.1). 모두 차 있으면 인원이 가장 적은 역할.</summary>
        public StaffRole NextEmptyRole()
        {
            var best = RoleOrder[0];
            var bestCount = int.MaxValue;
            foreach (var role in RoleOrder)
            {
                var count = CountRole(role);
                if (count >= bestCount) continue;
                best = role;
                bestCount = count;
            }
            return best;
        }

        public int CountRole(StaffRole role)
        {
            var n = 0;
            foreach (var worker in workers)
            {
                if (worker.Role == role) n++;
            }
            return n;
        }

        /// <summary>사무실 문 앞에 알바 1명을 만든다 (스탯은 무작위). 네비메시가 없으면 null.</summary>
        public StaffWorker Spawn(StaffRole role)
        {
            var spawn = StaffSpawnPoint.Current;
            var position = spawn != null ? spawn.transform.position : transform.position;
            if (!NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[StaffService] 알바를 놓을 네비메시가 없습니다.", this);
                return null;
            }

            var worker = Instantiate(workerPrefab, hit.position, spawn != null ? spawn.transform.rotation : Quaternion.identity, transform);
            worker.name = $"Staff_{workers.Count + 1}_{role}";
            worker.Setup(role, GameManager.Instance.Database.Staff.RollStats());
            workers.Add(worker);
            FacilityPop.Play(worker.transform);
            return worker;
        }

        internal void Remove(StaffWorker worker) => workers.Remove(worker);
    }
}
