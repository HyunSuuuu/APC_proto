using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 스프라이트 없이 정점으로 그리는 기본 UI 도형 (원 · 고리 · 둥근 사각형, 테두리 선택).
    /// 텍스처가 기본 흰색 하나라 같은 Canvas의 다른 도형 · <see cref="LevelUpKeyGraphic"/> · <see cref="LevelUpBurst"/>와
    /// 한 드로우콜로 묶인다 (레벨업 배너, 2026-11-25).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIShape : MaskableGraphic
    {
        public enum Kind { Circle, Ring, RoundRect }

        [SerializeField] Kind kind = Kind.RoundRect;
        [SerializeField, Tooltip("테두리 색 (안쪽으로 그림, 두께 0이면 없음)")] Color outlineColor = Color.black;
        [SerializeField, Min(0f)] float outlineWidth;
        [SerializeField, Min(0f), Tooltip("Ring: 고리 두께")] float ringWidth = 4f;
        [SerializeField, Min(0f), Tooltip("RoundRect: 모서리 반지름")] float cornerRadius = 8f;
        [SerializeField, Tooltip("RoundRect: 위 모서리만 둥글게")] bool topCornersOnly;

        public float RingWidth
        {
            get => ringWidth;
            set { if (Mathf.Approximately(ringWidth, value)) return; ringWidth = value; SetVerticesDirty(); }
        }

        /// <summary>바탕 · 테두리 색을 한 번에 바꾼다 (메시 한 번만 다시 만듦).</summary>
        public void SetColors(Color fill, Color outline)
        {
            outlineColor = outline;
            color = fill; // SetVerticesDirty
            SetVerticesDirty();
        }

        public void Setup(Kind shapeKind, Color fill, Color outline, float outlineSize, float radius, bool topOnly = false)
        {
            kind = shapeKind;
            outlineColor = outline;
            outlineWidth = outlineSize;
            cornerRadius = radius;
            topCornersOnly = topOnly;
            color = fill;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Color32 fill = color;
            Color o = outlineColor;
            o.a *= color.a;
            Color32 line = o;
            float ow = outlineWidth;

            switch (kind)
            {
                case Kind.Circle:
                {
                    float rx = r.width * 0.5f, ry = r.height * 0.5f;
                    if (ow > 0f) UIMesh.Ellipse(vh, r.center, rx, ry, line);
                    UIMesh.Ellipse(vh, r.center, Mathf.Max(0f, rx - ow), Mathf.Max(0f, ry - ow), fill);
                    break;
                }
                case Kind.Ring:
                {
                    float rad = Mathf.Min(r.width, r.height) * 0.5f;
                    UIMesh.Annulus(vh, r.center, rad, Mathf.Max(0f, rad - ringWidth), fill);
                    break;
                }
                default:
                {
                    if (ow > 0f) UIMesh.RoundRect(vh, r, cornerRadius, line, topCornersOnly);
                    var inner = new Rect(r.xMin + ow, r.yMin + ow, Mathf.Max(0f, r.width - ow * 2f), Mathf.Max(0f, r.height - ow * 2f));
                    UIMesh.RoundRect(vh, inner, Mathf.Max(0f, cornerRadius - ow), fill, topCornersOnly);
                    break;
                }
            }
        }
    }

    /// <summary>VertexHelper에 단색 도형을 더하는 도우미 (할당 없음: 공용 버퍼 사용, 메인 스레드 전용).</summary>
    public static class UIMesh
    {
        static readonly Vector2[] buffer = new Vector2[256];
        static readonly Vector2[] expanded = new Vector2[256];

        static void Vert(VertexHelper vh, Vector2 p, Color32 c)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vh.AddVert(v);
        }

        /// <summary>점 n개(볼록 또는 중심에서 보이는 별 모양)를 중심에서 부채꼴로 채운다.</summary>
        public static void Fan(VertexHelper vh, Vector2 center, Vector2[] pts, int n, Color32 c)
        {
            if (n < 3 || c.a == 0) return;
            int start = vh.currentVertCount;
            Vert(vh, center, c);
            for (int i = 0; i < n; i++) Vert(vh, pts[i], c);
            for (int i = 0; i < n; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % n);
        }

        public static void Ellipse(VertexHelper vh, Vector2 center, float rx, float ry, Color32 c, int segments = 40)
        {
            if (rx <= 0f || ry <= 0f) return;
            segments = Mathf.Clamp(segments, 8, buffer.Length);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                buffer[i] = new Vector2(center.x + Mathf.Cos(a) * rx, center.y + Mathf.Sin(a) * ry);
            }
            Fan(vh, center, buffer, segments, c);
        }

        public static void Annulus(VertexHelper vh, Vector2 center, float outer, float inner, Color32 c, int segments = 48)
        {
            if (outer <= 0f || c.a == 0) return;
            if (inner <= 0f) { Ellipse(vh, center, outer, outer, c, segments); return; }
            int start = vh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vert(vh, center + d * outer, c);
                Vert(vh, center + d * inner, c);
            }
            for (int i = 0; i < segments; i++)
            {
                int o0 = start + i * 2, i0 = o0 + 1;
                int o1 = start + (i + 1) % segments * 2, i1 = o1 + 1;
                vh.AddTriangle(o0, o1, i1);
                vh.AddTriangle(o0, i1, i0);
            }
        }

        public static void RoundRect(VertexHelper vh, Rect r, float radius, Color32 c, bool topOnly = false, int cornerSegments = 6)
        {
            if (r.width <= 0f || r.height <= 0f || c.a == 0) return;
            float rad = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            int n = 0;
            // 반시계: 오른쪽 아래 → 오른쪽 위 → 왼쪽 위 → 왼쪽 아래
            n = Corner(n, new Vector2(r.xMax, r.yMin), topOnly ? 0f : rad, -90f, cornerSegments);
            n = Corner(n, new Vector2(r.xMax, r.yMax), rad, 0f, cornerSegments);
            n = Corner(n, new Vector2(r.xMin, r.yMax), rad, 90f, cornerSegments);
            n = Corner(n, new Vector2(r.xMin, r.yMin), topOnly ? 0f : rad, 180f, cornerSegments);
            Fan(vh, r.center, buffer, n, c);
        }

        /// <summary>모서리 점 (반지름 0이면 꼭짓점 하나). corner = 사각형 꼭짓점, startDeg = 호 시작 각도.</summary>
        static int Corner(int n, Vector2 corner, float rad, float startDeg, int segments)
        {
            if (rad <= 0.01f) { buffer[n++] = corner; return n; }
            // 호의 중심 = 꼭짓점에서 안쪽으로 rad
            float a0 = startDeg * Mathf.Deg2Rad;
            var dirOut = new Vector2(Mathf.Cos(a0 + Mathf.PI * 0.25f), Mathf.Sin(a0 + Mathf.PI * 0.25f));
            var center = corner - new Vector2(Mathf.Sign(dirOut.x), Mathf.Sign(dirOut.y)) * rad;
            for (int i = 0; i <= segments; i++)
            {
                float a = a0 + i * (Mathf.PI * 0.5f) / segments;
                buffer[n++] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            }
            return n;
        }

        /// <summary>볼록 다각형을 바깥으로 w만큼 넓혀(뾰족한 꼭짓점은 3w까지) 채운다. 넓이가 0이면 그리지 않는다.</summary>
        public static void ConvexExpanded(VertexHelper vh, Vector2[] pts, int n, float w, Color32 c)
        {
            if (n < 3 || c.a == 0) return;
            float area = 0f;
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
                centroid += a;
            }
            if (Mathf.Abs(area) < 1e-4f) return;
            centroid /= n;
            if (w <= 0f) { Fan(vh, centroid, pts, n, c); return; }

            float sign = area > 0f ? 1f : -1f; // 반시계면 바깥 법선 = (dy, -dx)
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = pts[(i + n - 1) % n], cur = pts[i], next = pts[(i + 1) % n];
                Vector2 n1 = EdgeNormal(prev, cur, sign), n2 = EdgeNormal(cur, next, sign);
                Vector2 m = n1 + n2;
                if (m.sqrMagnitude < 1e-6f) m = n1;
                m.Normalize();
                float d = Vector2.Dot(m, n1);
                float len = d > 1e-3f ? Mathf.Min(w / d, w * 3f) : w;
                expanded[i] = cur + m * len;
            }
            Fan(vh, centroid, expanded, n, c);
        }

        static Vector2 EdgeNormal(Vector2 a, Vector2 b, float sign)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return Vector2.zero;
            d.Normalize();
            return new Vector2(d.y, -d.x) * sign;
        }

        /// <summary>꺾은선을 두께 width로 칠한다 (끝은 둥글게).</summary>
        public static void Stroke(VertexHelper vh, Vector2[] pts, int n, float width, Color32 c)
        {
            if (n < 2 || c.a == 0) return;
            float h = width * 0.5f;
            int start = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                Vector2 t = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                t.Normalize();
                var nrm = new Vector2(-t.y, t.x) * h;
                Vert(vh, pts[i] + nrm, c);
                Vert(vh, pts[i] - nrm, c);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 2, a + 3);
                vh.AddTriangle(a, a + 3, a + 1);
            }
            Vector2 first = pts[0], last = pts[n - 1];
            Ellipse(vh, first, h, h, c, 16);
            Ellipse(vh, last, h, h, c, 16);
        }
    }
}
