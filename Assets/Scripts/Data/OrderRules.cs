using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>주문 생성 규칙 (GDD 7.8, 8.1, 8.2).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Order Rules", fileName = "OrderRules")]
    public sealed class OrderRules : ScriptableObject
    {
        [Serializable]
        public struct Bracket
        {
            [Min(1), Tooltip("이 레벨부터 적용")] public int minLevel;
            [Min(1f), Tooltip("주문 생성 간격 (초)")] public float spawnInterval;
            [Range(0f, 1f), Tooltip("복합 주문 확률. 0이면 단일 주문만")] public float compoundChance;
            [Tooltip("단일 주문: 품목 1종 × 수량")] public IntRange singleQuantity;
            [Tooltip("복합 주문: 품목 종류 수")] public IntRange compoundKinds;
            [Tooltip("복합 주문: 품목당 수량")] public IntRange compoundQuantityPerItem;
            [Min(1), Tooltip("복합 주문 수량 합계 상한")] public int compoundMaxTotal;
        }

        [SerializeField, Tooltip("minLevel 오름차순")]
        List<Bracket> brackets = new()
        {
            new Bracket { minLevel = 1, spawnInterval = 15f, compoundChance = 0f, singleQuantity = new IntRange(2, 4), compoundKinds = new IntRange(2, 2), compoundQuantityPerItem = new IntRange(1, 3), compoundMaxTotal = 8 },
            new Bracket { minLevel = 4, spawnInterval = 12f, compoundChance = 0.6f, singleQuantity = new IntRange(2, 4), compoundKinds = new IntRange(2, 2), compoundQuantityPerItem = new IntRange(1, 3), compoundMaxTotal = 8 },
            new Bracket { minLevel = 7, spawnInterval = 12f, compoundChance = 0.6f, singleQuantity = new IntRange(2, 4), compoundKinds = new IntRange(2, 3), compoundQuantityPerItem = new IntRange(1, 3), compoundMaxTotal = 8 },
        };

        [SerializeField, Min(1), Tooltip("큐에 이만큼 쌓이면 생성을 멈춘다")] int maxQueuedOrders = 8;

        [Header("교착 방지 (7.8)")]
        [SerializeField, Range(0f, 1f), Tooltip("입고 품목 가중치의 품목별 최소 비율")] float inboundMinWeight = 0.1f;

        public int MaxQueuedOrders => maxQueuedOrders;
        public float InboundMinWeight => inboundMinWeight;

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
    }
}
