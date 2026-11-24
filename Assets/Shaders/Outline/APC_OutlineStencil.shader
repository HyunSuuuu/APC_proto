// 스텐실 인버티드 헐 외곽선 (VLB 패키지 AP/OutlineStencil v0.1.4를 옮기고 관찰용 바깥 테두리를 더함).
// 같은 Stencil Group끼리는 사이 선을 지워 한 덩어리로 그리고, 다른 그룹 · 외곽선 없는 물체(바닥 등) 위에는 그대로 그린다.
// URP가 기본으로 그리지 않는 LightMode만 쓴다. Visuals/OutlinePasses.cs가 렌더러 에셋 수정 없이 매 카메라에 패스를 넣는다:
//   OutlineMask(AfterRenderingOpaques) → OutlineHull(BeforeRenderingSkybox) → OutlineOuter(BeforeRenderingSkybox, Hull 다음)
// 그룹 값 규칙: 일반 1~126, 16의 배수는 피한다 (지금 렌더러는 Forward · Forward+지만, Deferred로 바꾸면 URP가 바닥 픽셀에 16 · 32 · 64 비트를 남겨 겹친다),
//              관찰 255 (128 비트 = 흰 헐 표시. 하위 7비트 127은 일반 그룹이 쓰지 않는다).
// OutlineOuter 패스는 관찰 재질(그룹 255)에서만 켠다 (Material.SetShaderPassEnabled, 메뉴 APC/Art/Validate Outline Materials).
// 함수는 APC_Outline.hlsl (APC/Toon과 공용). 외곽선 없는 재질(Lit 등) 뒤 슬롯에 붙일 때 쓴다.
// 셰이더 이름에 "outline"을 유지한다 (VLB VertexLightBaker.IsOutline이 변환에서 제외).
Shader "APC/OutlineStencil"
{
    Properties
    {
        _OutlineColor("OutlineColor", Color) = (0, 0, 0, 1)
        _OutlineWidth("OutlineWidth", Float) = 2
        [IntRange] _StencilGroup("Stencil Group", Range(1, 255)) = 1
        _OuterColor("Outer Color (관찰 전용)", Color) = (0, 0, 0, 1)
        _OuterWidth("Outer Width (관찰 전용, 픽셀)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #pragma multi_compile_instancing
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            half4 _OuterColor;
            float _OuterWidth;
        CBUFFER_END

        #include "APC_Outline.hlsl"
        ENDHLSL

        // Marks the visible pixels of every outlined body with its group.
        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "OutlineMask" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Stencil { Ref [_StencilGroup] Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex OutlineMaskVert
            #pragma fragment OutlineFrag
            ENDHLSL
        }

        // Hull is rejected over bodies of its own group, so seams inside a group vanish.
        // Writes only bit 128: on for the observed group (255), off for normal groups (1~126).
        Pass
        {
            Name "OutlineHull"
            Tags { "LightMode" = "OutlineHull" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Stencil { Ref [_StencilGroup] ReadMask 127 WriteMask 128 Comp NotEqual Pass Replace }

            HLSLPROGRAM
            #pragma vertex OutlineHullVert
            #pragma fragment OutlineFrag
            ENDHLSL
        }

        // Observed only: a thin ring outside the white hull. Same z as the white hull, so stencil bit 128
        // (observed body 255 + white hull pixels) keeps it out instead of the depth test (no z-fighting).
        Pass
        {
            Name "OutlineOuter"
            Tags { "LightMode" = "OutlineOuter" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Stencil { Ref 128 ReadMask 128 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex OutlineOuterVert
            #pragma fragment OutlineOuterFrag
            ENDHLSL
        }
    }
}
