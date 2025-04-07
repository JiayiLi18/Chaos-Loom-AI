Shader "Custom/ToonGradientShader_Old"
{
    Properties
    {
        [MainTexture] _MainTex("Albedo (RGB)", 2D) = "white" {}
        [NormalMap] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalMap_ST("Normal Map Tiling and Offset", Vector) = (1, 1, 0, 0) // Add this property
        _ColorA("Color A", Color) = (1, 0, 0, 1)
        _ColorB("Color B", Color) = (0, 0, 1, 1)
        _GradientScale("Gradient Scale", Range(0, 2)) = 1.0
        _GradientOffset("Gradient Offset", Range(-1, 1)) = 0.0
        _HighlightThreshold("Highlight Threshold", Range(-1, 2)) = 0.5
        _HighlightSmoothness("Highlight Smoothness", Range(0, 1)) = 0.5
        _ShadowThreshold("Shadow Threshold", Range(-1, 1)) = 0.5
        _ShadowSmoothness("Shadow Smoothness", Range(0, 0.2)) = 0.02
        _ShadowColor("Shadow Color", Color) = (0.2, 0.2, 0.2, 1)
        _ShadowColor2("Shadow Color 2", Color) = (0.2, 0.2, 0.2, 1)
        [Toggle] _ReceiveShadows("Receive Shadows", Float) = 1.0
        
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ShadowCaster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}


            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // Main Lighting Pass (Fixing shadow blending issue)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Ensure all shadow macros are enabled
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS :   NORMAL;
                float2 uv :         TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv :          TEXCOORD0;
                float3 normalWS :    TEXCOORD1;
                float3 positionWS :  TEXCOORD2;
                float3 positionOS :  TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;  
            float4 _MainTex_ST;
            float4 _NormalMap_ST; 
            float4 _ColorA;
            float4 _ColorB;
            float _GradientScale;
            float _GradientOffset;
            float _HighlightThreshold;
            float _HighlightSmoothness;  
            float _ShadowThreshold;
            float _ShadowSmoothness;
            float4 _ShadowColor;
            float4 _ShadowColor2;
            float _ReceiveShadows; 

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                // 获取阴影坐标
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Sample the albedo texture and force opacity
                float4 albedo = tex2D(_MainTex, IN.uv);
                albedo.a = 1.0;

                // Apply Tiling and Offset for normal map
                float2 normalUV = TRANSFORM_TEX(IN.uv, _NormalMap);  // Apply Tiling and Offset

                // Sample the normal map
                float3 normal = tex2D(_NormalMap, normalUV).rgb;
                normal = normalize(normal * 2.0 - 1.0); // Convert normal map to [-1, 1]
                // Ensure the normal is transformed correctly to world space for proper lighting calculation
                normal = normalize(TransformObjectToWorldNormal(normal));
                normal = normal * 0.5;

                // Combine the normal map with the original normal (apply normal map effect)
                float3 finalNormal = normalize(IN.normalWS + normal); // Add normal map to world space normal

                // 基于模型坐标的渐变
                float gradient = saturate(IN.positionOS.y * _GradientScale + _GradientOffset);
                float4 gradientColor = lerp(_ColorA, _ColorB, gradient);

                // 混合颜色
                float4 finalColor = albedo * gradientColor;
                finalColor.a = 1.0;


                // 使用 URP 中的获取主光源的宏
                Light mainLight = GetMainLight(IN.shadowCoord); // 获取主光源
                float3 lightDir = normalize(mainLight.direction); // 获取光线方向
                float NdotL =  dot(finalNormal, lightDir);

                // 硬边Toon阴影计算
                float toonRamp = smoothstep(
                    _ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness,
                    NdotL
                );
                float toonHighlightRamp = smoothstep(
                    _HighlightThreshold - _HighlightSmoothness,
                    _HighlightThreshold + _HighlightSmoothness,
                    NdotL
                );
                

                // 采样投射阴影
                float shadowAtten = 1.0;
                #if defined(_MAIN_LIGHT_SHADOWS)
                    shadowAtten = MainLightRealtimeShadow(IN.shadowCoord);
                #endif
                // 分离URP阴影衰减和Toon阴影
                shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _ReceiveShadows);
                shadowAtten = smoothstep(0, _ShadowSmoothness*5+0.05, shadowAtten);  // 平滑投射阴影的衰减
                // 将自阴影（toonRamp）和投射阴影（shadowAtten）结合
                float combinedShadow = toonRamp * shadowAtten;
                float combinedShadow2 = toonHighlightRamp * shadowAtten;

                float3 shadowColor = lerp(_ShadowColor2.rgb, _ShadowColor.rgb, combinedShadow);
                shadowColor = lerp(shadowColor, float3(2, 2, 2), combinedShadow2);

                // 最终颜色混合（确保阴影衰减和Toon阴影正确叠加）
                finalColor.rgb *= shadowColor * mainLight.color;

                return finalColor;
            }
            ENDHLSL
        }
    }
}