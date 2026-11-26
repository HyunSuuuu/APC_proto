using System.Collections.Generic;
using APC.Core;
using APC.Site;
using R3;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace APC.Visuals
{
    /// <summary>
    /// 구운 그림자 (2026-11-27). 메인 라이트 그림자를 끈 대신, 부지가 바뀔 때만(시작 · 해금 · 모듈 이동 · 레벨업 = 공사 울타리, 그 뒤 2초 동안 0.1초마다)
    /// 실제 빛 방향의 직교 카메라로 정적 물체 실루엣을 한 번 찍어 마스크로 두고, 바닥 높이의 판 하나(`APC/BakedShadow`)가
    /// 그 마스크를 투영해 어둡게 한다 — 프레임마다 그림자를 계산하지 않는다 (판 드로 1).
    /// 찍는 것 = 그림자를 드리우던(`shadowCastingMode` ≠ Off) 켜진 MeshRenderer 중 높이 0.15m 이상 (벽 · 기둥 · 선반 · 소품).
    /// 캐릭터(스킨) · 상자 · 트럭(그림자 끔) · 바닥 타일은 빠진다. 그림자는 바닥에만 생긴다 (물체 옆면 · 위에는 없음).
    /// SiteGrid가 붙인다.
    /// </summary>
    public sealed class BakedShadows : MonoBehaviour
    {
        const float RebakeWindow = 2f;   // 변화 뒤 이 시간 동안 다시 굽는다 — 팝 연출(0.4초) · 차례로 튀어나오는 선반 · 시작 연출을 따라감
        const float RebakeInterval = 0.1f;
        const float FloorHeight = 0.13f; // 바닥 타일 윗면(0.117) 위
        const float MinHeight = 0.15f;   // 이보다 납작한 것(바닥 · 발판 · 글자)은 찍지 않는다
        const float TexelSize = 0.06f;   // 마스크 한 칸 (m)
        const int MaxSize = 2048;

        static readonly int MaskId = Shader.PropertyToID("_BakedShadowMask");
        static readonly int MatrixId = Shader.PropertyToID("_BakedShadowVP");
        static readonly int CutoutCountId = Shader.PropertyToID("_WallCutoutCount");

        readonly List<Renderer> hidden = new();
        Camera bakeCamera;
        RenderTexture mask;
        Transform overlay;
        float nextBake = -1f;
        float rebakeUntil = -1f;

        /// <summary>마지막으로 구운 시간 (ms, 측정용).</summary>
        public static float LastBakeMs { get; private set; }

        void Start()
        {
            var grid = SiteGrid.Instance;
            if (grid != null)
            {
                grid.OnUnlocked.Subscribe(_ => Schedule()).AddTo(this);
                grid.OnMoved.Subscribe(_ => Schedule()).AddTo(this);
            }
            if (GameManager.Instance != null) GameManager.Instance.Level.Level.Subscribe(_ => Schedule()).AddTo(this);
            Schedule();
        }

        /// <summary>바로 굽고, RebakeWindow 동안 RebakeInterval마다 다시 굽는다 (2026-11-27 — 전엔 2초 뒤 한 번이라 그림자가 늦게 생겼다).</summary>
        public void Schedule()
        {
            var now = Time.unscaledTime; // 정산 팝업 등 시간 멈춤에도 굽는다
            rebakeUntil = now + RebakeWindow;
            nextBake = now;
        }

        void LateUpdate()
        {
            var now = Time.unscaledTime;
            if (nextBake < 0f || now < nextBake) return;
            nextBake = now < rebakeUntil ? now + RebakeInterval : -1f; // 창이 끝난 뒤 한 번 더 구우면 멈춤
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Bake();
            LastBakeMs = (float)watch.Elapsed.TotalMilliseconds;
        }

        void Bake()
        {
            var sun = Sun();
            var material = Resources.Load<Material>("M_BakedShadow");
            if (sun == null || material == null) return;

            // 찍을 것만 남기고 나머지는 잠깐 끈다
            hidden.Clear();
            var bounds = new Bounds();
            var any = false;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                // 캐릭터(CarryStack)에 붙은 것 = 도구 · 지게차 · 든 상자는 움직이므로 빼고 블롭 그림자로 (2026-11-27)
                var caster = r is MeshRenderer && r.shadowCastingMode != ShadowCastingMode.Off && r.bounds.size.y >= MinHeight
                             && r.GetComponentInParent<Logistics.CarryStack>() == null;
                if (!caster)
                {
                    r.forceRenderingOff = true;
                    hidden.Add(r);
                    continue;
                }
                if (any) bounds.Encapsulate(r.bounds);
                else bounds = r.bounds;
                any = true;
            }
            if (any) Render(sun, bounds, material);
            foreach (var r in hidden)
                if (r != null) r.forceRenderingOff = false;
            hidden.Clear();
        }

        void Render(Light sun, Bounds casters, Material material)
        {
            // 빛 공간에서 물체 + 바닥에 떨어지는 그림자까지 덮는 상자
            var rotation = sun.transform.rotation;
            var toLight = Quaternion.Inverse(rotation);
            var forward = sun.transform.forward;
            var ground = casters;
            var reach = forward.y < -0.05f ? casters.max.y / -forward.y : 0f; // 가장 높은 꼭대기의 그림자가 닿는 수평 거리
            ground.Encapsulate(new Vector3(casters.center.x + forward.x * reach, FloorHeight, casters.center.z + forward.z * reach));
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? ground.min.x : ground.max.x, (i & 2) == 0 ? ground.min.y : ground.max.y, (i & 4) == 0 ? ground.min.z : ground.max.z);
                var p = toLight * corner;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var size = max - min;
            var width = Mathf.Clamp(Mathf.CeilToInt(size.x / TexelSize), 64, MaxSize);
            var height = Mathf.Clamp(Mathf.CeilToInt(size.y / TexelSize), 64, MaxSize);

            if (mask == null || mask.width != width || mask.height != height)
            {
                if (mask != null) mask.Release();
                mask = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = "BakedShadowMask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }
            var cam = Camera();
            cam.transform.SetPositionAndRotation(rotation * new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, min.z - 1f), rotation);
            cam.orthographicSize = size.y * 0.5f;
            cam.aspect = size.x / Mathf.Max(size.y, 0.01f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = size.z + 2f;
            cam.targetTexture = mask;

            Shader.SetGlobalFloat(CutoutCountId, 0f); // 벽 구멍 없이 찍는다 (WallCutout이 다음 프레임에 다시 넣음)
            cam.Render();
            cam.targetTexture = null;

            Shader.SetGlobalTexture(MaskId, mask);
            Shader.SetGlobalMatrix(MatrixId, cam.projectionMatrix * cam.worldToCameraMatrix);
            Overlay(material, ground);
        }

        Camera Camera()
        {
            if (bakeCamera != null) return bakeCamera;
            var go = new GameObject("BakedShadowCamera");
            go.transform.SetParent(transform, false);
            bakeCamera = go.AddComponent<Camera>();
            bakeCamera.enabled = false;
            bakeCamera.orthographic = true;
            bakeCamera.clearFlags = CameraClearFlags.SolidColor;
            bakeCamera.backgroundColor = Color.clear;
            bakeCamera.allowHDR = false;
            bakeCamera.allowMSAA = false;
            var data = bakeCamera.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            return bakeCamera;
        }

        /// <summary>바닥 높이의 판 하나 (그림자가 떨어지는 범위).</summary>
        void Overlay(Material material, Bounds ground)
        {
            if (overlay == null)
            {
                var go = new GameObject("BakedShadowOverlay");
                overlay = go.transform;
                go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            overlay.SetPositionAndRotation(new Vector3(ground.center.x, FloorHeight, ground.center.z), Quaternion.Euler(90f, 0f, 0f));
            overlay.localScale = new Vector3(ground.size.x, ground.size.z, 1f);
        }

        static Light Sun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.isActiveAndEnabled) return light;
            return null;
        }

        void OnDestroy()
        {
            if (mask != null) mask.Release();
            if (overlay != null) Destroy(overlay.gameObject);
        }
    }
}
