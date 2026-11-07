#ifndef CATWALK_PLANET_SURFACE_INPUT_INCLUDED
#define CATWALK_PLANET_SURFACE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// One layout in every pass, including the non-tessellated fallback (SRP Batcher).
CBUFFER_START(UnityPerMaterial)
    float4 _LowColor;
    float4 _HighColor;
    float4 _RockColor;
    float4 _GrassColor;
    float4 _GrassDryColor;
    float _Seed;
    float _Displacement;
    float _NormalStrength;
    float _TerrainScale;
    float _TerrainHeight;
    float _Roughness;
    float _MountainHeight;
    float _MountainScale;
    float _Craters;
    float _CraterScale;
    float _CraterDensity;
    float _CraterSize;
    float _CraterDepth;
    float _CraterRim;
    float _Basins;
    float _BasinScale;
    float _BasinCoverage;
    float _BasinDepth;
    float _Canyons;
    float _CanyonScale;
    float _CanyonWidth;
    float _CanyonDepth;
    float _CanyonWarp;
    float _ColorHeight;
    float _ColorBlend;
    float _RockSlope;
    float _Metallic;
    float _Smoothness;
    float _Grass;
    float _GrassCoverage;
    float _GrassScale;
    float _GrassMinHeight;
    float _GrassMaxHeight;
    float _GrassMaxSlope;
    float _GrassDetail;
    float _GrassBump;
    float _GrassSmoothness;
    float _Tessellation;
    float _TessellationNear;
    float _TessellationFar;
CBUFFER_END

