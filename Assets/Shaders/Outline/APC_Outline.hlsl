// 스텐실 인버티드 헐 외곽선 함수 (APC/OutlineStencil · APC/Toon 공용). 패스 정의(Stencil · Cull 등)는 각 셰이더에 둔다.
// 넣는 셰이더가 UnityPerMaterial CBUFFER에 _OutlineColor · _OutlineWidth · _OuterColor · _OuterWidth를 선언해야 한다
// (셰이더마다 CBUFFER가 하나여야 SRP Batcher가 묶는다).
#ifndef APC_OUTLINE_INCLUDED
#define APC_OUTLINE_INCLUDED

struct OutlineAttributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings OutlineMaskVert(OutlineAttributes input)
{
    OutlineVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS);
    return output;
}

// Width in pixels at 1080p, pushed sideways on screen only so the hull never pokes through the front surface.
OutlineVaryings OutlineHullVertWidth(OutlineAttributes input, float width)
{
    OutlineVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS));
    float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
    normalVS.z = 0;
    float clipW = mul(UNITY_MATRIX_P, float4(positionVS, 1)).w;
    positionVS.xy += SafeNormalize(normalVS).xy * width / 540.0 * clipW / abs(UNITY_MATRIX_P[1][1]);
    output.positionCS = TransformWViewToHClip(positionVS);
    return output;
}

OutlineVaryings OutlineHullVert(OutlineAttributes input) { return OutlineHullVertWidth(input, _OutlineWidth); }
OutlineVaryings OutlineOuterVert(OutlineAttributes input) { return OutlineHullVertWidth(input, _OutlineWidth + _OuterWidth); }

half4 OutlineFrag(OutlineVaryings input) : SV_Target { return half4(_OutlineColor.rgb, 1); }
half4 OutlineOuterFrag(OutlineVaryings input) : SV_Target { return half4(_OuterColor.rgb, 1); }

#endif
