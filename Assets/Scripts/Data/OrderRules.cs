using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>주문 생성 규칙 (GDD 8.1, 8.2). 단일 주문만 (품목 1종 × 수량).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Order Rules", fileName = "OrderRules")]
    public sealed class OrderRules : ScriptableObject
    {
        [Serializable]
        public struct Bracket
        {
            [Min(1), Tooltip("이 레벨부터 적용")] public int minLevel;
            [Min(1f), Tooltip("주문 생성 간격 (초)")] public float spawnInterval;
        }

        [Serializable]
        public struct QuantityWeight
        {
            [Min(1)] public int quantity;
            [Min(0)] public int weight;
        }

        [SerializeField, Tooltip("minLevel 오름차순")]
        List<Bracket> brackets = new()
        {
            new Bracket { minLevel = 1, spawnInterval = 15f },
            new Bracket { minLevel = 4, spawnInterval = 12f },
        };

        [SerializeField, Tooltip("주문 수량 가중치 (레벨 · 회사 무관, GDD 8.1)")]
        List<QuantityWeight> quantityWeights = new()
        {
            new QuantityWeight { quantity = 3, weight = 40 },
            new QuantityWeight { quantity = 4, weight = 30 },
            new QuantityWeight { quantity = 5, weight = 20 },
            new QuantityWeight { quantity = 6, weight = 10 },
        };

        [SerializeField, Min(1), Tooltip("큐에 이만큼 쌓이면 생성을 멈춘다")] int maxQueuedOrders = 8;

        public int MaxQueuedOrders => maxQueuedOrders;

        /// <summary>해당 레벨에 적용되는 구간 (minLevel ≤ level 중 마지막).</summary>
        public Bracket GetBracket(int level)
        {
            if (brackets.Count == 0) throw new InvalidOperationException($"{name}: 주문 구간이 비어 있습니다.");
            var result = brackets[0];
            foreach (var b in brackets)
            {
                if (b.minLevel <= level) result = b;
                else break;
            }
            return result;
        }

        /// <summary>수량 가중치 표에서 하나 뽑는다.</summary>
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
