using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>알바 스탯 · 이력서 · 하루 정산 규칙 (GDD 11.1, 11.2, 11.7).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Staff Rules", fileName = "StaffRules")]
    public sealed class StaffRules : ScriptableObject
    {
        [Header("이력서")]
        [SerializeField, Min(1)] int candidateCount = 3;
        [SerializeField, Tooltip("스탯 총합과 확률 가중치")]
        List<WeightedInt> statTotalWeights = new()
        {
            new WeightedInt(4, 10), new WeightedInt(5, 20), new WeightedInt(6, 25),
            new WeightedInt(7, 20), new WeightedInt(8, 15), new WeightedInt(9, 10),
        };
        [SerializeField, Min(1)] int statMin = 1;
        [SerializeField, Min(1)] int statMax = 5;

        [Header("스탯 효과")]
        [SerializeField, Min(1)] int baseCarry = 2;
        [SerializeField, Min(0)] int carryPerStrength = 1;
        [SerializeField, Min(0.1f)] float baseMoveSpeed = 2.5f;
        [SerializeField, Min(0f), Tooltip("민첩 1포인트당 이동 속도 증가율")] float moveSpeedPerAgility = 0.2f;
        [SerializeField, Min(0f), Tooltip("지능 1포인트당 작업 속도 증가율")] float workSpeedPerIntelligence = 0.2f;
        [SerializeField, Min(0.01f), Tooltip("집기/내려놓기 간격 (초) ÷ 작업 배율")] float basePickInterval = 0.25f;
        [SerializeField, Min(0.1f), Tooltip("포장 게이지 (초) ÷ 작업 배율")] float basePackDuration = 4f;

        [Header("비용")]
        [SerializeField, Min(0)] int hireCostPerStat = 15;
        [SerializeField, Min(0)] int wagePerStat = 8;
        [SerializeField, Min(0)] int refreshCostPerLevel = 20;

        [Header("하루")]
        [SerializeField, Min(10f), Tooltip("하루 길이 (게임 시간 초). 끝나면 시급 정산 + 이력서 갱신")] float dayLengthSeconds = 600f;

        public int CandidateCount => candidateCount;
        public IReadOnlyList<WeightedInt> StatTotalWeights => statTotalWeights;
        public int StatMin => statMin;
        public int StatMax => statMax;
        public float BasePickInterval => basePickInterval;
        public float BasePackDuration => basePackDuration;
        public float DayLengthSeconds => dayLengthSeconds;

        /// <summary>최대 적재 수 = 기본 + 증가 × (힘 − 1)</summary>
        public int GetCarryCapacity(int strength) => baseCarry + carryPerStrength * (strength - statMin);

        /// <summary>이동 속도 = 기본 × (1 + 증가율 × (민첩 − 1))</summary>
        public float GetMoveSpeed(int agility) => baseMoveSpeed * (1f + moveSpeedPerAgility * (agility - statMin));

        /// <summary>작업 속도 배율 = 1 + 증가율 × (지능 − 1)</summary>
        public float GetWorkSpeedMultiplier(int intelligence) => 1f + workSpeedPerIntelligence * (intelligence - statMin);

        public int GetHireCost(int statTotal) => statTotal * hireCostPerStat;
        public int GetWage(int statTotal) => statTotal * wagePerStat;
        public int GetRefreshCost(int level) => refreshCostPerLevel * Mathf.Max(1, level);
    }
}
