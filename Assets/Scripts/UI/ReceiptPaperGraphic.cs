using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// (UI 샘플 SettlementReceipt에서 가져옴, 2026-11-20 — 일일 정산 영수증 말림)
    /// 감열 영수증 용지 바탕 그래픽.
    ///  - 상단 톱니 절단면(삼각형 반복)
    ///  - 감열지 도트 질감(반복 텍스처) + 좌우 가장자리 살짝 어두운 그라데이션
    ///  - 상단 말림 구간을 가로 띠로 잘게 세분화해 <see cref="ReceiptCurl"/>로 휘고, 뒷면은 별도 색으로 칠한다.
    ///
    /// 앞으로 넘어와 늘어진 윗부분은 그 아래 인쇄된 글씨를 "덮어야" 하므로 2장으로 나눠 그린다:
    ///  - <see cref="Part.Flat"/>  : 종이 루트. 굽힘선 아래 평평한 부분 (글씨보다 먼저 그려짐)
    ///  - <see cref="Part.Curl"/>  : 종이의 마지막 자식. 굽힘선 위 말린 부분 + 그림자 (글씨보다 나중에 그려짐)
    /// 두 그래픽 모두 <see cref="paperSpace"/>(종이 루트) 좌표로 메시를 계산한 뒤 자기 로컬로 옮긴다.
    /// 클릭 시 <see cref="Clicked"/> 이벤트 (출력 중 = 스킵, 출력 후 = 뜯기 용도).
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public class ReceiptPaperGraphic : MaskableGraphic, IPointerClickHandler
    {
        public enum Part
        {
            Whole,
            Flat,
            Curl
        }

        public ReceiptCurl curl;
        public Part part = Part.Whole;
        [Tooltip("종이 좌표 기준 RectTransform (비우면 자기 자신)")]
        public RectTransform paperSpace;

        [Header("Serrated Top")]
        public float toothWidth = 21f;
        public float toothHeight = 15f;

        [Header("Paper Look")]
        public Color backColor = new Color(0.86f, 0.82f, 0.74f);
        [Range(0f, 0.2f)] public float edgeWidth = 0.06f;
        public Color edgeTint = new Color(0.92f, 0.88f, 0.81f);
        public float dotSpacing = 17f;

        [Header("Curl")]
        public float curlStep = 3f;
        [Tooltip("넘어온 날개가 종이에 드리우는 그림자 (Curl 파트 전용)")]
        public Color shadowColor = new Color(0.24f, 0.15f, 0.08f, 0.28f);
        public Vector2 shadowOffset = new Vector2(0f, -6f);

        public event Action Clicked;

        private static Texture2D dotTexture;
        private readonly List<float> rowYs = new List<float>(64);
        private Matrix4x4 toLocal = Matrix4x4.identity;

        public override Texture mainTexture => DotTexture;

        private RectTransform Space => paperSpace != null ? paperSpace : rectTransform;

        private static Texture2D DotTexture
        {
            get
            {
                if (dotTexture != null) return dotTexture;
                const int n = 16;
                dotTexture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    name = "ReceiptDotTexture",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var dot = new Color(0.955f, 0.94f, 0.92f, 1f);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    float t = Mathf.Clamp01(1.6f - d);
                    dotTexture.SetPixel(x, y, Color.Lerp(Color.white, dot, t));
                }
                dotTexture.Apply();
                return dotTexture;
            }
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            RectTransform space = Space;
            Rect r = space.rect;
            if (r.width <= 0f || r.height <= toothHeight) return;

            toLocal = space == rectTransform ? Matrix4x4.identity : rectTransform.worldToLocalMatrix * space.localToWorldMatrix;

            float top = r.yMax;
            float bodyTop = top - toothHeight;
            float bottom = r.yMin;
            bool hasCurl = curl != null && curl.curlLength > 0f;
            float bendY = hasCurl ? Mathf.Clamp(top - curl.curlLength, bottom, bodyTop) : bodyTop;

            // ---- 이 파트가 담당하는 세로 구간 ----
            float from, to;
            bool teeth;
            switch (part)
            {
                case Part.Flat:
                    from = bendY; to = bottom; teeth = !hasCurl;
                    break;
                case Part.Curl:
                    if (!hasCurl) return;
                    from = bodyTop; to = bendY; teeth = true;
                    break;
                default:
                    from = bodyTop; to = bottom; teeth = true;
                    break;
            }

            rowYs.Clear();
            rowYs.Add(from);
            if (from > bendY)
            {
                float step = Mathf.Max(1f, curlStep);
                for (float y = from - step; y > bendY; y -= step) rowYs.Add(y);
                if (to <= bendY) rowYs.Add(bendY);
            }
            if (rowYs[rowYs.Count - 1] > to) rowYs.Add(to);

            float edge = r.width * edgeWidth;
            float[] xs = { r.xMin, r.xMin + edge, r.xMax - edge, r.xMax };
            float[] edgeK = { 0f, 1f, 1f, 0f };

            // 앞으로 넘어올 땐 끝부분(위쪽)이 화면에 가까우니 나중에, 뒤로 넘어갈 땐 끝부분이 멀어지니 먼저 그린다.
            bool forward = curl == null || curl.towardViewer;

            // ---- 그림자: 앞으로 넘어온 날개가 아래 종이에 드리움 (뒤로 넘어가면 앞면엔 그림자 없음) ----
            if (forward && part == Part.Curl && curl.IsCurling && shadowColor.a > 0f)
            {
                float strength = Mathf.Clamp01(curl.degrees / 120f);
                Color sc = shadowColor;
                sc.a *= strength;
                if (sc.a > 0.003f) EmitStrip(vh, xs, edgeK, sc, shadowOffset * strength, true, true);
            }

            if (teeth && !forward) EmitTeeth(vh, r, top, bodyTop, edge);
            EmitStrip(vh, xs, edgeK, default, Vector2.zero, false, forward);
            if (teeth && forward) EmitTeeth(vh, r, top, bodyTop, edge);
        }

        private void EmitTeeth(VertexHelper vh, Rect r, float top, float bodyTop, float edge)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(r.width / Mathf.Max(4f, toothWidth)));
            float tw = r.width / count;
            for (int i = 0; i < count; i++)
            {
                float x0 = r.xMin + i * tw;
                float x1 = x0 + tw;
                int start = vh.currentVertCount;
                AddVertex(vh, new Vector3(x0, bodyTop), EdgeFactor(x0, r, edge), default, Vector2.zero, false);
                AddVertex(vh, new Vector3((x0 + x1) * 0.5f, top), EdgeFactor((x0 + x1) * 0.5f, r, edge), default, Vector2.zero, false);
                AddVertex(vh, new Vector3(x1, bodyTop), EdgeFactor(x1, r, edge), default, Vector2.zero, false);
                vh.AddTriangle(start, start + 1, start + 2);
            }
        }

        /// <summary>
        /// rowYs × xs 격자를 그린다. topLast = true면 아래 → 위 순서(위쪽이 나중에 = 앞에),
        /// false면 위 → 아래 순서(위쪽이 먼저 = 뒤에)로 삼각형을 넣는다.
        /// </summary>
        private void EmitStrip(VertexHelper vh, float[] xs, float[] edgeK, Color shadow, Vector2 offset, bool isShadow, bool topLast)
        {
            int baseIndex = vh.currentVertCount;
            for (int row = 0; row < rowYs.Count; row++)
                for (int col = 0; col < xs.Length; col++)
                    AddVertex(vh, new Vector3(xs[col], rowYs[row]), edgeK[col], shadow, offset, isShadow);

            int rows = rowYs.Count - 1;
            for (int i = 0; i < rows; i++)
            {
                int row = topLast ? rows - 1 - i : i;
                int a = baseIndex + row * xs.Length;
                int b = baseIndex + (row + 1) * xs.Length;
                for (int col = 0; col < xs.Length - 1; col++)
                {
                    vh.AddTriangle(a + col, a + col + 1, b + col + 1);
                    vh.AddTriangle(a + col, b + col + 1, b + col);
                }
            }
        }

        private static float EdgeFactor(float x, Rect r, float edge)
        {
            if (edge <= 0f) return 1f;
            return Mathf.Clamp01(Mathf.Min(x - r.xMin, r.xMax - x) / edge);
        }

        private void AddVertex(VertexHelper vh, Vector3 pos, float edgeFactor, Color shadow, Vector2 offset, bool isShadow)
        {
            Color c = color * Color.Lerp(edgeTint, Color.white, edgeFactor);
            c.a = color.a;
            Vector3 p = pos;
            Vector2 uv = new Vector2(pos.x / dotSpacing, pos.y / dotSpacing);

            if (curl != null && curl.IsCurling && curl.InZone(pos.y))
            {
                p = curl.Deform(pos, out float facing, out float shade);
                if (facing < 0f)
                {
                    Color back = backColor * Color.Lerp(edgeTint, Color.white, edgeFactor);
                    c = new Color(back.r * shade, back.g * shade, back.b * shade, color.a);
                    uv = Vector2.zero; // 뒷면은 도트 없이 단색
                }
                else
                {
                    c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
                }
            }

            if (isShadow)
            {
                c = shadow;
                c.a *= color.a;
                uv = Vector2.zero;
                p += (Vector3)offset;
            }

            vh.AddVert(toLocal.MultiplyPoint3x4(p), c, uv);
        }
    }
}