float3 PlanetHash(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float PlanetNoise(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = lerp(PlanetHash(cell).x, PlanetHash(cell + float3(1, 0, 0)).x, f.x);
    float b = lerp(PlanetHash(cell + float3(0, 1, 0)).x, PlanetHash(cell + float3(1, 1, 0)).x, f.x);
    float c = lerp(PlanetHash(cell + float3(0, 0, 1)).x, PlanetHash(cell + float3(1, 0, 1)).x, f.x);
    float d = lerp(PlanetHash(cell + float3(0, 1, 1)).x, PlanetHash(cell + 1.0).x, f.x);
    return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
}

float3 PlanetSeedOffset()
{
    return PlanetHash(float3(_Seed, _Seed + 19.1, _Seed + 73.7)) * 127.0;
}

float PlanetFractal(float3 p)
{
    float amplitude = 1.0;
    float value = 0.0;
    float weight = 0.0;
    [unroll] for (int octave = 0; octave < 4; octave++)
    {
        value += (PlanetNoise(p) * 2.0 - 1.0) * amplitude;
        weight += amplitude;
        p = p * 2.03 + float3(13.1, 7.7, 19.3);
        amplitude *= _Roughness;
    }
    return value / max(weight, 0.001);
}

float PlanetCraterHeight(float3 p)
{
    float3 cell = floor(p);
    float3 local = frac(p);
    float bowl = 0.0;
    float rim = 0.0;
    // A bounded 3D neighbourhood gives circular bowls without UV seams or polar stretching.
    [loop] for (int z = -1; z <= 1; z++)
    [loop] for (int y = -1; y <= 1; y++)
    [loop] for (int x = -1; x <= 1; x++)
    {
        float3 offset = float3(x, y, z);
        float3 random = PlanetHash(cell + offset);
        float enabled = step(1.0 - _CraterDensity, PlanetHash(cell + offset + 51.7).x);
        float radius = lerp(0.22, 0.48, random.z) * _CraterSize;
        float d = length(offset + 0.15 + random * 0.7 - local) / max(radius, 0.001);
        float cavity = saturate(1.0 - d * d);
        bowl = max(bowl, cavity * cavity * enabled);
        float rimShape = 1.0 - smoothstep(0.0, 0.22, abs(d - 0.98));
        rim = max(rim, rimShape * enabled);
    }
    return -bowl * _CraterDepth + rim * _CraterRim;
}

float PlanetHeight(float3 direction)
{
    float3 seed = PlanetSeedOffset();
    float height = PlanetFractal(direction * _TerrainScale + seed) * _TerrainHeight;
    if (_MountainHeight > 0.0)
    {
        float ridge = 1.0 - abs(PlanetFractal(direction * _MountainScale + seed + 31.7));
        height += (ridge * ridge * ridge - 0.5) * _MountainHeight;
    }
    if (_Basins > 0.5 && _BasinDepth > 0.0)
    {
        float basin = PlanetNoise(direction * _BasinScale + seed + 67.1);
        height -= (1.0 - smoothstep(_BasinCoverage - 0.12, _BasinCoverage + 0.12, basin)) * _BasinDepth;
    }
    if (_Canyons > 0.5 && _CanyonDepth > 0.0)
    {
        float3 p = direction * _CanyonScale + seed;
        float3 warp = float3(PlanetNoise(p + 13.4), PlanetNoise(p + 51.2), PlanetNoise(p + 93.8));
        float valley = abs(PlanetNoise(p + (warp - 0.5) * _CanyonWarp) * 2.0 - 1.0);
        height -= (1.0 - smoothstep(_CanyonWidth * 0.2, _CanyonWidth, valley)) * _CanyonDepth;
    }
    if (_Craters > 0.5 && _CraterDensity > 0.0)
        height += PlanetCraterHeight(direction * _CraterScale + seed + 107.3);
    return clamp(height, -1.0, 1.0);
}

float PlanetRadiusScale(float height)
{
    // The original sphere is the outer envelope: displacement stays within its culling bounds.
    // _Displacement is the maximum depression as a fraction of the source sphere radius.
    return 1.0 - _Displacement * (0.5 - height * 0.5);
}

float3 PlanetDisplace(float3 positionOS)
{
    float radius = max(length(positionOS), 0.00001);
    float3 direction = positionOS / radius;
    return positionOS * PlanetRadiusScale(PlanetHeight(direction));
}

void PlanetSurface(float3 direction, out half3 albedo, out float3 normalOS, out half smoothness)
{
    direction = normalize(direction);
    float3 dx = ddx(direction);
    float3 dy = ddy(direction);
    float3 gradientX = cross(dy, direction);
    float3 gradientY = cross(direction, dx);
    float determinant = dot(dx, gradientX);
    float inverseDeterminant = sign(determinant) / max(abs(determinant), 1e-12);
    // Screen derivatives reuse neighbouring pixels: evaluate the costly height field only once.
    float height = PlanetHeight(direction);
    float3 slopeGradient = (gradientX * ddx(height) + gradientY * ddy(height)) * inverseDeterminant
        * (0.5 * _Displacement / max(PlanetRadiusScale(height), 0.01));
    float3 terrainNormal = normalize(direction - slopeGradient);
    float slope = 1.0 - saturate(dot(terrainNormal, direction));
    float elevation = height * 0.5 + 0.5;
    float colorBlend = smoothstep(_ColorHeight - _ColorBlend, _ColorHeight + _ColorBlend, elevation);
    albedo = lerp(_LowColor.rgb, _HighColor.rgb, colorBlend);
    float rocks = smoothstep(max(0.0, _RockSlope - 0.08), _RockSlope + 0.08, slope);
    albedo = lerp(albedo, _RockColor.rgb, rocks);
    smoothness = _Smoothness;
    normalOS = normalize(direction - slopeGradient * _NormalStrength);

    if (_Grass > 0.5 && _GrassCoverage > 0.0)
    {
        float3 seed = PlanetSeedOffset();
        float patches = PlanetNoise(direction * _GrassScale + seed + 211.1);
        float grass = smoothstep(1.0 - _GrassCoverage - 0.08, 1.0 - _GrassCoverage + 0.08, patches);
        grass *= smoothstep(_GrassMinHeight - 0.04, _GrassMinHeight + 0.04, elevation);
        grass *= 1.0 - smoothstep(_GrassMaxHeight - 0.04, _GrassMaxHeight + 0.04, elevation);
        grass *= 1.0 - smoothstep(max(0.0, _GrassMaxSlope - 0.06), _GrassMaxSlope + 0.06, slope);

        float detailFade = 1.0 - smoothstep(0.3, 1.0, max(length(dx), length(dy)) * _GrassDetail);
        float detail = lerp(0.5, PlanetNoise(direction * _GrassDetail + seed), detailFade);
        half3 grassColor = lerp(_GrassDryColor.rgb, _GrassColor.rgb, saturate(patches * 0.7 + detail * 0.3));
        albedo = lerp(albedo, grassColor * lerp(0.86, 1.1, detail), grass);
        smoothness = lerp(smoothness, _GrassSmoothness, grass);
        // Microrelief changes lighting only; it adds no geometry to the planet.
        float3 grassGradient = (gradientX * ddx(detail) + gradientY * ddy(detail)) * inverseDeterminant;
        normalOS = normalize(normalOS - grassGradient * (_GrassBump * grass / max(_GrassDetail, 1.0)));
    }
}

#endif
