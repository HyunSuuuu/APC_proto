// 캐릭터 툰 + 스텐실 외곽선 (2026-11-24, Shader Graph APC_Toon을 옮김).
// 색: 텍스처 × _BaseColor. 메인 라이트 N·L을 0.5 ± _ShadowSoftness smoothstep으로 가르고, 그늘 쪽은 채도(_ShadowSaturation) ·
//     명도(_ShadowBrightness) · 텍스처 알파로 섞은 웜/쿨 색을 곱한다. 라이트 색 · 그림자 받기 · 안개는 쓰지 않는다 (그래프와 같음). SSAO는 곱한다.
// 외곽선: APC/OutlineStencil과 같은 3패스(함수는 Outline/APC_Outline.hlsl). Visuals/OutlinePasses.cs가 LightMode로 그린다.
//   OutlineOuter 패스는 관찰 재질(_StencilGroup 255)에서만 켠다 (Material.SetShaderPassEnabled, CharacterToon · ObserveOutline).
// 재질은 런타임에 CharacterToon이 URP Lit 사본으로 만든다 (_BaseMap · _BaseColor 이름 유지). 스킨 메시 전용이라 인스턴싱 변형은 넣지 않는다.
Shader "APC/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("BaseMap", 2D) = "white" {}
        [MainColor] _BaseColor("BaseColor", Color) = (1, 1, 1, 1)
        _ShadowSoftness("ShadowSoftness", Float) = 0.142
        _ShadowSaturation("ShadowSaturation", Float) = 2.5
        _ShadowBrightness("ShadowBrightness", Float) = 0.8
        _ShadowWarmColor("ShadowWarmColor (텍스처 알파 0)", Color) = (0.9137255, 0.772549, 0.7843137, 1)
        _ShadowCoolColor("ShadowCoolColor (텍스처 알파 1)", Color) = (0.9137255, 0.772549, 0.7843137, 1)

        [Header(Outline)]
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _ShadowSoftness;
            float _ShadowSaturation;
            float _ShadowBrightness;
            half4 _ShadowWarmColor;
            half4 _ShadowCoolColor;
            half4 _OutlineColor;
            float _OutlineWidth;
            half4 _OuterColor;
            float _OuterWidth;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        #include "../Outline/APC_Outline.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv; // 그래프처럼 _BaseMap_ST(타일링)는 쓰지 않는다
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 albedo = tex.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight();
                // No Directional Light: URP reports a black light along +Z, which would shade every surface facing the camera.
                float3 lightDir = any(mainLight.color) ? mainLight.direction : normalize(float3(0.3, 0.8, -0.5));
                float ndl = saturate(dot(normalize(input.normalWS), lightDir));
                float lit = smoothstep(0.5 - _ShadowSoftness, 0.5 + _ShadowSoftness, ndl);

                float luma = dot(albedo, float3(0.2126729, 0.7151522, 0.0721750));
                float3 shade = (luma + _ShadowSaturation * (albedo - luma)) * _ShadowBrightness
                    * lerp(_ShadowWarmColor.rgb, _ShadowCoolColor.rgb, tex.a);
                float3 color = lerp(shade, albedo, lit);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                color *= GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS)).directAmbientOcclusion;
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            // URP ShadowCasterPass.hlsl GetShadowPositionHClip과 같다
            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
            }

            half4 DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthOnlyFrag
            half DepthOnlyFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        // SSAO(PC_Renderer)의 깊이 · 노멀 프리패스. Deferred가 아니라 _GBUFFER_NORMALS_OCT 인코딩은 넣지 않는다.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthNormalsFrag
            half4 DepthNormalsFrag(Varyings input) : SV_Target { return half4(NormalizeNormalPerPixel(input.normalWS), 0); }
            ENDHLSL
        }

        // 아래 3개는 APC/OutlineStencil과 같다 — 스텐실 규칙을 바꾸면 둘 다 고친다.
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
