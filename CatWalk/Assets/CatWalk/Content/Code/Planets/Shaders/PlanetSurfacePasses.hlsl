#ifndef CATWALK_PLANET_SURFACE_PASSES_INCLUDED
#define CATWALK_PLANET_SURFACE_PASSES_INCLUDED

#include "PlanetSurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

float3 _LightDirection;
float3 _LightPosition;

struct PlanetAttributes
{
    float4 positionOS : POSITION;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PlanetVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 directionOS : TEXCOORD1;
    half4 fogAndVertexLight : TEXCOORD2;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 3);
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD4;
#endif
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD5;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

PlanetVaryings PlanetVertex(PlanetAttributes input)
{
    PlanetVaryings output = (PlanetVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.directionOS = normalize(input.positionOS.xyz);
    float3 positionOS = PlanetDisplace(input.positionOS.xyz);
    VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(output.directionOS);
    output.positionCS = positions.positionCS;
    output.positionWS = positions.positionWS;

#if defined(PLANET_SHADOW_PASS)
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirectionWS = normalize(_LightPosition - positions.positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
        ApplyShadowBias(positions.positionWS, normalWS, lightDirectionWS)));
#elif defined(PLANET_META_PASS)
    output.positionCS = UnityMetaVertexPosition(positionOS, input.staticLightmapUV,
        input.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST);
#else
    output.fogAndVertexLight = half4(ComputeFogFactor(positions.positionCS.z), VertexLighting(positions.positionWS, normalWS));
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
    #ifdef DYNAMICLIGHTMAP_ON
        output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    #endif
    OUTPUT_SH4(positions.positionWS, normalWS, GetWorldSpaceNormalizeViewDir(positions.positionWS), output.vertexSH, output.probeOcclusion);
#endif
    return output;
}

void PlanetLitFragment(PlanetVaryings input, out half4 color : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 renderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    SurfaceData surface = (SurfaceData)0;
    float3 normalOS;
    PlanetSurface(input.directionOS, surface.albedo, normalOS, surface.smoothness);
    surface.metallic = _Metallic;
    surface.normalTS = half3(0, 0, 1);
    surface.occlusion = 1;
    surface.alpha = 1;

    InputData data = (InputData)0;
    data.positionWS = input.positionWS;
    data.normalWS = NormalizeNormalPerPixel(TransformObjectToWorldNormal(normalOS));
    data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    data.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1), input.fogAndVertexLight.x);
    data.vertexLighting = input.fogAndVertexLight.yzw;
    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surface, data);
#endif
#if defined(DYNAMICLIGHTMAP_ON)
    data.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, data.normalWS);
    data.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    data.bakedGI = SAMPLE_GI(input.vertexSH, GetAbsolutePositionWS(data.positionWS), data.normalWS,
        data.viewDirectionWS, input.positionCS.xy, input.probeOcclusion, data.shadowMask);
#else
    data.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, data.normalWS);
    data.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
    // The same metallic BRDF, light loop and probe reflections as stock URP Lit.
    color = UniversalFragmentPBR(data, surface);
    color.rgb = MixFog(color.rgb, data.fogCoord);
    color.a = 1;
#ifdef _WRITE_RENDERING_LAYERS
    renderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
#endif
}

half4 PlanetDepthFragment(PlanetVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    return 0;
}

void PlanetDepthNormalsFragment(PlanetVaryings input, out half4 normal : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 renderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    half3 albedo;
    half smoothness;
    float3 normalOS;
    PlanetSurface(input.directionOS, albedo, normalOS, smoothness);
    float3 normalWS = NormalizeNormalPerPixel(TransformObjectToWorldNormal(normalOS));
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5;
    normal = half4(PackFloat2To888(saturate(oct)), 0);
#else
    normal = half4(normalWS, 0);
#endif
#ifdef _WRITE_RENDERING_LAYERS
    renderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
#endif
}

half4 PlanetMetaFragment(PlanetVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half3 albedo;
    half smoothness;
    float3 normalOS;
    PlanetSurface(input.directionOS, albedo, normalOS, smoothness);
    BRDFData brdf;
    half alpha = 1;
    InitializeBRDFData(albedo, _Metallic, half3(0, 0, 0), smoothness, alpha, brdf);
    MetaInput meta = (MetaInput)0;
    meta.Albedo = brdf.diffuse + brdf.specular * brdf.roughness * 0.5;
    return MetaFragment(meta);
}

#endif
