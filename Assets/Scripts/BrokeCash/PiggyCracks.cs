using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace APC.BrokeCash
{
    /// <summary>
    /// 저금통 우측 하단 구석의 금 · 구멍 (공용). 이 오브젝트 위치 = 금이 시작되는 점 = 돈이 나오는 구멍.
    /// 금은 LineRenderer(흰 번짐 + 어두운 선) 11줄을 seed로 뽑아 Crack 때 한 번에 퍼뜨리고, Break 때 들쭉날쭉한 구멍 테두리를 켠다.
    /// 로컬 좌표로 그리므로 부모(DOTween 몸통 · 스켈레톤 금 뼈)를 따라 움직인다.
    /// </summary>
    public sealed class PiggyCracks : MonoBehaviour
    {
        [SerializeField, Tooltip("금 · 구멍 정렬 순서 (유리 이미지보다 위)")] int sortingOrder = 110;
        [SerializeField] Color lineColor = new(0.22f, 0.25f, 0.31f, 0.9f);
        [SerializeField] Color glowColor = new(1f, 1f, 1f, 0.95f);

        const int CrackCount = 11;
        const int Segments = 4;

        readonly List<Vector3[]> paths = new();
        readonly List<LineRenderer> cores = new();
        readonly List<LineRenderer> glows = new();
        LineRenderer holeCore;
        LineRenderer holeGlow;
        SpriteRenderer spark;
        bool built;

        void Build()
        {
            if (built) return;
            built = true;
            for (var i = 0; i < CrackCount; i++)
            {
                glows.Add(Line($"CrackGlow{i}", 0.045f, glowColor, sortingOrder));
                cores.Add(Line($"Crack{i}", 0.02f, lineColor, sortingOrder + 1));
            }
            holeGlow = Line("HoleGlow", 0.03f, glowColor, sortingOrder + 2);
            holeCore = Line("Hole", 0.025f, lineColor, sortingOrder + 3);
            holeGlow.loop = holeCore.loop = true;
            var sparkGo = new GameObject("Spark", typeof(SpriteRenderer));
            sparkGo.transform.SetParent(transform, false);
            spark = sparkGo.GetComponent<SpriteRenderer>();
            spark.sprite = BrokeCashArt.SoftCircle;
            spark.sortingOrder = sortingOrder + 4;
            sparkGo.transform.localScale = Vector3.one * 0.5f;
        }

        LineRenderer Line(string name, float width, Color color, int order)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(transform, false);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sharedMaterial = BrokeCashArt.SpriteMaterial;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.positionCount = 0;
            return line;
        }

        /// <summary>새 모양을 뽑고 모두 숨긴다.</summary>
        public void Prepare(int seed)
        {
            Build();
            DOTween.Kill(this);
            var rng = new System.Random(seed * 31 + 7);
            float R() => (float)rng.NextDouble();
            paths.Clear();
            for (var k = 0; k < CrackCount; k++)
            {
                // 대부분은 몸통 안쪽(왼쪽 위)으로, 몇 줄은 짧게 바깥쪽으로
                var angle = (k < 8 ? Mathf.Lerp(70f, 210f, R()) : Mathf.Lerp(-60f, 40f, R())) * Mathf.Deg2Rad;
                var length = k < 8 ? Mathf.Lerp(0.7f, 1.9f, R()) : Mathf.Lerp(0.25f, 0.55f, R());
                var pts = new Vector3[Segments + 1];
                var p = Vector3.zero;
                for (var s = 1; s <= Segments; s++)
                {
                    var a = angle + (R() - 0.5f) * 0.8f;
                    p += new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * (length / Segments);
                    pts[s] = p;
                }
                paths.Add(pts);
            }
            var hole = new Vector3[12];
            for (var i = 0; i < hole.Length; i++)
            {
                var a = i / (float)hole.Length * Mathf.PI * 2f;
                var r = Mathf.Lerp(0.26f, 0.52f, R());
                hole[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.85f);
            }
            holeCore.positionCount = holeGlow.positionCount = hole.Length;
            holeCore.SetPositions(hole);
            for (var i = 0; i < hole.Length; i++) hole[i] += new Vector3(0.02f, -0.02f);
            holeGlow.SetPositions(hole);
            SetGrowth(0f);
            holeCore.enabled = holeGlow.enabled = false;
            spark.enabled = false;
        }

        /// <summary>쩌적 — 금이 grow초 만에 한 번에 퍼지고 구멍 자리가 반짝인다.</summary>
        public void Crack(float grow)
        {
            spark.enabled = true;
            spark.color = Color.white;
            DOVirtual.Float(0f, 1f, grow, SetGrowth).SetEase(Ease.OutQuad).SetUpdate(true).SetId(this).SetLink(gameObject);
            spark.transform.localScale = Vector3.one * 0.35f;
            spark.transform.DOScale(0.6f, 0.08f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetId(this).SetLink(gameObject);
        }

        /// <summary>깨짐 — 구멍 테두리를 켜고 반짝임을 끈다.</summary>
        public void Break()
        {
            DOTween.Kill(this);
            SetGrowth(1f);
            spark.enabled = false;
            holeCore.enabled = holeGlow.enabled = true;
        }

        void SetGrowth(float g)
        {
            for (var k = 0; k < paths.Count; k++)
            {
                var pts = paths[k];
                if (g <= 0f) { cores[k].positionCount = glows[k].positionCount = 0; continue; }
                var upto = g * Segments;
                var whole = Mathf.FloorToInt(upto);
                var count = Mathf.Min(Segments, whole) + 1 + (whole < Segments ? 1 : 0);
                cores[k].positionCount = glows[k].positionCount = count;
                for (var j = 0; j <= Mathf.Min(Segments, whole); j++) { cores[k].SetPosition(j, pts[j]); glows[k].SetPosition(j, pts[j] + new Vector3(0.012f, -0.012f)); }
                if (whole < Segments)
                {
                    var tip = Vector3.Lerp(pts[whole], pts[whole + 1], upto - whole);
                    cores[k].SetPosition(count - 1, tip); glows[k].SetPosition(count - 1, tip + new Vector3(0.012f, -0.012f));
                }
            }
        }
    }
}
