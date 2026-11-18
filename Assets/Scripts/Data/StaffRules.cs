using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>알바 직무 5개 (GDD 11.3). 하차 = 트럭 → 하차 칸 공간, 보관(Sort) = 공간 → 선반, 상차(Pick) = 선반 → 상차 트럭, 반품 = 반품 구역 (M13-12), 지게차 = D 가구만 (M11-8). 숫자 고정 — 끝에만 더한다.</summary>
    public enum StaffRole { Unload, Sort, Pick, Returns, Forklift }

    /// <summary>알바 스탯 2종 (GDD 11.2). 각각 최소 1, 최대 5. 고용할 때 정해지고 바뀌지 않는다.</summary>
    [System.Serializable]
    public struct StaffStats
    {
        public int moveSpeed;
        public int carry;

        public StaffStats(int moveSpeed, int carry)
        {
            this.moveSpeed = moveSpeed;
            this.carry = carry;
        }

        public int Total => moveSpeed + carry;
    }

    /// <summary>알바 스탯 생성 · 효과 · 비용 · 외형 (GDD 11.1, 11.2, 11.7, 9.4).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Staff Rules", fileName = "StaffRules")]
    public sealed class StaffRules : ScriptableObject
    {
        [Header("스탯 생성 (GDD 11.2)")]
        [SerializeField, Tooltip("스탯 총합과 확률 가중치 (가운데 값일수록 높게)")]
        List<WeightedInt> statTotalWeights = new()
        {
            new WeightedInt(2, 5), new WeightedInt(3, 10), new WeightedInt(4, 20), new WeightedInt(5, 25),
            new WeightedInt(6, 20), new WeightedInt(7, 12), new WeightedInt(8, 8),
        };
        [SerializeField, Min(1)] int statMin = 1;
        [SerializeField, Min(1)] int statMax = 5;

        [Header("스탯 효과")]
        [SerializeField, Min(1)] int baseCarry = 2;
        [SerializeField, Min(0), Tooltip("적재량 1포인트당 최대 적재 수 증가")] int carryPerPoint = 1;
        [SerializeField, Min(0.1f)] float baseMoveSpeed = 2.5f;
        [SerializeField, Min(0f), Tooltip("이동 속도 1포인트당 이동 속도 증가율")] float moveSpeedPerPoint = 0.2f;
        [SerializeField, Min(0.01f), Tooltip("집기/내려놓기 간격 (초)")] float pickInterval = 0.25f;
        [SerializeField, Min(1), Tooltip("반품 알바 적재 수 (스탯 무관, GDD 11.3 임시)")] int returnsCarry = 12;
        [SerializeField, Min(1), Tooltip("지게차 알바 적재 수 (스탯 무관 고정, GDD 11.3 임시)")] int forkliftCarry = 4;

        [Header("작업")]
        [SerializeField, Min(0f), Tooltip("작업 지점(슬롯)에서 멈춘 뒤 상호작용을 시작하기까지 (초). 플레이어는 PlayerConfig")] float interactionDelay = 0.5f;
        [SerializeField, Min(0.05f), Tooltip("옮길 것이 이만큼(초) 없으면 그 작업을 끝낸다")] float workIdleTimeout = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("작업 판단 간격 (초)")] float thinkInterval = 0.25f;

        [Header("고용 (계약 PC 고용 탭)")]
        [SerializeField, Min(1), Tooltip("고용 탭 후보 수. 파트마다 · [새로고침]으로 다시 뽑는다")] int candidateCount = 3;

        [Header("비용")]
        [SerializeField, Min(0)] int hireCostPerStat = 15;
        [SerializeField, Min(0), Tooltip("파트 정산마다")] int wagePerStat = 4;
        [SerializeField, Min(0), Tooltip("지게차 알바 고용금 추가 (월급은 그대로, GDD 9.4 임시)")] int forkliftHireBonus = 300;

        [Header("외형 (GDD 11.1)")]
        [SerializeField, Range(0f, 1f), Tooltip("코스튬 알바가 나올 확률 (나머지는 물류 유니폼)")] float costumeChance = 0.1f;

        [Header("직무 색 (발밑 원판)")]
        [SerializeField] Color unloadColor = new(0.95f, 0.55f, 0.15f);
        [SerializeField] Color sortColor = new(0.15f, 0.75f, 0.75f);
        [SerializeField] Color pickColor = new(0.55f, 0.35f, 0.8f);
        [SerializeField] Color returnsColor = new(0.85f, 0.3f, 0.45f);
        [SerializeField] Color forkliftColor = new(0.95f, 0.8f, 0.15f);

        public int CandidateCount => candidateCount;
        public int StatMin => statMin;
        public int StatMax => statMax;
        public float PickInterval => pickInterval;
        public float InteractionDelay => interactionDelay;
        public float WorkIdleTimeout => workIdleTimeout;
        public float ThinkInterval => thinkInterval;

        /// <summary>디버그 메뉴 [Costume 100%] (DebugMenu가 Play 시작마다 끈다).</summary>
        public static bool ForceCostume;

        public bool RollCostume() => ForceCostume || Random.value < costumeChance;

        /// <summary>최대 적재 수 = 기본 + 증가 × (적재량 − 1)</summary>
        public int GetCarryCapacity(int carry) => baseCarry + carryPerPoint * (carry - statMin);

        /// <summary>이동 속도 = 기본 × (1 + 증가율 × (이동 속도 − 1))</summary>
        public float GetMoveSpeed(int moveSpeed) => baseMoveSpeed * (1f + moveSpeedPerPoint * (moveSpeed - statMin));

        /// <summary>그 직무의 최대 적재 수: 반품 · 지게차는 고정, 나머지는 적재량 스탯 (GDD 11.2 · 11.3).</summary>
        public int GetCarry(StaffRole role, int carry) => role switch
        {
            StaffRole.Returns => returnsCarry,
            StaffRole.Forklift => forkliftCarry,
            _ => GetCarryCapacity(carry),
        };

        /// <summary>고용금 = 스탯 총합 × 계수 (+ 지게차 추가금, GDD 9.4).</summary>
        public int GetHireCost(StaffRole role, int statTotal) => statTotal * hireCostPerStat + (role == StaffRole.Forklift ? forkliftHireBonus : 0);
        public int GetWage(int statTotal) => statTotal * wagePerStat;

        public Color GetRoleColor(StaffRole role) => role switch
        {
            StaffRole.Unload => unloadColor,
            StaffRole.Sort => sortColor,
            StaffRole.Pick => pickColor,
            StaffRole.Forklift => forkliftColor,
            _ => returnsColor,
        };

        /// <summary>스탯 생성 (GDD 11.2): 총합을 가중치로 뽑고, 두 스탯을 최소값에서 시작해 남은 포인트를 무작위로 나눈다 (스탯당 최대값까지).</summary>
        public StaffStats RollStats()
        {
            var total = Mathf.Clamp(RollTotal(), statMin * 2, statMax * 2);
            var stats = new StaffStats(statMin, statMin);
            for (var left = total - statMin * 2; left > 0; left--)
            {
                var toSpeed = stats.carry >= statMax || (stats.moveSpeed < statMax && Random.value < 0.5f);
                if (toSpeed) stats.moveSpeed++;
                else stats.carry++;
            }
            Debug.Assert(stats.Total == total && stats.moveSpeed <= statMax && stats.carry <= statMax, "[StaffRules] 스탯 생성 오류");
            return stats;
        }

        int RollTotal()
        {
            var sum = 0;
            foreach (var w in statTotalWeights) sum += Mathf.Max(0, w.weight);
            if (sum <= 0) return statMin * 2;

            var roll = Random.Range(0, sum);
            foreach (var w in statTotalWeights)
            {
                roll -= Mathf.Max(0, w.weight);
                if (roll < 0) return w.value;
            }
            return statTotalWeights[^1].value;
        }
    }
}
