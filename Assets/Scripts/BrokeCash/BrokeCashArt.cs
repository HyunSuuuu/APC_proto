using System.Collections.Generic;
using UnityEngine;

namespace APC.BrokeCash
{
    /// <summary>
    /// 깨기 연출용 임시 스프라이트를 코드로 그려 캐시한다 (동전 · 지폐 · 돈다발 · 원 · 유리 조각 · 단추). 아트가 오면 교체.
    /// 크기는 월드 유닛 (동전 지름 0.35, 지폐 0.66 × 0.31, 다발 0.8 × 0.38).
    /// </summary>
    public static class BrokeCashArt
    {
        static readonly Dictionary<string, Sprite> cache = new();
        static Material spriteMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); spriteMaterial = null; }

        public static Sprite Piece(CashPieceKind kind) => kind switch
        {
            CashPieceKind.Bill => Bill,
            CashPieceKind.Bundle => Bundle,
            _ => Coin,
        };

        /// <summary>LineRenderer · 코드로 만든 렌더러용 기본 재질.</summary>
        public static Material SpriteMaterial => spriteMaterial != null ? spriteMaterial : spriteMaterial = new Material(Shader.Find("Sprites/Default"));

        static readonly Color Gold = new(1f, 0.83f, 0.3f), GoldDark = new(0.73f, 0.48f, 0.06f), GoldLine = new(0.88f, 0.64f, 0.13f);
        static readonly Color Green = new(0.35f, 0.72f, 0.44f), GreenDark = new(0.18f, 0.48f, 0.27f), GreenLight = new(0.81f, 0.93f, 0.84f);

        public static Sprite Coin => Get("coin", 64, 64, 0.35f, (x, y) =>
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
            if (d > 31f) return Color.clear;
            if (d > 28f) return GoldDark;
            return d > 19f && d < 22f ? GoldLine : Gold;
        });

        public static Sprite Bill => Get("bill", 96, 46, 0.66f, (x, y) => BillPixel(x, y, 96, 46, false));
        public static Sprite Bundle => Get("bundle", 112, 54, 0.8f, (x, y) => BillPixel(x, y, 112, 54, true));

        static Color BillPixel(int x, int y, int w, int h, bool band)
        {
            if (band && Mathf.Abs(x - w / 2f) < w * 0.09f) return new Color(0.95f, 0.83f, 0.42f);
            if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) return GreenDark;
            var inset = h * 0.14f;
            var onFrame = (x > inset && x < w - inset && y > inset && y < h - inset) && (x < inset + 2 || x > w - inset - 3 || y < inset + 2 || y > h - inset - 3);
            if (onFrame) return GreenDark;
            if (Vector2.Distance(new Vector2(x, y), new Vector2(w / 2f, h / 2f)) < h * 0.26f) return GreenLight;
            return Green;
        }

        /// <summary>1 × 1 유닛 흰 사각형 (배경 · 바닥).</summary>
        public static Sprite Square => Get("square", 4, 4, 1f, (x, y) => Color.white);

        public static Sprite Circle =>Get("circle", 64, 64, 1f, (x, y) =>
            Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) <= 31f ? Color.white : Color.clear);

        /// <summary>부드러운 원 (먼지).</summary>
        public static Sprite SoftCircle => Get("softCircle", 64, 64, 1f, (x, y) =>
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 1.6f);
        });

        public static Sprite Shard => Get("shard", 64, 64, 0.22f, (x, y) =>
        {
            var p = new Vector2(x, y);
            Vector2 a = new(4, 10), b = new(60, 4), c = new(30, 60);
            if (!InTriangle(p, a, b, c)) return Color.clear;
            var edge = Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c), DistToSegment(p, c, a));
            return edge < 2.5f ? new Color(0.3f, 0.36f, 0.42f, 0.9f) : new Color(0.88f, 0.94f, 0.98f, 0.75f);
        });

        public static Sprite Button => Get("button", 64, 64, 0.35f, (x, y) =>
        {
            var p = new Vector2(x, y);
            var d = Vector2.Distance(p, new Vector2(31.5f, 31.5f));
            if (d > 31f) return Color.clear;
            foreach (var h in new[] { new Vector2(24, 24), new Vector2(40, 24), new Vector2(24, 40), new Vector2(40, 40) })
                if (Vector2.Distance(p, h) < 4f) return new Color(0.23f, 0.14f, 0.09f);
            return d > 27f ? new Color(0.37f, 0.23f, 0.14f) : new Color(0.54f, 0.35f, 0.23f);
        });

        static Sprite Get(string key, int w, int h, float worldWidth, System.Func<int, int, Color> pixel)
        {
            if (cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = $"BrokeCash_{key}", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                pixels[y * w + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w / worldWidth);
            sprite.name = tex.name;
            cache[key] = sprite;
            return sprite;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
            var d1 = Sign(p, a, b); var d2 = Sign(p, b, c); var d3 = Sign(p, c, a);
            var neg = d1 < 0 || d2 < 0 || d3 < 0; var pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
