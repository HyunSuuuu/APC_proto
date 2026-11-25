using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// 레벨업 배너 자물쇠의 고리 (2026-11-25). 왼쪽 다리는 짧고, 오른쪽 다리는 길어서 몸통 깊이 들어가 있다
    /// → 위로 뽑혀도 왼쪽만 빠지고 오른쪽은 몸통에 끼워진 채 연결돼 보인다.
    /// 검은 굵은 선 위에 크림색 가는 선 (끝은 둥글게). 설계 단위(뱃지 100 기준, 아래로 +y)를 <see cref="unit"/>배,
    /// 피벗 = 설계 좌표 <see cref="origin"/> (자물쇠 흔들림 축).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LevelUpShackleGraphic : MaskableGraphic
    {
        [SerializeField] float unit = 1.5f;
        [SerializeField, Tooltip("피벗의 설계 좌표")] Vector2 origin = new Vector2(50f, 62f);
        [SerializeField] float leftX = 38f, rightX = 62f, archY = 38f;
        [SerializeField, Tooltip("왼쪽(짧은) 다리 끝")] float leftBottom = 50f;
        [SerializeField, Tooltip("오른쪽(긴) 다리 끝 — 뽑혀도 몸통 안에 남을 만큼 길게")] float rightBottom = 72f;
        [SerializeField] float outerWidth = 10f, innerWidth = 5f;
        [SerializeField] Color outerColor = Color.black;
        [SerializeField] Color innerColor = new Color32(0xFE, 0xF3, 0xE4, 0xFF);

        const int ArcSegments = 20;
        readonly Vector2[] path = new Vector2[ArcSegments + 3];

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = 0;
            float r = (rightX - leftX) * 0.5f, cx = (leftX + rightX) * 0.5f;
            path[n++] = ToUI(leftX, leftBottom);
            for (int i = 0; i <= ArcSegments; i++)
            {
                float a = Mathf.PI * (1f - (float)i / ArcSegments); // 180° → 0° (위로 볼록)
                path[n++] = ToUI(cx + Mathf.Cos(a) * r, archY - Mathf.Sin(a) * r);
            }
            path[n++] = ToUI(rightX, rightBottom);

            float alpha = color.a;
            UIMesh.Stroke(vh, path, n, outerWidth * unit, new Color(outerColor.r, outerColor.g, outerColor.b, outerColor.a * alpha));
            UIMesh.Stroke(vh, path, n, innerWidth * unit, new Color(innerColor.r, innerColor.g, innerColor.b, innerColor.a * alpha));
        }

        Vector2 ToUI(float x, float y) => new Vector2((x - origin.x) * unit, (origin.y - y) * unit);
    }
}
