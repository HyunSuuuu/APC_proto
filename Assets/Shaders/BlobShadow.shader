// 캐릭터 발밑 원형 그림자 (2026-11-26, 메인 라이트 그림자 대신). 곱하기 합성이라 바닥 색이 어둡게만 된다.
// UV 가운데에서 멀수록 옅어진다 (텍스처 없음). 캐릭터 자식 쿼드(`BlobShadow`)에 쓴다.
Shader "APC/BlobShadow"
{
    Properties
    {
        _Color ("Shadow", Color) = (0.45, 0.45, 0.5, 1)
        [Toggle] _Rect ("Rect (고정 물체 판)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Rect;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half2 p = abs(input.uv - 0.5h) * 2.0h;
                half d = _Rect > 0.5h ? max(p.x, p.y) : length(p); // 사각(StaticShadows) · 원(BlobShadow)
                half a = smoothstep(1.0h, _Rect > 0.5h ? 0.45h : 0.2h, d); // 가장자리 부드럽게
                return half4(lerp(half3(1, 1, 1), _Color.rgb, a), 1);
            }
            ENDHLSL
        }
    }
}
