using System.Collections.Generic;
using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 고정 물체 그림자 판 (2026-11-27). 메인 라이트 그림자를 끈 뒤 선반 · 소품 · 기둥 아래가 비어 보여서, 모듈이 켜진 뒤 한 번
    /// 그림자를 드리우던 렌더러마다 바닥에 부드러운 사각 그림자 판을 깔고 모듈당 메시 하나로 합친다 (드로 1).
    /// 모듈은 실행 중에 생기고 재배치로 움직이므로 라이트맵 대신 이 방식 — 판은 모듈 자식이라 같이 움직인다.
    /// 재질 `Resources/M_StaticShadow`(셰이더 `APC/BlobShadow`, `_Rect` 1).
    /// </summary>
    public sealed class StaticShadows : MonoBehaviour
    {
        const string ChildName = "StaticShadows";
        const float BuildDelay = 2f;     // 팝 연출(FacilityPop)이 끝난 뒤 크기로 잰다
        const float Lift = 0.13f;        // 바닥 타일 윗면(0.117) 위
        const float MinHeight = 0.25f;   // 이보다 낮은 것(바닥 · 글자 · 납작한 판)은 건너뜀
        const float ShiftPerHeight = 0.35f;
        const float MaxShift = 1.2f;
        const float Margin = 0.1f;

        static Material material;
        static Vector3 lightForward;
        static bool lightFound;

        bool built;

        void OnEnable()
        {
            if (!built) Invoke(nameof(Build), BuildDelay);
        }

        void Build()
        {
            if (built || !isActiveAndEnabled) return;
            built = true;
            if (material == null) material = Resources.Load<Material>("M_StaticShadow");
            if (material == null) return;

            // 그림자는 빛이 가는 방향(수평 성분)으로 밀린다
            var forward = LightForward();
            var shiftDir = new Vector3(forward.x, 0f, forward.z);
            shiftDir = shiftDir.sqrMagnitude > 1e-4f ? shiftDir.normalized : Vector3.zero;

            var floorY = transform.position.y + Lift;
            var toLocal = transform.worldToLocalMatrix;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
                var b = r.bounds;
                if (b.size.y < MinHeight || b.size.x * b.size.z < 0.04f) continue;
                var shift = shiftDir * Mathf.Min((b.max.y - transform.position.y) * ShiftPerHeight, MaxShift);
                var center = new Vector3(b.center.x + shift.x * 0.5f, floorY, b.center.z + shift.z * 0.5f);
                var halfX = b.extents.x + Mathf.Abs(shift.x) * 0.5f + Margin;
                var halfZ = b.extents.z + Mathf.Abs(shift.z) * 0.5f + Margin;

                var start = vertices.Count;
                vertices.Add(toLocal.MultiplyPoint3x4(center + new Vector3(-halfX, 0f, -halfZ)));
                vertices.Add(toLocal.MultiplyPoint3x4(center + new Vector3(-halfX, 0f, halfZ)));
                vertices.Add(toLocal.MultiplyPoint3x4(center + new Vector3(halfX, 0f, halfZ)));
                vertices.Add(toLocal.MultiplyPoint3x4(center + new Vector3(halfX, 0f, -halfZ)));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            if (vertices.Count == 0) return;

            var mesh = new Mesh { name = ChildName };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Vector3 LightForward()
        {
            if (lightFound) return lightForward;
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (sun == null) return Vector3.down; // 다음에 다시 찾는다
            lightFound = true;
            lightForward = sun.transform.forward;
            return lightForward;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => lightFound = false;
    }
}
