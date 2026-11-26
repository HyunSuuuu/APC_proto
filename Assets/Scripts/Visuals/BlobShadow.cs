using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 캐릭터 발밑 원형 그림자 (2026-11-26). 메인 라이트 그림자를 끈 대신 플레이어 · 알바에 자식 쿼드로 붙인다.
    /// 재질 `Resources/M_BlobShadow`(셰이더 `APC/BlobShadow`)는 SRP Batcher로 그린다 — shortcut: 그림자 하나 = 드로 하나(GPU 인스턴싱 경로가 이 프로젝트에서 안 그려짐), 드로가 문제면 인스턴싱 원인부터 찾기.
    /// </summary>
    public static class BlobShadow
    {
        const string ChildName = "BlobShadow";
        const float Lift = 0.14f; // 발 위치(y 0)보다 바닥 타일 윗면(0.117)이 높다 — 그 위로

        static Material material;
        static Mesh quad;

        /// <summary>그림자를 붙이거나(이미 있으면) 지름(m)만 바꾼다.</summary>
        public static void Attach(GameObject target, float diameter)
        {
            var shadow = target.transform.Find(ChildName);
            if (shadow == null)
            {
                if (material == null) material = Resources.Load<Material>("M_BlobShadow");
                if (quad == null) quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                if (material == null || quad == null) return;

                var go = new GameObject(ChildName);
                go.layer = target.layer;
                shadow = go.transform;
                shadow.SetParent(target.transform, false);
                shadow.localPosition = new Vector3(0f, Lift, 0f);
                shadow.localRotation = Quaternion.Euler(90f, 0f, 0f); // 쿼드(XY)를 바닥(XZ)에 눕힘
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            shadow.localScale = Vector3.one * diameter;
        }
    }
}
