#include "Assets/Scripts/PlanetGeneration/noiseSimplex.cginc"

float noise(float3 p, float freq, float amp, int octaves){
    float v = 0;

    float f = freq;
    float a = amp;
    for(int i = 0; i < octaves; i++){
        v += snoise(p * f) * a;
        f *= 2.0;
        a /= 2.0;
    }
    return v;
}

float3 getNormal(float3 pos, float freq, float amp, int octaves){
    float dx = 0.01;
    float n = noise(pos, freq, amp, octaves);

    float dhx = (n - noise(pos + float3(dx, 0, 0), freq, amp, octaves)) / dx;
    float dhy = (n - noise(pos + float3(0, 0, dx), freq, amp, octaves)) / dx;

    float3 normal = cross(float3(dhx, 0, 1), float3(0, 0, dhy));
    return normal;
}

void TerrainDetails_float(
    float3 pos, float3 norm, float curveOffset, 
    float4 grassC, float4 grassN, float4 stoneC, float4 stoneN, float4 sandC, float4 sandN, //Textures (details)
    float4 boulder1, float4 boulder2, float boulderScale, float boulderAmp, float fade,
    float normThresh, float sandThresh, float normalStrength, float normVaryFreq, float normVaryAmp,
    out float4 d, out float3 n
){
    const float4 texsD[3] = {grassC, stoneC, sandC};
    const float4 texsN[3] = {grassN, stoneN, sandN};

    pos.y -= curveOffset;
    
    int mat = 0; //0 - grass, 1 - stone, 2 - sand

    float vary = noise(pos, normVaryFreq, normVaryAmp, 8);

    float dotV = dot(norm, float3(0, 1, 0));

    mat = (pos.y > sandThresh) ? 0 : 2;
    mat = (-dotV < normThresh) ? mat : 1;

    d = texsD[mat];
    if(mat == 1){
        float boulderNoise = saturate(pow(noise(pos, boulderScale, boulderAmp, 4), fade));
        float4 boulderColor = lerp(boulder1, boulder2, boulderNoise);
        d += boulderColor;
    }

    n = float3(texsN[mat].xyz);
    if(mat == 1){
        float3 boulderNoiseN = getNormal(pos, boulderScale, boulderAmp, 5); 
        n += boulderNoiseN;
    }
    n.y *= normalStrength;
    n = normalize(n);
}
