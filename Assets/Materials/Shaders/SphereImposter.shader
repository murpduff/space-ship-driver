        
Shader "Custom/SphereImposter"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _DeepColor("Deep Color", Color) = (1, 1, 1, 1)
        _ShallowColor("Shallow Color", Color) = (1, 1, 1, 1)
        _FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        _DepthWeight("Depth Weight", Float) = 1
        _DepthFade("Depth Fade", Float) = 1
        _MaxVisibleDepth("Max Visible Depth", Float) = 1
        _Smoothness("Smoothness", Float) = 1
        _NormalSampleMag("Normal Sample Magnitude", Float) = 1
        _NoiseScale("Noise Scale", Float) = 1
        _NoiseAmp("Noise Amplitude", Float) = 1
        _NoiseSpeed("Noise Speed", Float) = 1
        _FoamWeight("Foam Weight", Float) = 1
        _FoamFade("Foam Fade", Float) = 1

        _UP("UP", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+500" "RenderPipeline" = "UniversalPipeline"} 

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Scripts/PlanetGeneration/noiseSimplex.cginc"
            #include "RaySphereIntersection.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _SharedSphereRadius;
                float4 _MainTex_ST;
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _FoamColor;
                float _DepthWeight;
                float _DepthFade;
                float _MaxVisibleDepth;
                float _NormalSampleMag;
                float _NoiseScale;
                float _NoiseAmp;
                float _Smoothness;
                float _NoiseSpeed;
                float _FoamWeight;
                float _FoamFade;
                
                float _UP;
            CBUFFER_END
            
            struct Attributes{
                float4 positionOS : POSITION;
            };

            struct Varyings{
                //clipSpace transform
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            struct FragmentOutput{
                float4 color : SV_Target0;
                float dst : SV_Target1;
            };

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.worldPos);
                return OUT;
            }

            float getDepth(float2 uv, float3 hit){
                float2 screenUV = uv;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                
                float3 viewPos = mul(UNITY_MATRIX_V, float4(hit, 1.0)).xyz;
                float waterEyeDepth = -viewPos.z;

                float waterDepth = sceneEyeDepth - waterEyeDepth;
                return waterDepth;
            }

            float noise(float3 pos){
                float3 p = pos * _NoiseScale;
                float time = _Time.y * _NoiseSpeed;

                float noiseA = snoise(p + time);
                float noiseB = snoise((p * 2.5) + (noiseA * 0.5) - (time * 1.5));
                float noiseC = snoise((p * 8.0) + time * 2.0);

                float finalNoise = (noiseA * 0.5) + (noiseB * 0.25) + (noiseC * 0.125);
                return finalNoise;
            }

            float3 calculateNormal(float3 normal, float3 pos){
                float sample = _NormalSampleMag;
                float3 worldUp = float3(0, 1, 0);
                float3 localRight = normalize(cross(worldUp, normal));
                float3 localUp = normalize(cross(worldUp, localRight)); 

                float hHeightDif = (noise(pos + localRight * sample) - noise(pos - localRight * sample)) / (sample * 2.0) * _NoiseAmp;
                float vHeightDif = (noise(pos + localUp * sample) - noise(pos - localUp * sample)) / (sample * 2.0) * _NoiseAmp;

                return normalize(normal + localRight * hHeightDif + localUp * vHeightDif);
            }

            float specularLighting(float3 normal, float3 rayDir, float3 lightDir){
                float3 viewDir = -rayDir;

                float3 halfVec = normalize(lightDir + viewDir);

                float specPower = exp2(10.0 * _Smoothness + 1.0);
                float specularDot = saturate(dot(normal, halfVec));
                return pow(specularDot, specPower);
            }

            float getFoamWeight(float d){
                float foamMask = saturate(1.0 - (d / _FoamWeight));
                return foamMask;
            }

            FragmentOutput frag(Varyings IN, out float depth : SV_Depth){ 
                FragmentOutput outData;

                float3 rayDir = normalize(IN.worldPos - _WorldSpaceCameraPos);

                float nearClipDst = _ProjectionParams.y;
                float3 rayOrigin = _WorldSpaceCameraPos + rayDir * nearClipDst;

                float radius = _SharedSphereRadius;
                float3 spherePos = /* GetObjectToWorldMatrix()._m03_m13_m23 + */ float3(_WorldSpaceCameraPos.x, -radius, _WorldSpaceCameraPos.z);

                float rayHitSphere = sphIntersect(rayOrigin, rayDir, spherePos, radius).t1;

                float3 sphereHitWorldPos = rayOrigin + rayDir * rayHitSphere;

                //SpherePointInfo pointInfo = sphIntersect(rayOrigin, rayDir, spherePos, radius + noise(sphereHitWorldPos) * _UP);

                float rayHit = rayHitSphere;

                if(rayHit < 0.0){
                    // if(rayHitSphere < 0.0){
                    //     discard;
                    // }else{
                    //     rayHit = rayHitSphere;
                    //}

                    discard;
                };

                float3 hitWorldPos = rayOrigin + rayDir * rayHit;
                float3 normal = normalize(hitWorldPos - spherePos);

                normal = calculateNormal(normal, sphereHitWorldPos);

                Light mainLight = GetMainLight();
                float d = saturate(dot(normal, mainLight.direction));
                float3 lightColor = mainLight.color * d;
                float specularColor = mainLight.color * specularLighting(normal, rayDir, mainLight.direction) * _Smoothness;

                /* */
                float2 screenUV = IN.positionCS.xy / _ScreenParams.xy; 
                float waterDepth = getDepth(screenUV, hitWorldPos);
            
                if(waterDepth < -0.01) discard;

                float depthWeight = saturate(waterDepth / _MaxVisibleDepth);
                float foamWeight = getFoamWeight(depthWeight);
                /* */

                float4 clipPos = TransformWorldToHClip(hitWorldPos);
                depth = clipPos.z / clipPos.w;

                float2 uv = float2(atan2(normal.z, normal.x) / (3.14159 * 2.0), acos(-normal.y) / 3.14159);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * lerp(_ShallowColor, _DeepColor, depthWeight);
                col = lerp(col, _FoamColor, foamWeight);

                //float noiseVal = snoise(hitWorldPos);

                outData.color = float4(col * lightColor + specularColor, lerp(max(saturate(depthWeight * _DepthFade), 0.3), 1.0, foamWeight));
                outData.dst = rayHitSphere;

                return outData;
            }

            ENDHLSL 
        }
    }
}


