using System;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>바닥에 쌓는 화물 배치 (열 × 행 한 층을 채우고 위로 쌓음). 더미 · 공용 보관소 · 선반이 쓴다.</summary>
    [Serializable]
    public struct CargoGridLayout
    {
        [Min(1)] public int columns;
        [Min(1)] public int rows;
        [Min(0.1f), Tooltip("상자 간격 (m)")] public float spacing;
        [Min(0.05f), Tooltip("층 높이 (m)")] public float layerHeight;
        [Min(0f), Tooltip("세로(z) 줄 간격 (m). 0이면 spacing과 같음 — 길쭉한 물건(지폐 다발)을 빈틈없이 (2026-11-19)")] public float rowSpacing;

        public CargoGridLayout(int columns, int rows, float spacing, float layerHeight, float rowSpacing = 0f)
        {
            this.columns = columns;
            this.rows = rows;
            this.spacing = spacing;
            this.layerHeight = layerHeight;
            this.rowSpacing = rowSpacing;
        }

        public float RowSpacing => rowSpacing > 0f ? rowSpacing : spacing;

        public Vector3 GetLocalPosition(int index)
        {
            var cols = Mathf.Max(1, columns);
            var perLayer = cols * Mathf.Max(1, rows);
            var layer = index / perLayer;
            var inLayer = index % perLayer;
            var col = inLayer % cols;
            var row = inLayer / cols;
            return new Vector3(
                (col - (cols - 1) * 0.5f) * spacing,
                layer * layerHeight,
                (row - (Mathf.Max(1, rows) - 1) * 0.5f) * RowSpacing);
        }
    }
}
