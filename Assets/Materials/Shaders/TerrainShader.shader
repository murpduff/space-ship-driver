Shader "Custom/TerrainShader"
{
    Properties
    {
        [NoScaleOffset] _DiffuseArray("Diffuse Texture Array", 2DArray) = " "{}
        [NoScaleOffset] _NormalArray("Normal Texture Array", 2DArray) = " "{}
        [NoScaleOffset] _MacroDiffuseArray("Macro Diffuse Texture Array", 2DArray) = " "{}
        [NoScaleOffset] _MacroNormalArray("Macro Normal Texture Array", 2DArray) = " "{}
        _SlopeThresh("Slope Threshold", Float) = 1.0
        _SlopeBlend("Slope Blend", Float) = 1.0
        _Tiling("Triplanar Tiling", Float) = 1.0
        _Blend("Triplanar Blend", Float) = 1.0
        _NormalStrength("Normal Strength", Float) = 1.0
        _NormalMacroScale("Normal Macro Scale", Float) = 1.0
        _NormalMacroStrength("Normal Macro Strength", Float) = 1.0
        _MacroFalloffDst("Macro Falloff Distance", Float) = 1.0
        _MacroFalloffPow("Macro Falloff Power", Float) = 1.0
        [Toggle] _IncludeMacro("Include Macro", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // 1. MAIN COLOR PASS
        Pass {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
        
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD0;
            };

            TEXTURE2D_ARRAY(_DiffuseArray);
            SAMPLER(sampler_DiffuseArray);
            TEXTURE2D_ARRAY(_NormalArray);
            SAMPLER(sampler_NormalArray);
            TEXTURE2D_ARRAY(_MacroDiffuseArray);
            SAMPLER(sampler_MacroDiffuseArray);
            TEXTURE2D_ARRAY(_MacroNormalArray);
            SAMPLER(sampler_MacroNormalArray);

            CBUFFER_START(UnityPerMaterial)
                float _SlopeThresh;
                float _SlopeBlend;
                float _Tiling; 
                float _Blend;
                float _NormalStrength;
                float _NormalMacroScale;
                float _NormalMacroStrength;
                float _MacroFalloffDst;
                float _MacroFalloffPow;
                float _IncludeMacro;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

                return OUT;
            }

            float4 SampleTriplanar(TEXTURE2D_ARRAY_PARAM(texArray, sampler_texArray), float3 worldPos, float3 worldNormal, float blend, float tiling, float index)
            {
                float3 triW = abs(worldNormal);
                triW /= triW.x + triW.y + triW.z;
                float4 colX = SAMPLE_TEXTURE2D_ARRAY(texArray, sampler_texArray, worldPos.zy * tiling, index);
                float4 colY = SAMPLE_TEXTURE2D_ARRAY(texArray, sampler_texArray, worldPos.xz * tiling, index);
                float4 colZ = SAMPLE_TEXTURE2D_ARRAY(texArray, sampler_texArray, worldPos.xy * tiling, index);

                return colX * triW.x + colY * triW.y + colZ * triW.z;
            }

            //Thanks gemini

            float3x3 GetTBN(float3 worldNormal) {
                // We use World Forward as the reference to keep flat ground stable
                float3 reference = float3(0, 0, 1);
                
                // If the normal is pointing straight at the reference, 
                // we nudge the reference to avoid a zero-length cross product
                if (abs(dot(worldNormal, reference)) > 0.99) {
                    reference = float3(0, 1, 0);
                }

                float3 tangent = normalize(cross(reference, worldNormal));
                float3 bitangent = cross(worldNormal, tangent);

                // Create a matrix to transform from Tangent Space to World Space
                return float3x3(tangent, bitangent, worldNormal);
            }

            float3 CalculateNormal(float4 normalMap, float strength){
                float3 localNormal = UnpackNormal(normalMap);
                localNormal.xy *= strength;
                localNormal = normalize(localNormal);

                return localNormal;
            }

            float3 BlendNormalsWeighted(float3 nBase, float3 nDetail, float weight) {
                // 1. Fade the detail normal toward "flat" (0,0,1) based on weight
                float3 weightedDetail = lerp(float3(0, 0, 1), nDetail, weight);

                // 2. Whiteout Blending (Stable for macro-scale)
                float3 r;
                r.xy = nBase.xy + weightedDetail.xy;
                r.z  = nBase.z * weightedDetail.z;
                
                return normalize(r);
            }

            //IMPLEMENT TERRAIN VERTEX CURVATURE

            float4 frag (Varyings IN) : SV_Target
            {

                int mat = 0;

                float3 normalWS = normalize(IN.normalWS);

                float slope = 1 - saturate(dot(normalWS, float3(0, 1, 0)));
                //mat = (slope > _SlopeThresh) ? 1.0 : 0.0;

                // mat = (pos.y > sandThresh) ? 0 : 2;
 
                float4 flatColor = SampleTriplanar(TEXTURE2D_ARRAY_ARGS(_DiffuseArray, sampler_DiffuseArray), IN.positionWS, normalWS, _Blend, _Tiling, 0.0);
                float4 slopeColor = SampleTriplanar(TEXTURE2D_ARRAY_ARGS(_DiffuseArray, sampler_DiffuseArray), IN.positionWS, normalWS, _Blend, _Tiling, 1.0);

                float4 triColor = lerp(flatColor, slopeColor, saturate(saturate(slope - _SlopeThresh) * _SlopeBlend));

                float4 microMap = SampleTriplanar(TEXTURE2D_ARRAY_ARGS(_NormalArray, sampler_NormalArray), IN.positionWS, normalWS, _Blend, _Tiling, mat);
                float4 macroMap = SampleTriplanar(TEXTURE2D_ARRAY_ARGS(_MacroNormalArray, sampler_MacroNormalArray), IN.positionWS / _NormalMacroScale, normalWS, _Blend, _Tiling, mat);
                
                float3 worldNormal;
                if(_IncludeMacro == 1.0){
                    float3 tsMicro = CalculateNormal(microMap, _NormalStrength);
                    float3 tsMacro = CalculateNormal(macroMap, _NormalMacroStrength);
                    float3 finalNormal;

                    // 3. Calculate distance weight
                    float dist = distance(_WorldSpaceCameraPos.xz, IN.positionWS.xz);
                    float falloff = saturate(pow(dist / _MacroFalloffDst, _MacroFalloffPow));

                    // 4. Blend them in Tangent Space
                    float3 blendedTSNormal = (_IncludeMacro == 1.0) 
                        ? BlendNormalsWeighted(tsMicro, tsMacro, falloff) 
                        : tsMicro;

                    // 5. Transform to World Space using a stable TBN
                    float3x3 TBN = GetTBN(normalize(normalWS)); // The mesh/terrain normal
                    worldNormal = normalize(mul(blendedTSNormal, TBN));
                }else{
                    worldNormal = normalWS;
                }

                // 6. Final Lighting
                float diffuse = saturate(dot(worldNormal, GetMainLight().direction));
                float4 finalColor = triColor * diffuse * float4(GetMainLight().color, 1);

                return finalColor;
            }

            ENDHLSL
        }



        // 2. DEPTH ONLY (For Scene Depth Debugger)
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag // Keep this...

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                // Apply your curvature math here!
                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            // ...But you MUST define this function for the pragma above to work
            half4 frag(Varyings IN) : SV_Target { 
                return 0; // The GPU ignores this because of ColorMask 0, but the compiler needs it.
            }
            ENDHLSL
        }

        // 3. DEPTH NORMALS (For Normals Debugger)
        Pass {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                // Apply Curvature here too!
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target {
                float3 normal = normalize(IN.normalWS);
                return float4(normal, 0); 
            }
            ENDHLSL
        }
    }
}