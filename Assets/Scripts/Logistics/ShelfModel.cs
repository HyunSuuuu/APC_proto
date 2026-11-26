using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 보관 구역 선반 모델 하나 (모양 · 상자 칸 배치만, GDD 7.3 M20-2). 보관함 · 존 · 수량은 구역 하나(`Shelf`)가 갖고,
    /// 모델은 `Shelf` 바로 아래 자식으로 네 모서리에 놓인다. 상자 칸 한 단 = 가로 boxesPerSide × 세로 boxRows, 다 차면 위 단.
    /// </summary>
    public sealed class ShelfModel : MonoBehaviour
    {
        [SerializeField, Tooltip("상자 칸 기준 (모델 로컬)")] Transform boxRoot;
        [SerializeField, Tooltip("품목 색 표시 (구역이 칠함)")] Renderer iconRenderer;

        [Header("칸 배치 (한 단 = 가로 boxesPerSide × 세로 boxRows)")]
        [SerializeField, Min(1)] int boxesPerSide = 3;
        [SerializeField, Min(0), Tooltip("한 단의 세로(z) 줄 수. 0이면 boxesPerSide (정사각형)")] int boxRows;
        [SerializeField, Min(0.1f)] float boxSpacing = 0.52f;
        [SerializeField] float firstLevelHeight = 0.13f;
        [SerializeField, Min(0.1f)] float levelHeight = 0.8f;

        public Renderer IconRenderer => iconRenderer;

        /// <summary>
        /// index번째 칸을 부모(`Shelf`) 로컬로. 크기 0 팝 연출 중에도 맞도록 월드 변환 대신 로컬 TRS를 곱한다.
        /// </summary>
        public Vector3 SlotInParent(int index)
        {
            var self = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
            var box = boxRoot != null && boxRoot != transform ? Matrix4x4.TRS(boxRoot.localPosition, boxRoot.localRotation, boxRoot.localScale) : Matrix4x4.identity;
            return (self * box).MultiplyPoint3x4(SlotLocal(index));
        }

        Vector3 SlotLocal(int index)
        {
            var rows = boxRows > 0 ? boxRows : boxesPerSide;
            var perLevel = boxesPerSide * rows;
            var level = index / perLevel;
            var inLevel = index % perLevel;
            var halfX = (boxesPerSide - 1) * 0.5f;
            var halfZ = (rows - 1) * 0.5f;
            return new Vector3((inLevel % boxesPerSide - halfX) * boxSpacing, firstLevelHeight + level * levelHeight, (inLevel / boxesPerSide - halfZ) * boxSpacing);
        }

        // 상자 칸 (2단)
        void OnDrawGizmosSelected()
        {
            var rows = boxRows > 0 ? boxRows : boxesPerSide;
            CargoGizmos.DrawSlots(boxRoot != null ? boxRoot : transform, SlotLocal, boxesPerSide * rows * 2, new Color(0.3f, 0.8f, 1f), CargoGizmos.BoxSize);
        }
    }
}
