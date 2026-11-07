using System;

namespace APC.Data
{
    /// <summary>정수 범위 (양 끝 포함). 예: 품목 1종 × 2~4개.</summary>
    [Serializable]
    public struct IntRange
    {
        public int min;
        public int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>min 이상 max 이하의 정수를 무작위로 고른다.</summary>
        public int Roll() => UnityEngine.Random.Range(min, max + 1);
    }

    /// <summary>가중치가 붙은 값. 예: 알바 스탯 총합 4 (10%).</summary>
    [Serializable]
    public struct WeightedInt
    {
        public int value;
        public int weight;

        public WeightedInt(int value, int weight)
        {
            this.value = value;
            this.weight = weight;
        }
    }
}
