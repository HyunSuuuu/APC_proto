using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 장애물 바닥 모양 (XZ 평면의 회전 상자). 선반 · 가구 · 레일을 길찾기 격자에 칠할 때 쓴다.
    /// AxisX = 상자 로컬 X축의 월드 XZ 방향(정규화), AxisZ = 그에 수직 (Unity의 transform.right · forward와 같은 관계).
    /// </summary>
    public readonly struct NavBox
    {
        public readonly Vector2 Center;
        public readonly Vector2 HalfSize;
        public readonly Vector2 AxisX;

        public NavBox(Vector2 center, Vector2 halfSize, Vector2 axisX)
        {
            Center = center;
            HalfSize = halfSize;
            AxisX = axisX.sqrMagnitude > 0f ? axisX.normalized : Vector2.right;
        }

        public Vector2 AxisZ => new(-AxisX.y, AxisX.x);

        /// <summary>점이 사방으로 inflate만큼 키운 상자 안(경계 포함)인가.</summary>
        public bool Contains(Vector2 point, float inflate)
        {
            var d = point - Center;
            return Mathf.Abs(Vector2.Dot(d, AxisX)) <= HalfSize.x + inflate
                   && Mathf.Abs(Vector2.Dot(d, AxisZ)) <= HalfSize.y + inflate;
        }

        /// <summary>키운 상자를 감싸는 월드 XZ 사각형 (x = X, y = Z).</summary>
        public Rect Bounds(float inflate)
        {
            var hx = HalfSize.x + inflate;
            var hz = HalfSize.y + inflate;
            var az = AxisZ;
            var ex = Mathf.Abs(AxisX.x) * hx + Mathf.Abs(az.x) * hz;
            var ez = Mathf.Abs(AxisX.y) * hx + Mathf.Abs(az.y) * hz;
            return new Rect(Center.x - ex, Center.y - ez, ex * 2f, ez * 2f);
        }
    }
}
