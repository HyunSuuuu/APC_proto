using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace APC.Visuals
{
    /// <summary>
    /// 외곽선 셰이더(APC/OutlineStencil)의 LightMode 패스 3개를 매 카메라에 넣는다 — 렌더러 에셋(Mobile · PC_Renderer)에
    /// Render Objects 피처를 추가하는 것과 같은 일을 코드로 한다 (에셋 수정 없음).
    /// 렌더러 에셋에 같은 LightMode의 Render Objects 피처를 따로 넣으면 두 번 그려지니 넣지 않는다.
    /// Play 중에만 동작한다 (편집 화면에는 외곽선 없음).
    /// </summary>
    static class OutlinePasses
    {
        static readonly RenderObjectsPass Mask = Pass("OutlineMask", RenderPassEvent.AfterRenderingOpaques);
        static readonly RenderObjectsPass Hull = Pass("OutlineHull", RenderPassEvent.BeforeRenderingSkybox);
        static readonly RenderObjectsPass Outer = Pass("OutlineOuter", RenderPassEvent.BeforeRenderingSkybox); // Hull 다음 (같은 이벤트는 넣은 순서)

        static RenderObjectsPass Pass(string lightMode, RenderPassEvent passEvent) =>
            new(lightMode, passEvent, new[] { lightMode }, RenderQueueType.Opaque, ~0, new RenderObjects.CustomCameraSettings());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            RenderPipelineManager.beginCameraRendering -= Enqueue; // 도메인 리로드를 끈 Play에서도 한 번만
            RenderPipelineManager.beginCameraRendering += Enqueue;
        }

        static void Enqueue(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            var renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            if (renderer == null) return;
            renderer.EnqueuePass(Mask);
            renderer.EnqueuePass(Hull);
            renderer.EnqueuePass(Outer);
        }
    }
}
