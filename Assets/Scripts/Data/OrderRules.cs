using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>상차 트럭 주문 규칙 (GDD 8.1): 트럭 1대 합계 수량 가중치 · 두 색 확률. 주문서 큐 · 생성 간격은 삭제 (2026-11-15).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Order Rules", fileName = "OrderRules")]
    public sealed class OrderRules : ScriptableObject
    {
        [Serializable]
        public struct QuantityWeight
        {
            [Min(1)] public int quantity;
            [Min(0)] public int weight;
        }

        [SerializeField, Tooltip("상차 트럭 1대 합계 수량 가중치 (레벨 · 색 무관, GDD 8.1)")]
        List<QuantityWeight> quantityWeights = new()
        {
            new QuantityWeight { quantity = 3, weight = 40 },
            new QuantityWeight { quantity = 4, weight = 30 },
            new QuantityWeight { quantity = 5, weight = 20 },
            new QuantityWeight { quantity = 6, weight = 10 },
        };

        [SerializeField, Range(0f, 1f), Tooltip("요구할 수 있는 색이 2개 이상일 때 두 색을 요구할 확률 (색 최대 2, GDD 8.1 `임시`)")] float twoColorChance = 0.5f;

        public float TwoColorChance => twoColorChance;

        /// <summary>합계 수량 가중치 표에서 하나 뽑는다.</summary>
        public int RollQuantity()
        {
            if (quantityWeights.Count == 0) throw new InvalidOperationException($"{name}: 수량 가중치 표가 비어 있습니다.");
            var total = 0;
            foreach (var q in quantityWeights) total += q.weight;
            var roll = UnityEngine.Random.Range(0, total);
            foreach (var q in quantityWeights)
            {
                roll -= q.weight;
                if (roll < 0) return q.quantity;
            }
            return quantityWeights[^1].quantity;
        }
    }
}
