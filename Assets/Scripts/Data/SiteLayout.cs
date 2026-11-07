using System;
using System.Collections.Generic;
using UnityEngine;

namespace APC.Data
{
    /// <summary>부지 그리드 · 확장 칸 (GDD 4.2, 9.3). R = 행 (R1이 도로 쪽), C = 열 (왼쪽 입고 → 오른쪽 출고).</summary>
    [CreateAssetMenu(menuName = "APC/Data/Site Layout", fileName = "SiteLayout")]
    public sealed class SiteLayout : ScriptableObject
    {
        [Serializable]
        public struct Cell
        {
            public string id;
            [Min(1)] public int row;
            [Min(0)] public int column;
            [Tooltip("처음부터 열려 있는 칸")] public bool isStartArea;
            [Min(1)] public int unlockLevel;
            [Min(0)] public int cost;
            public string content;
        }

        [SerializeField, Min(1f), Tooltip("칸 한 변의 길이 (m) — 임시")] float cellSize = 10f;

        [SerializeField]
        List<Cell> cells = new()
        {
            new Cell { id = "R1C1", row = 1, column = 1, isStartArea = true, unlockLevel = 1, content = "입고 구역 (입고 베이 1, 픽업 존)" },
            new Cell { id = "R1C2", row = 1, column = 2, isStartArea = true, unlockLevel = 1, content = "공용 보관소" },
            new Cell { id = "R1C3", row = 1, column = 3, isStartArea = true, unlockLevel = 1, content = "선반 구역 A (선반 4개)" },
            new Cell { id = "R1C4", row = 1, column = 4, isStartArea = true, unlockLevel = 1, content = "출고 구역 (출고 베이 1, 현금 존)" },
            new Cell { id = "R2C1", row = 2, column = 1, isStartArea = true, unlockLevel = 1, content = "사무실 (계약 PC, 고용 데스크, 배치 현황판)" },
            new Cell { id = "E1", row = 1, column = 0, unlockLevel = 2, cost = 100, content = "입고 베이 2 (Lv.10에 베이 3 구매 패드)" },
            new Cell { id = "E5", row = 1, column = 5, unlockLevel = 3, cost = 150, content = "출고 베이 2 (Lv.8에 베이 3 구매 패드)" },
            new Cell { id = "E3", row = 2, column = 3, unlockLevel = 4, cost = 250, content = "포장 구역 (포장대 1, Lv.8에 포장대 2)" },
            new Cell { id = "E2", row = 2, column = 2, unlockLevel = 6, cost = 350, content = "선반 구역 B" },
            new Cell { id = "E4", row = 2, column = 4, unlockLevel = 9, cost = 500, content = "선반 구역 C" },
            new Cell { id = "E6", row = 2, column = 0, unlockLevel = 11, cost = 600, content = "반품 구역 (검수 테이블, 반품 랙)" },
        };

        public float CellSize => cellSize;
        public IReadOnlyList<Cell> Cells => cells;

        public bool TryGetCell(string id, out Cell cell)
        {
            foreach (var c in cells)
            {
                if (c.id != id) continue;
                cell = c;
                return true;
            }
            cell = default;
            return false;
        }
    }
}
