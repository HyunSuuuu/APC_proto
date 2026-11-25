using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 레벨업 배너의 열쇠 (2026-11-25). 뾰족한 끝 · 바깥으로 튀어나온 뾰족한 톱니 2개 · 둥근 손잡이(아래 작은 구멍).
    /// <see cref="Turn"/>(0 → 1)로 세로축 90° 돌린다: 앞면 폭은 cosθ로 줄고, 옆면(두께) 폭이 두께 × sinθ로 늘어난다.
    /// 손잡이가 몸통보다 두꺼워서 끝까지 돌리면 두께만 보이는 막대가 된다 (종이처럼 납작해지지 않게).
    ///
    /// 좌표는 설계 단위(열쇠 끝 = 0, 아래로 +y, 손잡이 중심 y = 62)이고 <see cref="unit"/>배 해서 UI로 옮긴다.
    /// 피벗 = 열쇠 끝. 기본 흰 텍스처라 다른 <see cref="UIShape"/>와 한 드로우콜로 묶인다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LevelUpKeyGraphic : MaskableGraphic
    {
        [SerializeField, Tooltip("설계 단위 1 = UI 몇 px")] float unit = 1.5f;
        [SerializeField, Range(0f, 1f), Tooltip("0 = 정면, 1 = 90° 돌아가 옆면만")] float turn;
        [SerializeField, Tooltip("몸통(샤프트) 두께 (설계 단위)")] float shaftThickness = 5.5f;
        [SerializeField, Tooltip("손잡이 두께 (설계 단위). 몸통보다 두꺼움")] float bowThickness = 10f;
        [SerializeField, Tooltip("바깥 테두리 두께 (px)")] float outlinePx = 2.4f;
        [SerializeField, Tooltip("앞면 테두리 두께 (px)")] float frontOutlinePx = 1.2f;
        [SerializeField] Color outline = Color.black;
        [SerializeField] Color front = Color.white;
        [SerializeField] Color sideLight = new Color32(0xD4, 0xD4, 0xD4, 0xFF);
        [SerializeField] Color sideDark = new Color32(0x8F, 0x8F, 0x8F, 0xFF);
        [SerializeField] Color hole = new Color32(0x3B, 0x20, 0x00, 0xFF);

        public float Turn
        {
            get => turn;
            set { value = Mathf.Clamp01(value); if (Mathf.Approximately(turn, value)) return; turn = value; SetVerticesDirty(); }
        }

        // 몸통 조각 (모두 볼록): 사각 몸통, 뾰족한 끝, 왼쪽으로 튀어나온 톱니 2개 (몸통 아래쪽 = 자물쇠 밖으로 보이는 곳)
        static readonly Vector2[][] ShaftPieces =
        {
            new[] { new Vector2(-4f, 5f), new Vector2(4f, 5f), new Vector2(4f, 50f), new Vector2(-4f, 50f) },
            new[] { new Vector2(-4f, 5.2f), new Vector2(0f, 0f), new Vector2(4f, 5.2f) },
            new[] { new Vector2(-4f, 25f), new Vector2(-10.5f, 27.5f), new Vector2(-4f, 31.5f) },
            new[] { new Vector2(-4f, 35f), new Vector2(-10.5f, 37.5f), new Vector2(-4f, 41.5f) },
        };
        const float BowY = 62f, BowR = 14f;
        const float ShaftTop = 2f, ShaftBottom = 50f, BowTop = 48f, BowBottom = 76f;

        static readonly Vector2[] tmp = new Vector2[8];

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float th = turn * Mathf.PI * 0.5f;
            float c = Mathf.Max(Mathf.Cos(th), 0f), s = Mathf.Sin(th);
            float os = shaftThickness * s, ob = bowThickness * s;
            float alpha = color.a;

            Color32 line = Tint(outline, alpha);
            // 1) 바깥 테두리: 앞 · 뒤 두 장 + 사이 두께를 통째로 넓혀 검게
            Extrusion(vh, c, os, ob, outlinePx / unit, line);

            // 2) 옆면(두께): 같은 모양을 회색으로, 왼쪽 밝게 → 오른쪽 어둡게
            int sideStart = vh.currentVertCount;
            Extrusion(vh, c, os, ob, 0f, Tint(sideLight, alpha));
            ShadeSide(vh, sideStart, alpha);

            // 3) 앞면: 돌아갈수록 폭이 줄고 살짝 어두워짐
            if (c < 0.04f) return;
            float g = 1f - 0.18f * s;
            Color32 face = Tint(new Color(front.r * g, front.g * g, front.b * g, front.a), alpha);
            float wf = frontOutlinePx / unit;
            Shaft(vh, -os * 0.5f, c, wf, line);
            Bow(vh, -ob * 0.5f, c, wf, line);
            Shaft(vh, -os * 0.5f, c, 0f, face);
            Bow(vh, -ob * 0.5f, c, 0f, face);
            UIMesh.Ellipse(vh, ToUI(-ob * 0.5f, 67f), 3.4f * c * unit, 3.6f * unit, Tint(hole, alpha), 20);
        }

        void Extrusion(VertexHelper vh, float c, float os, float ob, float w, Color32 col)
        {
            Shaft(vh, -os * 0.5f, c, w, col);
            Shaft(vh, os * 0.5f, c, w, col);
            if (os > 0.05f) Box(vh, -os * 0.5f, os * 0.5f, ShaftTop, ShaftBottom, w, col);
            Bow(vh, -ob * 0.5f, c, w, col);
            Bow(vh, ob * 0.5f, c, w, col);
            if (ob > 0.05f) Box(vh, -ob * 0.5f, ob * 0.5f, BowTop, BowBottom, w, col);
        }

        void Shaft(VertexHelper vh, float shift, float scaleX, float w, Color32 col)
        {
            float sx = Mathf.Max(scaleX, 0.001f);
            for (int p = 0; p < ShaftPieces.Length; p++)
            {
                var piece = ShaftPieces[p];
                for (int i = 0; i < piece.Length; i++) tmp[i] = ToUI(shift + piece[i].x * sx, piece[i].y);
                UIMesh.ConvexExpanded(vh, tmp, piece.Length, w * unit, col);
            }
        }

        void Bow(VertexHelper vh, float shift, float scaleX, float w, Color32 col)
        {
            UIMesh.Ellipse(vh, ToUI(shift, BowY), (BowR * scaleX + w) * unit, (BowR + w) * unit, col, 36);
        }

        void Box(VertexHelper vh, float x0, float x1, float y0, float y1, float w, Color32 col)
        {
            tmp[0] = ToUI(x0, y0); tmp[1] = ToUI(x1, y0); tmp[2] = ToUI(x1, y1); tmp[3] = ToUI(x0, y1);
            UIMesh.ConvexExpanded(vh, tmp, 4, w * unit, col);
        }

        /// <summary>옆면 정점 색을 x 위치에 따라 밝음 → 어두움으로.</summary>
        void ShadeSide(VertexHelper vh, int start, float alpha)
        {
            int end = vh.currentVertCount;
            if (end <= start) return;
            var v = new UIVertex();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = start; i < end; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                min = Mathf.Min(min, v.position.x);
                max = Mathf.Max(max, v.position.x);
            }
            float span = Mathf.Max(0.001f, max - min);
            for (int i = start; i < end; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = (v.position.x - min) / span;
                v.color = Tint(t < 0.5f ? Color.Lerp(sideLight, Color.Lerp(sideLight, sideDark, 0.4f), t * 2f)
                                        : Color.Lerp(Color.Lerp(sideLight, sideDark, 0.4f), sideDark, (t - 0.5f) * 2f), alpha);
                vh.SetUIVertex(v, i);
            }
        }

        Vector2 ToUI(float x, float y) => new Vector2(x * unit, -y * unit);

        static Color32 Tint(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);
    }
}
