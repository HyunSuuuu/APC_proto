// 바닥 타일 테두리 글로우 (2026-11-19, 현금 존 — 먹을 돈이 있을 때). 반짝임 · 크기 변화 없는 정적 글로우.
// 메시 정점 색 알파(테두리 1 → 안 · 밖 0)를 제곱해 부드럽게 퍼지게 하고, 색은 _Color. 링 메시는 Visuals/TileGlow.cs가 만든다.
Shader "APC/ZoneGlow"
{
    Properties
    {
        _Color("Color", Color) = (0.3, 1, 0.4, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half a = input.color.a;
                return half4(_Color.rgb, _Color.a * a * a);
            }
            ENDHLSL
        }
    }
}
