#ifndef CATWALK_PLANET_TESSELLATION_INCLUDED
#define CATWALK_PLANET_TESSELLATION_INCLUDED

// Only included by the hardware tessellation subshader. The fallback has no hull/domain stages.
struct PlanetControlPoint
{
    float4 positionOS : INTERNALTESSPOS;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PlanetTessellationFactors
{
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

PlanetControlPoint PlanetTessellationVertex(PlanetAttributes input)
{
    PlanetControlPoint output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionOS = input.positionOS;
    output.staticLightmapUV = input.staticLightmapUV;
    output.dynamicLightmapUV = input.dynamicLightmapUV;
    return output;
}

float PlanetEdgeFactor(float3 a, float3 b)
{
    float radius = max((length(a) + length(b)) * 0.5, 0.0001);
    float worldRadius = radius * max(length(TransformObjectToWorldDir(float3(1, 0, 0), false)),
        max(length(TransformObjectToWorldDir(float3(0, 1, 0), false)), length(TransformObjectToWorldDir(float3(0, 0, 1), false))));
    float3 midpoint = normalize(a + b) * radius;
    float distanceInRadii = distance(GetCameraPositionWS(), TransformObjectToWorld(midpoint)) / max(worldRadius, 0.0001);
    float fade = 1.0 - smoothstep(_TessellationNear, max(_TessellationNear + 0.01, _TessellationFar), distanceInRadii);
    // Each factor depends only on the shared edge, so neighbouring triangles agree exactly.
    return lerp(1.0, clamp(_Tessellation, 1.0, 16.0), fade);
}

PlanetTessellationFactors PlanetPatchConstants(InputPatch<PlanetControlPoint, 3> patch)
{
    UNITY_SETUP_INSTANCE_ID(patch[0]);
    PlanetTessellationFactors output;
    output.edge[0] = PlanetEdgeFactor(patch[1].positionOS.xyz, patch[2].positionOS.xyz);
    output.edge[1] = PlanetEdgeFactor(patch[2].positionOS.xyz, patch[0].positionOS.xyz);
    output.edge[2] = PlanetEdgeFactor(patch[0].positionOS.xyz, patch[1].positionOS.xyz);
    output.inside = (output.edge[0] + output.edge[1] + output.edge[2]) / 3.0;
    return output;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("PlanetPatchConstants")]
[maxtessfactor(16.0)]
PlanetControlPoint PlanetHull(InputPatch<PlanetControlPoint, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

[domain("tri")]
PlanetVaryings PlanetDomain(PlanetTessellationFactors factors,
    const OutputPatch<PlanetControlPoint, 3> patch, float3 barycentric : SV_DomainLocation)
{
    PlanetAttributes input = (PlanetAttributes)0;
    UNITY_TRANSFER_INSTANCE_ID(patch[0], input);
    float3 p = patch[0].positionOS.xyz * barycentric.x + patch[1].positionOS.xyz * barycentric.y + patch[2].positionOS.xyz * barycentric.z;
    float radius = dot(float3(length(patch[0].positionOS.xyz), length(patch[1].positionOS.xyz), length(patch[2].positionOS.xyz)), barycentric);
    // Project subdivided triangles back onto the sphere before applying the shared height field.
    input.positionOS = float4(normalize(p) * radius, 1);
    input.staticLightmapUV = patch[0].staticLightmapUV * barycentric.x + patch[1].staticLightmapUV * barycentric.y + patch[2].staticLightmapUV * barycentric.z;
    input.dynamicLightmapUV = patch[0].dynamicLightmapUV * barycentric.x + patch[1].dynamicLightmapUV * barycentric.y + patch[2].dynamicLightmapUV * barycentric.z;
    return PlanetVertex(input);
}

#endif
