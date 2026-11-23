// 벽 컷아웃: 카메라와 대상(플레이어 · 주차한 트럭 등) 사이에 있고, 카메라 방향 원기둥(대상마다 반지름 R) 안에 드는 벽 픽셀을 버린다.
// 값은 전역이며 WallCutout.cs가 매 프레임 넣는다. 대상이 0개면(꺼짐) 아무것도 버리지 않는다.
// 각 패스의 #include 뒤에 WALL_CUTOUT_* 하나를 정의하고 이 파일을 넣는다 (WallCutout.shader).
#ifndef APC_WALL_CUTOUT_INCLUDED
#define APC_WALL_CUTOUT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

#define WALL_CUTOUT_MAX 8 // WallCutout.cs MaxTargets와 같아야 한다

float4 _WallCutoutCenters[WALL_CUTOUT_MAX]; // xyz: 대상 월드 위치, w: 구멍 반지름(m)
float _WallCutoutCount;                     // 쓰는 대상 수 (0 = 꺼짐)
float4 _WallCutoutParams;                   // y: 가장자리 폭(m), z: 대상보다 이만큼(m) 앞부터 버림

void WallCutoutClip(float4 positionCS)
{
    int count = (int)_WallCutoutCount;
    if (count <= 0) return;

    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float3 positionWS = ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP);
    float3 normal = cross(ddy(positionWS), ddx(positionWS));
    bool ortho = unity_OrthoParams.w > 0.5;

    float inside = 0;
    [loop] for (int i = 0; i < WALL_CUTOUT_MAX; i++)
    {
        if (i >= count) break;
        float4 center = _WallCutoutCenters[i];

        // 직교 카메라는 카메라 앞 방향, 원근은 카메라 → 대상 방향을 원기둥 축으로 쓴다
        float3 axis = ortho ? -UNITY_MATRIX_V[2].xyz : normalize(center.xyz - _WorldSpaceCameraPos);
        float3 offset = positionWS - center.xyz;
        float along = dot(offset, axis);

        // 이 픽셀이 있는 면(평면)이 대상 → 카메라 축과 만나는 지점으로 앞뒤를 정한다.
        // 픽셀마다 앞뒤를 재면 비스듬한 벽 위에서 판정이 갈려 구멍이 직선으로 잘린다.
        float facing = dot(axis, normal);
        float hit = abs(facing) > 1e-8 ? dot(offset, normal) / facing : along;
        if (hit > -_WallCutoutParams.z) continue; // 대상보다 뒤에 있는 면은 그대로

        float distance = length(offset - axis * along);
        inside = max(inside, saturate((center.w - distance) / max(_WallCutoutParams.y, 1e-4)));
    }
    // 가장자리는 디더로 점점 비운다 (inside 1 = 전부 버림, 0 = 전부 남김)
    clip(InterleavedGradientNoise(positionCS.xy, 0) - inside);
}

#if defined(WALL_CUTOUT_FORWARD)
#if defined(_WALL_GRADIENT)
// 부지 벽 안쪽 면 그라데이션. 값은 전역이며 SiteWalls.cs가 넣는다 (색은 SetGlobalColor라 선형으로 들어옴).
half4 _WallGradientBottom;
half4 _WallGradientTop;
float _WallGradientHeight; // 조각 피벗(바닥)에서 잰 월드 높이(m) — 0에서 아래 색, 이 높이에서 위 색, 이보다 위는 원래 색

// LitPassFragment(URP LitForwardPass.hlsl) 복사본 + 안쪽 면 albedo 바꾸기. URP를 올리면 원본과 맞춰 볼 것.
void WallGradientLitFragment(Varyings input, out half4 outColor
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    // wall_L · wall_S (Z-up fbx): 로컬 +y = 부지 안쪽을 보는 큰 면, 로컬 z = 높이. SiteWalls가 로컬 +z(회전 후)를 바깥으로 세운다
    // 위쪽 갈색 띠(3.6955m~)는 그대로 두려고 _WallGradientHeight 아래만 칠한다
    float3 normalOS = TransformWorldToObjectDir(input.normalWS);
    float height = input.positionWS.y - UNITY_MATRIX_M._m13;
    if (normalOS.y > 0.5 && height < _WallGradientHeight)
    {
        float t = saturate(height / max(_WallGradientHeight, 1e-3));
        surfaceData.albedo = lerp(_WallGradientBottom.rgb, _WallGradientTop.rgb, t);
    }

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
#define WALL_LIT_FRAGMENT WallGradientLitFragment
#else
#define WALL_LIT_FRAGMENT LitPassFragment
#endif

void CutoutLitFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    WallCutoutClip(input.positionCS);
    WALL_LIT_FRAGMENT(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#endif

#if defined(WALL_CUTOUT_GBUFFER)
GBufferFragOutput CutoutGBufferFragment(Varyings input)
{
    WallCutoutClip(input.positionCS);
    return LitGBufferPassFragment(input);
}
#endif

#if defined(WALL_CUTOUT_DEPTH)
half CutoutDepthOnlyFragment(Varyings input) : SV_TARGET
{
    WallCutoutClip(input.positionCS);
    return DepthOnlyFragment(input);
}
#endif

#if defined(WALL_CUTOUT_DEPTH_NORMALS)
void CutoutDepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    WallCutoutClip(input.positionCS);
    DepthNormalsFragment(input, outNormalWS
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#endif

#endif
