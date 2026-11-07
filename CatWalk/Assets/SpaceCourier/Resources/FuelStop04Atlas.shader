Shader "DeepField/Fuel 04 Single Atlas"
{
    Properties
    {
        _BaseMap("Shared 512px atlas (RGB paint, A opacity)", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Cutout", Range(0,1)) = 0.5
        _EmissionStrength("Lamps (vertex alpha)", Range(0,4)) = 1.3
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _Cutoff, _EmissionStrength;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:TEXCOORD3; };
        Varyings Vert(Attributes i)
        {
            Varyings o;
            o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(i.normalOS);
            o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.color=i.color;
            return o;
        }
        half4 Atlas(Varyings i)
        {
            half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
            clip(c.a-_Cutoff);return c;
        }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings i):SV_Target
            {
                half3 paint=Atlas(i).rgb*_BaseColor.rgb*i.color.rgb;
                half3 n=normalize(i.normalWS);
                Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lighting=max(SampleSH(n),half3(.16,.19,.21));
                lighting+=main.color*(saturate(dot(n,main.direction))*.82*main.shadowAttenuation);
                #if defined(_ADDITIONAL_LIGHTS) || USE_FORWARD_PLUS
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if USE_FORWARD_PLUS
                for(uint j=0;j<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);j++)
                {
                    Light light=GetAdditionalLight(j,i.positionWS);
                    lighting+=light.color*(saturate(dot(n,light.direction))*light.distanceAttenuation);
                }
                #endif
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light light=GetAdditionalLight(lightIndex,i.positionWS);
                    lighting+=light.color*(saturate(dot(n,light.direction))*light.distanceAttenuation);
                LIGHT_LOOP_END
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                lighting+=VertexLighting(i.positionWS,n);
                #endif
                // Restrained checker dithering of light, not another texture or global post effect.
                half checker=fmod(floor(i.positionCS.x)+floor(i.positionCS.y),2)*.5+.25;
                lighting=floor(lighting*24+checker)/24;
                return half4(paint*lighting+paint*i.color.a*_EmissionStrength,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            half4 Depth(Varyings i):SV_Target { Atlas(i);return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes i)
            {
                Varyings o=Vert(i);
                float3 direction=_LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction=normalize(_LightPosition-o.positionWS);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,direction));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 Depth(Varyings i):SV_Target { Atlas(i);return 0; }
            ENDHLSL
        }
    }
}
