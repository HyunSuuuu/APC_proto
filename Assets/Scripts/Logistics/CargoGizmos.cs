using System;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 상자가 쌓이는 자리를 에디터 씬 뷰에 그린다 (2026-11-19). 모듈 프리팹을 열면 팔레트 · 선반 · 현금 더미 칸이 보인다.
    /// 칸 자리(바닥 중앙)마다 상자 크기 와이어 박스, 첫 칸은 채운 박스. 실행 중 · 빌드에는 영향 없음.
    /// </summary>
    public static class CargoGizmos
    {
        /// <summary>상자 한 칸 크기 (CarryConfig.BoxHeight 0.5 · 색 상자 × 1.667). 에디터에선 config를 못 읽을 수 있어 고정.</summary>
        public static readonly Vector3 BoxSize = new(0.5f, 0.5f, 0.5f);

        public static void DrawSlots(Transform root, Func<int, Vector3> slot, int count, Color color, Vector3 size)
        {
            if (root == null || slot == null || count <= 0) return;
            var old = Gizmos.matrix;
            Gizmos.matrix = root.localToWorldMatrix;
            for (var i = 0; i < count; i++)
            {
                var center = slot(i) + Vector3.up * (size.y * 0.5f);
                Gizmos.color = i == 0 ? new Color(color.r, color.g, color.b, 0.35f) : color;
                if (i == 0) Gizmos.DrawCube(center, size);
                Gizmos.color = color;
                Gizmos.DrawWireCube(center, size);
            }
            Gizmos.matrix = old;
        }
    }
}
