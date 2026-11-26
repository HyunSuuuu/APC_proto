// 구운 그림자 덮개 (2026-11-27). BakedShadows가 부지가 바뀔 때 빛 방향 직교 카메라로 정적 물체 실루엣을 한 번 찍어(_BakedShadowMask)
// 그 행렬(_BakedShadowVP)을 넘긴다. 바닥 높이의 큰 판이 자기 위치를 빛 쪽으로 투영해 마스크가 덮인 곳을 곱하기로 어둡게 한다.
Shader "APC/BakedShadow"
{
    Properties
    {
        _Color ("Shadow", Color) = (0.62, 0.62, 0.7, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Offset -1, -1
        Blend DstColor Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            TEXTURE2D(_BakedShadowMask);
            SAMPLER(sampler_BakedShadowMask);
            float4x4 _BakedShadowVP;
            float4 _BakedShadowMask_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BakedShadowMask, sampler_BakedShadowMask, uv).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 clip = mul(_BakedShadowVP, float4(input.positionWS, 1));
                float2 uv = clip.xy / clip.w * 0.5 + 0.5;
                if (any(uv < 0) || any(uv > 1)) return half4(1, 1, 1, 1);
                // 5점 평균으로 가장자리를 살짝 부드럽게
                float2 t = _BakedShadowMask_TexelSize.xy * 1.5;
                half m = (Mask(uv) * 2 + Mask(uv + float2(t.x, 0)) + Mask(uv - float2(t.x, 0)) + Mask(uv + float2(0, t.y)) + Mask(uv - float2(0, t.y))) / 6;
                return half4(lerp(half3(1, 1, 1), _Color.rgb, m), 1);
            }
            ENDHLSL
        }
    }
}
