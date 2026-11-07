Shader "CatWalk/Space Sky"
{
    Properties
    {
        _VoidColor("Deep space", Color) = (0.004,0.007,0.016,1)
        _NebulaCool("Nebula / petrol blue", Color) = (0.075,0.28,0.32,1)
        _NebulaWarm("Nebula / faded violet", Color) = (0.28,0.085,0.19,1)
        _PixelResolution("Vertical sky pixels", Range(96,1024)) = 270
        _ColorSteps("Cloud palette steps", Range(4,32)) = 14
        _Seed("Seed", Float) = 108
        _StarDensity("Star density", Range(0,1)) = 0.55
        _GalaxyVisibility("Galaxy formation", Range(0,1)) = 0.85
        _NebulaDensity("Nebula density", Range(0,1)) = 0.4
        _Activity("Stellar activity", Range(0,1)) = 0.65
        _Warmth("Warmth", Range(0,1)) = 0.35
        _Exposure("Exposure", Range(0,3)) = 1
        _SkyTime("Visual clock", Float) = 0
        _SunRadius("Sun angular radius", Range(0.2,15)) = 5
        _SunCorona("Sun corona", Range(0,2)) = 0.9
        [HideInInspector] _GalaxyDirection0("Galaxy 1 direction", Vector) = (0,0,1,0)
        [HideInInspector] _GalaxyDirection1("Galaxy 2 direction", Vector) = (0,0,1,0)
        [HideInInspector] _GalaxyDirection2("Galaxy 3 direction", Vector) = (0,0,1,0)
        [HideInInspector] _GalaxyShape0("Galaxy 1 shape", Vector) = (0,0,0,0)
        [HideInInspector] _GalaxyShape1("Galaxy 2 shape", Vector) = (0,0,0,0)
        [HideInInspector] _GalaxyShape2("Galaxy 3 shape", Vector) = (0,0,0,0)
        [HideInInspector] _GalaxyColor0("Galaxy 1 color", Color) = (1,1,1,1)
        [HideInInspector] _GalaxyColor1("Galaxy 2 color", Color) = (1,1,1,1)
        [HideInInspector] _GalaxyColor2("Galaxy 3 color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Sphere" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                float4 _VoidColor, _NebulaCool, _NebulaWarm;
                float _PixelResolution, _ColorSteps, _Seed, _StarDensity;
                float _GalaxyVisibility, _NebulaDensity, _Activity, _Warmth, _Exposure, _SkyTime;
                float _SunRadius, _SunCorona;
                float4 _GalaxyDirection0, _GalaxyDirection1, _GalaxyDirection2;
                float4 _GalaxyShape0, _GalaxyShape1, _GalaxyShape2;
                float4 _GalaxyColor0, _GalaxyColor1, _GalaxyColor2;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // A real sphere, centred on each rendering camera, always behind scene geometry.
                // Ignore translation so flight, origin shifts and stereo eye offsets cannot move the sky.
                float3 view = mul((float3x3)UNITY_MATRIX_V, input.vertex.xyz);
                output.positionCS = mul(UNITY_MATRIX_P, float4(view, 1));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = 0;
                #else
                    output.positionCS.z = output.positionCS.w;
                #endif
                output.direction = input.vertex.xyz;
                return output;
            }

            // All detail is arithmetic. No textures, cubemaps, particles or world-position input.
            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x),
                         lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), f.x),
                         lerp(Hash(cell + float3(0,1,1)), Hash(cell + 1), f.x), f.y), f.z);
            }

            float Fractal(float3 p)
            {
                float value = Noise(p) * 0.55;
                p = p.yzx * 2.03 + 17.1; value += Noise(p) * 0.26;
                p = p.yzx * 2.01 + 11.7; value += Noise(p) * 0.13;
                return value + Noise(p.zxy * 2.02 + 7.3) * 0.06;
            }

            float3 PixelDirection(float3 direction, float2 screenPosition, out float2 pixel)
            {
                // Pixelate the image, never parameterize the sphere as six planar faces.
                float size = max(1.0, floor(_ScaledScreenParams.y / max(96.0, _PixelResolution) + 0.5));
                pixel = floor(screenPosition / size);
                float2 offset = (pixel + 0.5) * size - screenPosition;
                float viewDepth = -mul((float3x3)UNITY_MATRIX_V, direction).z;
                float3 ray = direction / max(viewDepth, 0.00001);
                // A ray on the z=-1 view plane is affine in screen coordinates, even across mesh triangles.
                ray += ddx(ray) * offset.x + ddy(ray) * offset.y;
                return normalize(ray);
            }

            float Dither(float2 pixel)
            {
                float2 p = fmod(abs(pixel), 4.0);
                float2 low = fmod(p, 2.0), high = floor(p * 0.5);
                float b0 = 2.0 * low.x + 3.0 * low.y - 4.0 * low.x * low.y;
                float b1 = 2.0 * high.x + 3.0 * high.y - 4.0 * high.x * high.y;
                return (b0 * 4.0 + b1 + 0.5) / 16.0;
            }

            float Life(float3 id)
            {
                float phase = Hash(id + 19.3) * TWO_PI;
                float rate = lerp(0.13, 0.55, Hash(id + 41.7));
                float pulse = 0.82 + 0.18 * sin(_SkyTime * 0.9 + phase);
                float variable = smoothstep(Hash(id + 73.1) - 0.1, Hash(id + 73.1) + 0.1, _Activity * 0.75);
                float burning = smoothstep(-0.45, 0.3, sin(_SkyTime * rate + phase));
                return pulse * lerp(1.0, burning, variable);
            }

            void Galaxies(float3 d, out float mass, out float3 light, out float3 starTint)
            {
                mass = 0; light = 0; starTint = 0;
                [unroll] for (int g = 0; g < 3; g++)
                {
                    float4 centre = g == 0 ? _GalaxyDirection0 : g == 1 ? _GalaxyDirection1 : _GalaxyDirection2;
                    float4 shape = g == 0 ? _GalaxyShape0 : g == 1 ? _GalaxyShape1 : _GalaxyShape2;
                    float3 tint = (g == 0 ? _GalaxyColor0 : g == 1 ? _GalaxyColor1 : _GalaxyColor2).rgb;
                    float front = dot(d, centre.xyz);
                    float reveal = smoothstep(g * 0.16, 0.58 + g * 0.16, _GalaxyVisibility) * shape.w;
                    if (front > 0.25 && reveal > 0.001 && centre.w > 0.001)
                    {
                        float3 right = normalize(cross(abs(centre.y) < 0.95 ? float3(0,1,0) : float3(1,0,0), centre.xyz));
                        float3 up = cross(centre.xyz, right);
                        float2 q = float2(dot(d, right), dot(d, up)) / (front * centre.w);
                        float s, c; sincos(shape.y, s, c);
                        q = float2(c * q.x - s * q.y, s * q.x + c * q.y);
                        q.y /= max(0.15, shape.x);
                        float radius = length(q);
                        if (radius < 1.2)
                        {
                            float disc = exp(-radius * radius * 2.7) * (1.0 - smoothstep(0.8, 1.2, radius));
                            float angle = atan2(q.y, q.x);
                            float arms = pow(saturate(0.5 + 0.5 * cos(angle * shape.z - log(max(radius, 0.035)) * 6.4 - _SkyTime * 0.008 * _Activity)), 5.0);
                            float knots = Noise(d * 95.0 + _Seed * 0.17 + g * 17.0);
                            float birth = smoothstep(knots * 0.65, knots * 0.65 + 0.35, reveal);
                            float core = exp(-radius * radius * 85.0);
                            float field = (arms * disc + core * 0.85) * (0.35 + 0.65 * knots) * birth * reveal;
                            mass += field;
                            starTint += tint * field;
                            // Almost all of the spiral is resolved stars; the halo stays deliberately faint.
                            light += tint * (disc * 0.008 + field * 0.025 + core * 0.012) * reveal;
                        }
                    }
                }
                starTint /= max(mass, 0.0001);
            }

            float3 Nebula(float3 d)
            {
                if (_NebulaDensity < 0.001) return 0;
                float3 p = d * 3.7 + _Seed * 0.013;
                float3 drift = float3(0.004, -0.002, 0.003) * _SkyTime * lerp(0.2, 1.0, _Activity);
                float3 warp = float3(Noise(p + 3.1), Noise(p + 17.7), Noise(p + 41.3)) - 0.5;
                float cloud = Fractal(p + warp * 1.4 + drift);
                float belt = 1.0 - smoothstep(0.12, 0.85, abs(dot(d, normalize(float3(-0.37, 0.85, 0.24))) + 0.08));
                float threshold = lerp(0.74, 0.30, _NebulaDensity);
                float shape = smoothstep(threshold, threshold + 0.22, cloud) * belt * _NebulaDensity;
                float filaments = Noise(p * 4.8 + warp * 2.0 + drift * 0.3);
                float dust = 1.0 - smoothstep(0.48, 0.72, filaments) * 0.78;
                float tint = saturate(Noise(p * 0.8 + 23.7) * 1.7 - 0.3 + (_Warmth - 0.5) * 0.6);
                float3 color = lerp(_NebulaCool.rgb, _NebulaWarm.rgb, tint);
                color = lerp(color, float3(0.34, 0.22, 0.10), _Warmth * smoothstep(0.62, 0.85, cloud) * 0.65);
                return color * shape * dust * (0.35 + 0.65 * cloud);
            }

            float3 Stars(float3 d, float mass, float3 galaxyTint)
            {
                // A single stationary 3D field sampled on a unit sphere. No face IDs, UV seams or poles.
                float3 p = d * 96.0 + _Seed * float3(0.17, 0.31, 0.23);
                float3 id = floor(p);
                float3 jitter = float3(Hash(id + 1.7), Hash(id + 9.2), Hash(id + 21.3)) * 0.22 - 0.11;
                float3 delta = frac(p) - 0.5 - jitter;
                // Every star is contained within its cell, so cell boundaries contain no truncated stars.
                float core = 1.0 - smoothstep(0.12, 0.36, length(delta));
                float probability = _StarDensity * 0.18 + mass * 3.5;
                float faint = smoothstep(Hash(id + 53.0) - 0.018, Hash(id + 53.0) + 0.018, probability);
                faint *= saturate(probability * 100.0) * Life(id) * core;
                float3 cold = float3(0.48, 0.68, 0.78), warm = float3(1.0, 0.75, 0.42);
                float3 color = lerp(cold, warm, saturate(Hash(id + 8.1) * 0.8 + _Warmth * 0.35));
                color = lerp(color, galaxyTint, saturate(mass * 3.0) * 0.7);
                float3 stars = color * faint * lerp(0.65, 1.6, Hash(id + 13.1)) * (1.0 + mass * 2.0);

                p = d * 24.0 + _Seed * float3(0.37, 0.13, 0.29) + 7.3;
                id = floor(p);
                jitter = float3(Hash(id + 1.7), Hash(id + 9.2), Hash(id + 21.3)) * 0.14 - 0.07;
                float3 a = abs(frac(p) - 0.5 - jitter);
                float outer = 1.0 - smoothstep(0.34, 0.42, max(a.x, max(a.y, a.z)));
                float crossDistance = min(max(a.x, a.y), min(max(a.y, a.z), max(a.z, a.x)));
                float rays = (1.0 - smoothstep(0.025, 0.065, crossDistance)) * outer;
                float centre = 1.0 - smoothstep(0.08, 0.19, length(a));
                float visible = smoothstep(Hash(id + 53.0) - 0.04, Hash(id + 53.0) + 0.04, _StarDensity * 0.22);
                visible *= saturate(_StarDensity * 100.0) * Life(id);
                stars += lerp(cold, warm, Hash(id + 37.0)) * (centre * 1.8 + rays * 0.35) * visible;
                return stars;
            }

            float3 NearbySun(float3 d, float3 background, float2 pixel)
            {
                // URP supplies -light.forward and the light's color * intensity, including temperature.
                // These globals update per camera in both Edit and Play modes; no material copy is needed.
                float3 sunColor = max(_MainLightColor.rgb, 0.0);
                if (max(sunColor.r, max(sunColor.g, sunColor.b)) <= 0.0) return background;
                float3 sunDirection = normalize(_MainLightPosition.xyz);
                float front = dot(d, sunDirection);
                if (front <= 0.0) return background;
                float3 right = normalize(cross(abs(sunDirection.y) < 0.95 ? float3(0,1,0) : float3(1,0,0), sunDirection));
                float3 up = cross(sunDirection, right);
                float2 q = float2(dot(d, right), dot(d, up)) / (front * tan(radians(clamp(_SunRadius, 0.2, 15.0))));
                float radius = length(q);
                if (radius >= 3.4) return background;

                // Use the same pixel ray and visual clock as the sky: no billboard, UV seam or texture.
                float time = _SkyTime * 0.05;
                float light;
                if (radius < 1.0)
                {
                    float limb = sqrt(saturate(1.0 - radius * radius));
                    float3 surface = float3(q, limb);
                    float s, c; sincos(time * 0.3, s, c);
                    surface.xz = float2(c * surface.x + s * surface.z, -s * surface.x + c * surface.z);
                    float3 flow = float3(0, time * 0.35, time * 0.12) + _Seed * 0.17;
                    float plasma = Fractal(surface * 5.5 + flow);
                    float grain = Noise(surface * 34.0 + flow * 1.7);
                    float heat = smoothstep(0.25, 0.78, plasma * 0.75 + grain * 0.25);
                    light = lerp(0.55, 1.0, heat) * (0.65 + 0.35 * pow(limb, 0.4));
                }
                else
                {
                    float edge = radius - 1.0;
                    float2 rim = q / radius;
                    float angle = atan2(rim.y, rim.x);
                    // Sampling a circle and integer angular frequencies keeps the corona closed.
                    float flow = Noise(float3(rim * 4.0, time * 0.2)) * 4.0;
                    float streamers = pow(saturate(0.5 + 0.5 * sin(angle * 11.0 + flow + time * 0.45)), 4.0);
                    float wisps = pow(saturate(0.5 + 0.5 * sin(angle * 31.0 - time * 0.9 + edge * 3.0)), 6.0);
                    float reach = 0.18 + streamers * 0.7 + wisps * 0.15;
                    float corona = exp(-edge * 3.0 / reach) * (0.35 + streamers * 0.45);
                    corona += exp(-edge * 2.6) * 0.1;
                    corona *= (1.0 - smoothstep(2.5, 3.4, radius)) * _SunCorona;
                    light = corona;
                }
                float levels = max(4.0, _ColorSteps);
                light = pow(floor(sqrt(max(light, 0.0)) * levels + Dither(pixel)) / levels, 2.0);
                // The opaque disc hides distant stars; its corona adds light around the limb.
                return light * sunColor + (radius < 1.0 ? float3(0,0,0) : background);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 pixel;
                float3 d = PixelDirection(input.direction, input.positionCS.xy, pixel);
                float mass; float3 galaxyLight, galaxyTint;
                Galaxies(d, mass, galaxyLight, galaxyTint);
                float3 cloud = Nebula(d) + galaxyLight;
                // Quantize in perceptual space so low-light bands survive without crushing to black.
                float levels = max(4.0, _ColorSteps);
                cloud = pow(floor(sqrt(max(cloud, 0.0)) * levels + Dither(pixel)) / levels, 2.0);

                float3 stars = Stars(d, mass, galaxyTint);
                float3 color = NearbySun(d, _VoidColor.rgb + cloud + stars, pixel);
                return half4(color * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
