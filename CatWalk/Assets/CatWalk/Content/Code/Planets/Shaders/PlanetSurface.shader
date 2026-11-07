Shader "CatWalk/Planet Surface"
{
    Properties
    {
        [Header(Shape)]
        _Seed ("Seed", Float) = 1
        _Displacement ("Relief depth / sphere radius", Range(0, 0.3)) = 0.08
        _NormalStrength ("Terrain normal strength", Range(0, 2)) = 1
        _TerrainScale ("Terrain scale", Range(1, 30)) = 4
        _TerrainHeight ("Hills height", Range(0, 1)) = 0.55
        _Roughness ("Small scale roughness", Range(0, 0.8)) = 0.48
        _MountainHeight ("Mountain height", Range(0, 1)) = 0.16
        _MountainScale ("Mountain scale", Range(1, 30)) = 7

        [Header(Craters)]
        [ToggleUI] _Craters ("Enable craters", Float) = 1
        _CraterScale ("Crater frequency", Range(2, 40)) = 10
        _CraterDensity ("Crater density", Range(0, 1)) = 0.32
        _CraterSize ("Crater size", Range(0.2, 1)) = 0.8
        _CraterDepth ("Crater depth", Range(0, 1)) = 0.3
        _CraterRim ("Crater rim height", Range(0, 0.5)) = 0.08

        [Header(Depressions)]
        [ToggleUI] _Basins ("Enable depressions", Float) = 1
        _BasinScale ("Depression scale", Range(1, 20)) = 3
        _BasinCoverage ("Depression coverage", Range(0, 1)) = 0.32
        _BasinDepth ("Depression depth", Range(0, 1)) = 0.3

        [Header(Canyons)]
        [ToggleUI] _Canyons ("Enable canyons", Float) = 1
        _CanyonScale ("Canyon frequency", Range(1, 30)) = 5
        _CanyonWidth ("Canyon width", Range(0.01, 0.3)) = 0.06
        _CanyonDepth ("Canyon depth", Range(0, 1)) = 0.35
        _CanyonWarp ("Canyon meandering", Range(0, 3)) = 1.2

        [Header(URP Lit Surface)]
        [MainColor] _LowColor ("Lowland color", Color) = (0.19, 0.13, 0.08, 1)
        _HighColor ("Highland color", Color) = (0.57, 0.51, 0.38, 1)
        _RockColor ("Cliff color", Color) = (0.28, 0.29, 0.27, 1)
        _ColorHeight ("Lowland to highland height", Range(0, 1)) = 0.5
        _ColorBlend ("Color blend width", Range(0.01, 0.5)) = 0.22
        _RockSlope ("Cliff slope threshold", Range(0.01, 1)) = 0.25
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Ground smoothness", Range(0, 1)) = 0.22

        [Header(Grass Cover)]
        [ToggleUI] _Grass ("Enable grass cover", Float) = 1
        _GrassColor ("Grass color", Color) = (0.18, 0.31, 0.065, 1)
        _GrassDryColor ("Dry grass color", Color) = (0.4, 0.34, 0.12, 1)
        _GrassCoverage ("Grass coverage", Range(0, 1)) = 0.58
        _GrassScale ("Grass patch scale", Range(1, 50)) = 9
        _GrassMinHeight ("Grass minimum height", Range(0, 1)) = 0.3
        _GrassMaxHeight ("Grass maximum height", Range(0, 1)) = 0.75
        _GrassMaxSlope ("Grass maximum slope", Range(0.01, 1)) = 0.3
        _GrassDetail ("Grass detail frequency", Range(10, 500)) = 160
        _GrassBump ("Grass microrelief", Range(0, 3)) = 0.65
        _GrassSmoothness ("Grass smoothness", Range(0, 1)) = 0.1

        [Header(Adaptive Tessellation)]
        _Tessellation ("Maximum subdivisions", Range(1, 16)) = 4
        _TessellationNear ("Full detail distance / radius", Range(0, 10)) = 2
        _TessellationFar ("Base mesh distance / radius", Range(1, 100)) = 15
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "PreviewType"="Sphere" "DisableBatching"="True" }
        LOD 300
        Cull Back
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.6
            #pragma require tessellation tesshw
            #pragma vertex PlanetTessellationVertex
            #pragma hull PlanetHull
            #pragma domain PlanetDomain
            #pragma fragment PlanetLitFragment
            #include_with_pragmas "PlanetSurfaceForward.hlsl"
            #include "PlanetSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.6
            #pragma require tessellation tesshw
            #pragma vertex PlanetTessellationVertex
            #pragma hull PlanetHull
            #pragma domain PlanetDomain
            #pragma fragment PlanetDepthFragment
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #define PLANET_SHADOW_PASS
            #include "PlanetSurfacePasses.hlsl"
            #include "PlanetSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 4.6
            #pragma require tessellation tesshw
            #pragma vertex PlanetTessellationVertex
            #pragma hull PlanetHull
            #pragma domain PlanetDomain
            #pragma fragment PlanetDepthFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "PlanetSurfacePasses.hlsl"
            #include "PlanetSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 4.6
            #pragma require tessellation tesshw
            #pragma vertex PlanetTessellationVertex
            #pragma hull PlanetHull
            #pragma domain PlanetDomain
            #pragma fragment PlanetDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "PlanetSurfacePasses.hlsl"
            #include "PlanetSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PlanetVertex
            #pragma fragment PlanetMetaFragment
            #define PLANET_META_PASS
            #include "PlanetSurfacePasses.hlsl"
            ENDHLSL
        }
    }

    // Devices without tessellation keep the same surface and displace the existing mesh vertices.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "PreviewType"="Sphere" "DisableBatching"="True" }
        LOD 200
        Cull Back
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PlanetVertex
            #pragma fragment PlanetLitFragment
            #include_with_pragmas "PlanetSurfaceForward.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PlanetVertex
            #pragma fragment PlanetDepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #define PLANET_SHADOW_PASS
            #include "PlanetSurfacePasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PlanetVertex
            #pragma fragment PlanetDepthFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "PlanetSurfacePasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PlanetVertex
            #pragma fragment PlanetDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "PlanetSurfacePasses.hlsl"
            ENDHLSL
        }
        UsePass "CatWalk/Planet Surface/Meta"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
