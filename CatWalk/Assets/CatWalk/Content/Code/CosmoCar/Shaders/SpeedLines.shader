// Полосы ветра маршевого режима: лучи бегут от центра экрана к краям, центр остаётся чистым.
// Рисуется квадом перед камерой (CosmoCarCamera) прямо в клип-пространстве, на весь экран.
Shader "CatWalk/Speed Lines"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 0
        _LineCount ("Line Count", Float) = 40
        _Speed ("Speed", Float) = 5
        _ClearRadius ("Clear Radius", Float) = 0.75
        _Color ("Color", Color) = (1, 1, 1, 0.55)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "SpeedLines"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _LineCount;
                float _Speed;
                float _ClearRadius;
                float4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Квад −0.5…0.5 растягивается на весь экран независимо от камеры и её FOV.
                #if UNITY_REVERSED_Z
                    output.positionCS = float4(input.positionOS.xy * 2.0, 0.5, 1.0);
                #else
                    output.positionCS = float4(input.positionOS.xy * 2.0, 0.0, 1.0);
                #endif
                output.uv = input.uv;
                return output;
            }

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                p.x *= _ScreenParams.x / _ScreenParams.y;
                float r = length(p);

                // Экран делится на узкие секторы, в каждом — свой луч.
                float s = (atan2(p.y, p.x) * (0.5 / PI) + 0.5) * _LineCount;
                float id = floor(s);
                float h1 = Hash(id), h2 = Hash(id + 31.7), h3 = Hash(id + 73.1);

                // С ростом интенсивности лучей становится больше.
                float on = saturate((_Intensity * 0.8 - h3) * 5.0);
                float across = 1.0 - smoothstep(0.0, 0.06 + 0.1 * h2, abs(frac(s) - (0.25 + 0.5 * h1)));

                // Штрих бежит наружу, у каждого луча своя скорость и длина.
                float f = frac(r * (0.7 + 0.5 * h2) - _Time.y * _Speed * (0.7 + 0.6 * h1) + h3 * 7.0);
                float dash = smoothstep(0.0, 0.1, f) * (1.0 - smoothstep(0.25, 0.55, f));

                float edge = smoothstep(_ClearRadius, _ClearRadius + 0.45, r);
                float alpha = across * dash * edge * on * _Intensity * _Color.a;
                return float4(_Color.rgb * alpha, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
