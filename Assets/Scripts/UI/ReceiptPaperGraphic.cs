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

        [Header("Tear Mesh")]
        [Tooltip("절취선 위아래 격자의 가로 칸 = 구멍 간격 × 이 값. 절취선 줄(톱니 · 노치)만 구멍 간격 그대로 (2026-11-25 성능) `임시`")]
        [Min(1)] public int tearGridStride = 3;

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

            // 통째 날아가기 중 평평한 종이의 몸통 투명도 (남는 조각은 그대로, 다른 그래픽은 ReceiptCurl이 CanvasRenderer 알파로)
            ReceiptTear flyTear = curl != null ? curl.Tear : null;
            bodyFade = flyTear != null && flyTear.RigidFlying && part == Part.Flat ? flyTear.FlyAlpha : 1f;

            float top = r.yMax;
            float bodyTop = top - toothHeight;
            float bottom = r.yMin;
            bool hasCurl = curl != null && curl.curlLength > 0f;
            float bendY = hasCurl ? Mathf.Clamp(top - curl.curlLength, bottom, bodyTop) : bodyTop;

            // ---- 절취선이 나타난 뒤 평평한 부분 = 몸통 + 남는 조각 (ReceiptTear, 2026-11-23) ----
            ReceiptTear tear = curl != null ? curl.Tear : null;
            if (part == Part.Flat && tear != null && tear.HasPerforation && tear.PerfY > bottom + tear.notchHeight + 2f && tear.PerfY < bendY - tear.notchHeight - 4f)
            {
                EmitTornFlat(vh, r, bendY, tear);
                return;
            }

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

        // ------------------------------------------------------------------ 절취선 (ReceiptTear)

        private readonly List<float> tearXs = new List<float>(64);
        private readonly List<int> coarseCols = new List<int>(32); // tearXs 중 성긴 줄이 쓰는 칸 (양끝 · 가장자리 경계 포함)
        private float bodyFade = 1f;
        private readonly List<float> bodyYs = new List<float>(48);
        private readonly List<float> stubYs = new List<float>(8);

        /// <summary>
        /// 평평한 부분을 절취선(perfY) 위 몸통과 아래 남는 조각으로 나눠 그린다. 절취선 줄 정점은 두 벌이라 실제로 떨어진다.
        /// 절취선 근처는 구멍 간격으로 잘게 나눠 휘어도 매끄럽고, 양옆 V 노치 · 점선(안 뜯긴 곳) · 톱니 단면(뜯긴 곳)을 같은 메시에 그린다.
        /// </summary>
        private void EmitTornFlat(VertexHelper vh, Rect r, float bendY, ReceiptTear tear)
        {
            float perf = tear.PerfY;
            float bottom = r.yMin;
            float edge = r.width * edgeWidth;
            float show = tear.PerforationAlpha;
            float notch = tear.notchDepth * show;
            float notchH = Mathf.Max(1f, tear.notchHeight);

            // 가로: 구멍 간격 + 가장자리 그라데이션 경계
            tearXs.Clear();
            float pitch = Mathf.Max(4f, tear.perforationPitch);
            int n = Mathf.Max(2, Mathf.RoundToInt(r.width / pitch));
            for (int i = 0; i <= n; i++) tearXs.Add(r.xMin + r.width * i / n);
            if (edge > 0.5f)
            {
                tearXs.Add(r.xMin + edge);
                tearXs.Add(r.xMax - edge);
            }
            tearXs.Sort();
            for (int i = tearXs.Count - 1; i > 0; i--)
                if (tearXs[i] - tearXs[i - 1] < 0.5f) tearXs.RemoveAt(i);

            // 성긴 줄: stride칸마다 + 양끝 + 가장자리 그라데이션 경계
            coarseCols.Clear();
            int stride = Mathf.Max(1, tearGridStride);
            int lastCol = tearXs.Count - 1;
            for (int i = 0; i <= lastCol; i++)
            {
                float x = tearXs[i];
                bool edgeLine = edge > 0.5f && (Mathf.Abs(x - (r.xMin + edge)) < 0.5f || Mathf.Abs(x - (r.xMax - edge)) < 0.5f);
                if (i == 0 || i == lastCol || edgeLine || i % stride == 0) coarseCols.Add(i);
            }

            // 세로 (위 → 아래): 위쪽은 성기게, 절취선 위 bendHeight 구간은 촘촘히, 노치 줄, 절취선
            bodyYs.Clear();
            float fineTop = Mathf.Min(bendY, perf + tear.bendHeight * 1.2f);
            bodyYs.Add(bendY);
            for (float y = bendY - 48f; y > fineTop + 4f; y -= 48f) bodyYs.Add(y);
            if (fineTop < bendY) bodyYs.Add(fineTop);
            for (float y = fineTop - 10f; y > perf + notchH + 2f; y -= 10f) bodyYs.Add(y);
            bodyYs.Add(perf + notchH);
            bodyYs.Add(perf);

            stubYs.Clear();
            stubYs.Add(perf);
            if (perf - notchH > bottom + 1f) stubYs.Add(perf - notchH);
            stubYs.Add(bottom);

            float jag = tear.jagHeight;
            // 남는 조각의 위 끝 톱니는 몸통과 반대 방향 (-jag)
            EmitTearGrid(vh, r, edge, stubYs, perf, notch, -jag, tear, true);
            EmitTearGrid(vh, r, edge, bodyYs, perf, notch, jag, tear, false);

            // 뜯긴 단면 테두리 (몸통 아래 끝)
            Color edgeCol = tear.tornEdgeColor;
            for (int c = 0; c < tearXs.Count - 1; c++)
            {
                float u0 = (tearXs[c] - r.xMin) / r.width;
                float u1 = (tearXs[c + 1] - r.xMin) / r.width;
                float b0 = tear.TornBlend(u0), b1 = tear.TornBlend(u1);
                if (b0 + b1 < 0.02f) continue;
                Vector3 p0 = PerfPoint(r, c, perf, notch, jag, tear);
                Vector3 p1 = PerfPoint(r, c + 1, perf, notch, jag, tear);
                Vector3 up = new Vector3(0f, tear.tornEdgeWidth);
                Color ca = edgeCol, cb = edgeCol;
                ca.a *= b0;
                cb.a *= b1;
                AddQuad(vh, p0, p1, p1 + up, p0 + up, ca, cb, cb, ca, false);
            }

            // 점선 (아직 안 뜯긴 곳만, 남는 조각 쪽)
            if (show > 0.01f)
            {
                Color dc = tear.dashColor;
                dc.a *= show;
                float half = tear.dashThickness * 0.5f;
                float clipX = tear.TearU > 0f ? r.xMin + r.width * (tear.TearU + 0.02f) : float.MinValue;
                float step = Mathf.Max(1f, tear.dashLength + tear.dashGap);
                for (float x = r.xMin + notch + 3f; x < r.xMax - notch - 3f; x += step)
                {
                    float x0 = Mathf.Max(x, clipX);
                    float x1 = Mathf.Min(x + tear.dashLength, r.xMax - notch - 3f);
                    if (x1 - x0 < 0.5f) continue;
                    AddQuad(vh,
                        new Vector3(x0, perf - half), new Vector3(x1, perf - half),
                        new Vector3(x1, perf + half), new Vector3(x0, perf + half),
                        dc, dc, dc, dc, true);
                }
            }
        }

        /// <summary>절취선 줄의 c번째 정점 위치 (양끝 = 노치로 안쪽, 뜯긴 곳 = 톱니).</summary>
        private Vector3 PerfPoint(Rect r, int c, float perf, float notch, float jag, ReceiptTear tear)
        {
            float x = tearXs[c];
            int last = tearXs.Count - 1;
            if (c == 0) return new Vector3(x + notch, perf);
            if (c == last) return new Vector3(x - notch, perf);
            float u = (x - r.xMin) / r.width;
            float zig = (c % 2 == 0 ? 1f : -0.6f) * jag * tear.TornBlend(u);
            return new Vector3(x, perf + zig);
        }

        /// <summary>
        /// ys 줄마다 정점을 놓고 이웃 줄끼리 잇는다. 절취선 줄(톱니 · 노치)만 tearXs 전부, 나머지 줄은 coarseCols만 쓴다 (2026-11-25 성능).
        /// 촘촘한 줄 ↔ 성긴 줄 사이는 성긴 칸마다 부채꼴로 이어 틈이 없게 한다.
        /// </summary>
        private void EmitTearGrid(VertexHelper vh, Rect r, float edge, List<float> ys, float perf, float notch, float jag, ReceiptTear tear, bool stub)
        {
            int prevBase = -1;
            bool prevFine = false;
            for (int row = 0; row < ys.Count; row++)
            {
                float y = ys[row];
                bool perfRow = Mathf.Abs(y - perf) < 0.01f;
                int rowBase = vh.currentVertCount;
                if (perfRow)
                {
                    for (int c = 0; c < tearXs.Count; c++)
                        AddVertex(vh, PerfPoint(r, c, perf, notch, jag, tear), EdgeFactor(tearXs[c], r, edge), default, Vector2.zero, false, stub);
                }
                else
                {
                    for (int j = 0; j < coarseCols.Count; j++)
                    {
                        float x = tearXs[coarseCols[j]];
                        AddVertex(vh, new Vector3(x, y), EdgeFactor(x, r, edge), default, Vector2.zero, false, stub);
                    }
                }
                if (prevBase >= 0) ConnectRows(vh, prevBase, prevFine, rowBase, perfRow);
                prevBase = rowBase;
                prevFine = perfRow;
            }
        }

        private void ConnectRows(VertexHelper vh, int a, bool aFine, int b, bool bFine)
        {
            if (aFine == bFine)
            {
                int n = aFine ? tearXs.Count : coarseCols.Count;
                for (int c = 0; c < n - 1; c++)
                {
                    vh.AddTriangle(a + c, a + c + 1, b + c + 1);
                    vh.AddTriangle(a + c, b + c + 1, b + c);
                }
                return;
            }
            int coarse = aFine ? b : a;
            int fine = aFine ? a : b;
            for (int j = 0; j < coarseCols.Count - 1; j++)
            {
                int i0 = coarseCols[j], i1 = coarseCols[j + 1];
                for (int i = i0; i < i1; i++) vh.AddTriangle(coarse + j, fine + i, fine + i + 1);
                vh.AddTriangle(coarse + j, fine + i1, coarse + j + 1);
            }
        }

        /// <summary>단색 사각형 (점선 · 단면 테두리). 종이와 같은 변형을 거친다.</summary>
        private void AddQuad(VertexHelper vh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd, bool stub)
        {
            int start = vh.currentVertCount;
            AddRawVertex(vh, a, ca, stub);
            AddRawVertex(vh, b, cb, stub);
            AddRawVertex(vh, c, cc, stub);
            AddRawVertex(vh, d, cd, stub);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddRawVertex(VertexHelper vh, Vector3 pos, Color c, bool stub)
        {
            c.a *= color.a;
            Vector3 p = pos;
            ReceiptTear tear = curl != null && curl.TearDeforming ? curl.Tear : null;
            if (tear != null)
            {
                p = tear.Deform(p, stub, out float shade, out float alpha);
                c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a * alpha * (stub ? 1f : bodyFade));
            }
            vh.AddVert(toLocal.MultiplyPoint3x4(p), c, Vector2.zero); // uv 0 = 도트 없는 흰 픽셀
        }

        private void AddVertex(VertexHelper vh, Vector3 pos, float edgeFactor, Color shadow, Vector2 offset, bool isShadow, bool stub = false)
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

            // 말림 다음에 절취선 뜯기 (남는 조각 여부는 호출한 쪽이 정함)
            ReceiptTear tear = curl != null && curl.TearDeforming ? curl.Tear : null;
            if (tear != null && !isShadow)
            {
                p = tear.Deform(p, stub, out float tearShade, out float tearAlpha);
                c = new Color(Mathf.Min(1f, c.r * tearShade), Mathf.Min(1f, c.g * tearShade), Mathf.Min(1f, c.b * tearShade), c.a * tearAlpha * (stub ? 1f : bodyFade));
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
