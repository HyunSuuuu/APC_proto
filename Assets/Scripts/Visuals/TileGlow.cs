using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 바닥 사각 타일의 테두리 글로우 (2026-11-19, 현금 존). 테두리에서 안쪽 `inner` · 바깥쪽 `outer`만큼 퍼지는 링 메시를
    /// 만들어 자식으로 붙인다. 정점 색 알파 = 테두리 1, 양 끝 0 (셰이더 APC/ZoneGlow가 제곱해 부드럽게). 반짝임 · 크기 변화 없음.
    /// </summary>
    public static class TileGlow
    {
        /// <summary>parent 로컬 원점 중심, XZ 평면 size 타일의 테두리 글로우를 만들고 꺼 둔 채 돌려준다.</summary>
        public static GameObject Create(Transform parent, Vector2 size, float inner, float outer, float height, Material material)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = BuildRing(size * 0.5f, inner, outer);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        /// <summary>안 · 테두리 · 밖 세 사각형(꼭짓점 4개씩)을 잇는 링. 모서리는 대각선으로 이어진다.</summary>
        static Mesh BuildRing(Vector2 half, float inner, float outer)
        {
            var offsets = new[] { -Mathf.Min(inner, Mathf.Min(half.x, half.y)), 0f, outer };
            var alphas = new[] { 0f, 1f, 0f };
            var corners = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };

            var vertices = new Vector3[12];
            var colors = new Color[12];
            for (var ring = 0; ring < 3; ring++)
            {
                for (var c = 0; c < 4; c++)
                {
                    var i = ring * 4 + c;
                    vertices[i] = new Vector3(corners[c].x * (half.x + offsets[ring]), 0f, corners[c].y * (half.y + offsets[ring]));
                    colors[i] = new Color(1f, 1f, 1f, alphas[ring]);
                }
            }

            var triangles = new int[2 * 4 * 6];
            var t = 0;
            for (var band = 0; band < 2; band++)
            {
                for (var c = 0; c < 4; c++)
                {
                    int a = band * 4 + c, b = band * 4 + (c + 1) % 4, d = a + 4, e = b + 4;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = e;
                    triangles[t++] = a; triangles[t++] = e; triangles[t++] = b;
                }
            }

            var mesh = new Mesh { name = "TileGlow" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
