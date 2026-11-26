using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 레벨업 배너의 별 · 동그라미 파티클 (2026-11-25). 오브젝트를 파티클마다 만들지 않고 **Graphic 하나가 메시 하나로 전부 그린다**.
    ///  - 파티클 값(방향 · 거리 · 회전 · 지연 · 크기 · 색)은 배열에 미리 담아 두고 <see cref="Restart"/>에서 다시 섞기만 한다 (할당 없음).
    ///  - 터지기 전(<see cref="Time"/> &lt; 0)과 다 사라진 뒤엔 정점이 0개 → 그리지 않는다. SetActive · Instantiate 없음.
    ///  - 흰 기본 텍스처라 배너의 다른 도형(<see cref="UIShape"/> 등)과 한 드로우콜로 묶인다.
    /// 피벗 = 터지는 중심(자물쇠 몸통). 움직임은 키프레임 5개를 보간 (0 → 0.06 → 0.35 → 0.7 → 1, 전체 OutQuart).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LevelUpBurst : MaskableGraphic
    {
        [SerializeField, Min(1)] int count = 26;
        [SerializeField, Tooltip("이 번호부터는 별 대신 동그라미")] int dotsFrom = 20;
        [SerializeField, Tooltip("크기 · 거리 배율 (샘플 기준 px → UI px)")] float scale = 1.34f;
        [SerializeField] Color starOutline = new Color32(0x4A, 0x2A, 0x00, 0xFF);
        [SerializeField] Color[] palette =
        {
            Color.white, new Color32(0xFF, 0xD5, 0x4A, 0xFF), new Color32(0xF7, 0xA5, 0x31, 0xFF),
            Color.white, new Color32(0xFF, 0xE5, 0x8A, 0xFF),
        };

        float time = -1f;
        Vector2[] start, end;
        float[] spin, delay, duration, size;
        bool seeded;

        static readonly Vector2[] pts = new Vector2[10];

        /// <summary>마지막 파티클이 사라지는 시각 (초).</summary>
        public float TotalDuration => 0.06f + 1.25f;

        /// <summary>터진 뒤 경과 시간(초). 음수면 숨김.</summary>
        public float Time
        {
            get => time;
            set
            {
                if (Mathf.Approximately(time, value)) return;
                bool wasVisible = time >= 0f && time <= TotalDuration;
                time = value;
                bool visible = time >= 0f && time <= TotalDuration;
                if (visible || wasVisible) SetVerticesDirty();
            }
        }

        /// <summary>파티클 값을 다시 섞고 숨긴다 (배열은 처음 한 번만 만든다).</summary>
        public void Restart()
        {
            Seed();
            for (int i = 0; i < count; i++)
            {
                bool dot = i >= dotsFrom;
                size[i] = dot ? Random.Range(9f, 14f) : Random.Range(13f, 24f);
                float ang = i / (float)count * Mathf.PI * 2f + Random.Range(0f, 0.4f);
                float r = Random.Range(85f, 165f);
                start[i] = new Vector2(Mathf.Cos(ang) * 36f, Mathf.Sin(ang) * 36f * 0.6f);
                end[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 0.85f);
                spin[i] = (Random.value < 0.5f ? -1f : 1f) * Random.Range(120f, 360f);
                delay[i] = Random.Range(0f, 0.06f);
                duration[i] = Random.Range(0.9f, 1.25f);
            }
            time = -1f;
            SetVerticesDirty();
        }

        void Seed()
        {
            if (seeded && start.Length == count) return;
            start = new Vector2[count];
            end = new Vector2[count];
            spin = new float[count];
            delay = new float[count];
            duration = new float[count];
            size = new float[count];
            seeded = true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!seeded || time < 0f) return;
            for (int i = 0; i < count; i++)
            {
                float u = (time - delay[i]) / duration[i];
                if (u <= 0f || u >= 1f) continue;
                float e = 1f - Mathf.Pow(1f - u, 4f);
                Sample(i, e, out Vector2 pos, out float scl, out float rot, out float alpha);
                alpha *= color.a;
                if (alpha <= 0.01f) continue;
                Color baseColor = palette.Length > 0 ? palette[i % palette.Length] : Color.white;
                if (i >= dotsFrom) DrawDot(vh, pos, size[i] * scl * scale, baseColor == Color.white ? Color.white : (Color)new Color32(0xF7, 0xA5, 0x31, 0xFF), alpha);
                else DrawStar(vh, pos, size[i] * scl * scale, rot, baseColor, alpha);
            }
        }

        // 샘플의 키프레임 (샘플은 아래로 +y → UI는 y 반전)
        void Sample(int i, float e, out Vector2 pos, out float scl, out float rot, out float alpha)
        {
            Vector2 s0 = start[i], s1 = end[i];
            Vector2 k0 = s0, k1 = s0 * 1.4f, k2 = s1 * 0.8f, k3 = new Vector2(s1.x, s1.y + 10f), k4 = new Vector2(s1.x * 1.03f, s1.y + 26f);
            float r = spin[i];
            if (e < 0.06f) { float t = e / 0.06f; pos = Vector2.Lerp(k0, k1, t); scl = Mathf.Lerp(0.2f, 0.5f, t); rot = Mathf.Lerp(0f, r * 0.1f, t); alpha = t; }
            else if (e < 0.35f) { float t = (e - 0.06f) / 0.29f; pos = Vector2.Lerp(k1, k2, t); scl = Mathf.Lerp(0.5f, 1.1f, t); rot = Mathf.Lerp(r * 0.1f, r * 0.6f, t); alpha = 1f; }
            else if (e < 0.7f) { float t = (e - 0.35f) / 0.35f; pos = Vector2.Lerp(k2, k3, t); scl = Mathf.Lerp(1.1f, 1f, t); rot = Mathf.Lerp(r * 0.6f, r * 0.85f, t); alpha = Mathf.Lerp(1f, 0.9f, t); }
            else { float t = (e - 0.7f) / 0.3f; pos = Vector2.Lerp(k3, k4, t); scl = Mathf.Lerp(1f, 0.6f, t); rot = Mathf.Lerp(r * 0.85f, r, t); alpha = Mathf.Lerp(0.9f, 0f, t); }
            pos = new Vector2(pos.x * scale, -pos.y * scale);
        }

        void DrawStar(VertexHelper vh, Vector2 c, float px, float rotDeg, Color fill, float alpha)
        {
            float k = px / 24f; // 샘플 별 viewBox 24
            float rad = -rotDeg * Mathf.Deg2Rad; // 샘플은 시계 방향 +
            Star(c, 10f * k + 2.6f * k, 4.2f * k + 1.4f * k, rad);
            UIMesh.Fan(vh, c, pts, 10, new Color(starOutline.r, starOutline.g, starOutline.b, starOutline.a * alpha));
            Star(c, 10f * k, 4.2f * k, rad);
            UIMesh.Fan(vh, c, pts, 10, new Color(fill.r, fill.g, fill.b, fill.a * alpha));
        }

        static void Star(Vector2 c, float outer, float inner, float rot)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = rot + Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i & 1) == 0 ? outer : inner;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
        }

        static void DrawDot(VertexHelper vh, Vector2 c, float px, Color col, float alpha)
        {
            float k = px / 12f; // 샘플 동그라미 viewBox 12 (반지름 4, 선 1.8)
            UIMesh.Annulus(vh, c, (4f + 0.9f) * k, (4f - 0.9f) * k, new Color(col.r, col.g, col.b, col.a * alpha), 20);
        }
    }
}
