
// void AdjustRayDirection_float(float2 ScreenPos, float3 CameraWorldPos, out float3 RayDir, out float3 RayOrigin)
// {
//     // 1. Precise Ray Direction (World Space)
//     float4 clipPos = float4(ScreenPos * 2.0 - 1.0, 1.0, 1.0);
//     float4 viewPos = mul(UNITY_MATRIX_I_P, clipPos);
//     viewPos.z = -1.0;
//     viewPos.w = 0.0;
    
//     // Transform direction to World Space
//     RayDir = normalize(mul(UNITY_MATRIX_I_V, viewPos).xyz);

//     // 2. Precise Origin
//     // We use the Camera's actual World Position
//     RayOrigin = CameraWorldPos;
// }

// void CalculateOcean_float(
//     float2 ScreenPos, 
//     float3 CameraWorldPos, 
//     float3 PlanetWorldPos, 
//     float PlanetRadius, 
//     out float HitDist, 
//     out float3 WorldPos)
// {
//     // 1. GENERATE THE RAY (No external View Vector needed)
//     // Convert 0..1 Screen space to -1..1 Clip space
//     float4 clipPos = float4(ScreenPos * 2.0 - 1.0, 1.0, 1.0);
    
//     // Un-project from the lens to View Space
//     float4 viewPos = mul(UNITY_MATRIX_I_P, clipPos);
//     viewPos.z = -1.0;
//     viewPos.w = 0.0;
    
//     // Transform from View Space to World Space
//     float3 rayDir = normalize(mul(UNITY_MATRIX_I_V, viewPos).xyz);
//     float3 rayOrigin = CameraWorldPos;

//     // 2. THE INTERSECTION MATH
//     HitDist = -1.0;
//     WorldPos = float3(0,0,0);

//     float3 relOrigin = rayOrigin - PlanetWorldPos;
//     float b = dot(relOrigin, rayDir);
//     float c = dot(relOrigin, relOrigin) - (PlanetRadius * PlanetRadius);
//     float d = b * b - c; // The Discriminant

//     if (d >= 0.0) {
//         float t = -b - sqrt(d); 
//         if (t > 0.0) {
//             HitDist = t;
//             // This WorldPos is now perfectly stable and non-warped
//             WorldPos = rayOrigin + (rayDir * t);
//         }
//     }
// }

void CalculateOcean_float(
    float2 ScreenPos, 
    float3 PlanetWorldPos, 
    float PlanetRadius, 
    out float HitDist, 
    out float3 WorldPos)
{
    // 1. Generate Ray Direction
    float4 clipPos = float4(ScreenPos * 2.0 - 1.0, 1.0, 1.0);
    float4 viewPos = mul(UNITY_MATRIX_I_P, clipPos);
    viewPos.z = -1.0;
    viewPos.w = 0.0;
    float3 rayDir = normalize(mul(UNITY_MATRIX_I_V, viewPos).xyz);
    
    // 2. Use the engine's internal camera position (most stable)
    float3 rayOrigin = _WorldSpaceCameraPos; 

    // 3. Intersection Math
    HitDist = -1.0;
    WorldPos = float3(0,0,0);
    float3 relOrigin = rayOrigin - PlanetWorldPos;
    relOrigin.y = -relOrigin.y;
    
    float b = dot(relOrigin, rayDir);
    float c = dot(relOrigin, relOrigin) - (PlanetRadius * PlanetRadius);
    float d = b * b - c;

    if (d >= 0.0) {
        float t = -b - sqrt(d); 
        if (t > 0.0) {
            HitDist = t;
            WorldPos = rayOrigin + (rayDir * t);
        }
    }
}