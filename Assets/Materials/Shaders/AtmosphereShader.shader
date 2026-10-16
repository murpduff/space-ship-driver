Shader "Custom/AtmosphereShader"
{
    Properties
    {
        _SunColor("Sun Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _SunIntensity("Sun Intensity", Float) = 1.0
        _SunRadius("Sun Radius", Float) = 1.0
        _SunCutoff("Sun Cutoff", Float) = 1.0
        _NumInScatteringPoints("In Scattering Points", Float) = 0.0
        _NumOpticalDepthPoints("Optical Depth Points", Float) = 0.0
        _DensityFalloff("Density Falloff", Float) = 1.0
        _ShadowStrength("Shadow Strength", Float) = 1.0
        _ShadowLength("Shadow Length", Float) = 1.0
        _ScatterMultiplier("Scatter Multiplier", Float) = 1.0
        _ScatteringCoefficients("Scattering Coefficients", Vector) = (0, 0, 0)
        _ExtinctionCoefficient("Extinction Coefficient", Float) = 1.0
        _DensityBias("Denisty Bias", Float) = 1.0
        _StarDiminisher("Star Diminisher", Float) = 1.0
        _FogStrength("Fog Strength", Float) = 1.0
        _WorldHeightDim("World Height Diminisher", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "LightMode" = "UniversalForward"}

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "RaySphereIntersection.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewRay : TEXCOORD1;
            };

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            CBUFFER_START(UnityPerMaterial)
                float _SharedSphereRadius;
                float _SharedAtmosHeight;
                half4 _SunColor;
                float _SunRadius;
                float _SunCutoff;
                float _SunIntensity;
                float4 _BaseMap_ST;
                float _NumInScatteringPoints;
                float _NumOpticalDepthPoints;
                float _DensityFalloff;
                float _ShadowStrength;
                float _ShadowLength;
                float _ScatterMultiplier;
                float3 _ScatteringCoefficients;
                float _ExtinctionCoefficient;
                float _DensityBias;
                float _StarDiminisher;
                float _FogStrength;
                float _WorldHeightDim;
                
                float3 _WaveLengths;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);

                // Get the ray pointing from camera to far plane in VIEW SPACE
                float3 viewRay = mul(unity_CameraInvProjection, float4(-(OUT.uv * 2.0 - 1.0), 0.0, 1.0)).xyz;
                
                // Convert to WORLD SPACE (Rotation only)
                OUT.viewRay = mul((float3x3)unity_CameraToWorld, viewRay);

                return OUT;
            }

            SpherePointInfo getDst(float3 rayDir, float radius, float offset){
                SpherePointInfo p;

                float nearClipDst = _ProjectionParams.y;

                float3 spherePos = float3(0.0, offset - _WorldSpaceCameraPos.y, 0.0);

                p = sphIntersect(float3(0,0,0), rayDir, spherePos, radius);

                return p;
            }

            float densityAtPoint(float3 densitySamplePoint) {
                // Since planet is at (0,0,0), length is just the distance from center
                float heightAboveSurface = length(densitySamplePoint) - _SharedSphereRadius;
                float height01 = saturate(heightAboveSurface / (_SharedAtmosHeight * 1.0));
                
                // Standard exponential falloff
                float localDensity = exp(-height01 * _DensityFalloff);
                return localDensity * (1.0 - height01);
            }

            // 2. Total density along a ray
            float opticalDepth(float3 rayOrigin, float3 rayDir, float rayLength) {
                float3 samplePoint = rayOrigin;
                float stepSize = rayLength / (_NumOpticalDepthPoints - 1);
                float totalOpticalDepth = 0;

                for(int i = 0; i < _NumOpticalDepthPoints; i++) {
                    totalOpticalDepth += densityAtPoint(samplePoint) * stepSize;
                    samplePoint += rayDir * stepSize;
                }
                return totalOpticalDepth;
            }

            float phaseFunction(float3 rayDir, float3 sunDir) {
                float cosAngle = dot(rayDir, sunDir);
                // Simple Henyey-Greenstein or Rayleigh phase
                return 3.0 / (16.0 * PI) * (1.0 + cosAngle * cosAngle);
            }

            // 3. The Scattering Loop    IMPORTANT: later should probably add texture optimization (see the atmosphere video)
            float3 calculateLight(float3 rayOrigin, float3 rayDir, float rayLength) {
                float3 sunDir = _MainLightPosition.xyz;
                sunDir *= float3(-1.0, 1.0, -1.0);
                float3 inScatterPoint = rayOrigin;
                float stepSize = rayLength / (_NumInScatteringPoints - 1);
                float3 inScatteredLight = 0;
                
                for(int i = 0; i < _NumInScatteringPoints; i++) {
                    SpherePointInfo sunHitAtmos = sphIntersect(inScatterPoint, sunDir, float3(0,0,0), _SharedSphereRadius + _SharedAtmosHeight);
                    float sunRayLength = sunHitAtmos.t1; 

                    float sunRayOpticalDepth = opticalDepth(inScatterPoint, sunDir, sunRayLength);
                    float viewRayOpticalDepth = opticalDepth(inScatterPoint, -rayDir, stepSize * i);
                    float totalOpticalDepth = (sunRayOpticalDepth + viewRayOpticalDepth);
                    totalOpticalDepth = pow(totalOpticalDepth / (_SharedSphereRadius + _SharedAtmosHeight), _DensityBias) * (_SharedSphereRadius + _SharedAtmosHeight) * (_ExtinctionCoefficient / 1000.0) * _DensityBias;

                    //Create day/night distinction
                    float3 pointNormal = normalize(inScatterPoint);
                    float dotSun = dot(pointNormal, sunDir);

                    float shadowMask = smoothstep(-_ShadowLength, _ShadowLength, dotSun);

                    // C: Transmittance - Beer's Law
                    float3 transmittance = exp(-totalOpticalDepth * _ScatteringCoefficients);
                    
                    float localDensity = densityAtPoint(inScatterPoint);
                    float scatterLight = localDensity * _ScatteringCoefficients * stepSize * shadowMask;
                    inScatteredLight += scatterLight * transmittance;
                    inScatterPoint += rayDir * stepSize;
                }
                
                return saturate(inScatteredLight * (_ScatterMultiplier / 1000.0) * _SunIntensity * _ScatteringCoefficients * phaseFunction(rayDir, sunDir));
            }

            float sunValue(float3 rayDir){
                float3 sunDir = _MainLightPosition.xyz;
                sunDir *= float3(-1.0, 1.0, -1.0);

                return pow(saturate(dot(rayDir, sunDir)), _SunRadius) * _SunCutoff;
            }

            half4 frag(Varyings IN) : SV_Target {
                float2 uv = IN.uv;
                float3 rayDir = normalize(IN.viewRay);
                rayDir.y *= -1.0;

                float rawDepth = SampleSceneDepth(uv);
                float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                SpherePointInfo oceanP = getDst(rayDir, _SharedSphereRadius, -_SharedSphereRadius);
                float t = (oceanP.t1 <= 0) ? _ProjectionParams.z : oceanP.t1;
                linearDepth = min(t, linearDepth);

                // Relative Camera Position (Planet is at 0,0,0)
                // We assume the camera is at height H above the planet surface
                float3 ro = float3(0, _WorldSpaceCameraPos.y / _WorldHeightDim + _SharedSphereRadius, 0);

                // Get intersections with Atmosphere Shell
                SpherePointInfo atmP = sphIntersect(ro, rayDir, float3(0,0,0), _SharedSphereRadius + _SharedAtmosHeight);
                
                float3 finalAtmos = 0;
                float dstThroughAtm = 0;

                float start = 0;
                float end = 0;
                if(atmP.t1 > 0) {
                    if(atmP.submerged == 0){
                        start = max(0, atmP.t1);
                        end = min(atmP.t2, linearDepth);

                        end = min(linearDepth, atmP.t2);
                        if(oceanP.t1 > 0) end = min(end, oceanP.t1);
                    }else{
                        start = 0;
                        end = min(linearDepth, atmP.t1);

                        end = min(linearDepth, atmP.t1);
                        if(oceanP.t1 > 0) end = min(end, oceanP.t1);
                    }
                }
                float pathLength = max(0, end - start);

                float4 sunColor;

                if (pathLength > 0) {
                    finalAtmos = (calculateLight(ro + rayDir * start, rayDir, pathLength));
                    if(dot(rayDir, _MainLightPosition.xyz) == 0){
                        sunColor = float4(_SunColor.rgb * finalAtmos, 1.0);
                    }
                }

                float depth01 = linearDepth / _ProjectionParams.z;

                float sunVal = 0;
                if(depth01 > 0.95) sunVal = sunValue(rayDir);
                else sunVal = 0;

                float4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                float atmStrength = pow(saturate(linearDepth / _ProjectionParams.z), _FogStrength);
                float atmBrightness = length(finalAtmos.xyz) * _StarDiminisher;
                finalAtmos += sunColor * sunVal;

                return half4((1 - saturate(atmStrength * atmBrightness)) * sceneColor.rgb + saturate(atmStrength * atmBrightness) * finalAtmos, 1.0);
            }

            ENDHLSL 
        }
    }
}
