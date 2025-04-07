Shader "Custom/ToonGradientShader"
{
    Properties
    {
        _MaskTexture("Mask Texture", 2D) = "black" {}
        [MainTexture] _MainTex("Albedo (RGB)", 2D) = "white" {}
        [NormalMap] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalMap_ST("Normal Map Tiling and Offset", Vector) = (1, 1, 0, 0) // Add this property
        _NormalMapScale("Normal Map Scale", Range(0, 2)) = 1.0
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
            #pragma multi_compile_instancing

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

        // Main Lighting Pass 
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
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma multi_compile _ _SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS :   NORMAL;
                float4 tangentOS : TANGENT; // 新增Tangent输入
                float2 uv :         TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv :          TEXCOORD0;
                float3 normalWS :    TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 positionWS :  TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                float gradient : TEXCOORD6; // 预计算渐变值
            };
            
            TEXTURE2D(_MaskTexture);
            SAMPLER(sampler_MaskTexture);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTexture_ST; // 平铺和偏移参数
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
                float _NormalMapScale;
            CBUFFER_END


            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                // 计算渐变并传递到片段着色器
                float gradient = saturate(IN.positionOS.y * _GradientScale + _GradientOffset);
                OUT.gradient = gradient;

                OUT.positionHCS = positionInputs.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = normalInputs.normalWS;
                OUT.tangentWS = normalInputs.tangentWS;
                OUT.bitangentWS = normalInputs.bitangentWS;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                #if defined(_MAIN_LIGHT_SHADOWS)
                    OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                #else
                    OUT.shadowCoord = GetShadowCoord(positionInputs);
                #endif

                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Sample the albedo texture and force opacity
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                albedo.a = 1.0;

                // 采样法线贴图（修复1：正确UV变换）
                float2 normalUV = TRANSFORM_TEX(IN.uv, _NormalMap);
                float3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV),
                    _NormalMapScale
                );
    
                float3x3 TBN = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // 计算渐变颜色
                float4 gradientColor = lerp(_ColorA, _ColorB, IN.gradient);
                float4 finalColor = albedo * gradientColor;

                // 应用遮罩纹理
                float4 maskData = SAMPLE_TEXTURE2D(_MaskTexture, sampler_MaskTexture, TRANSFORM_TEX(IN.uv, _MaskTexture));
                finalColor.rgb = lerp(finalColor.rgb, maskData.rgb, maskData.a);

                // 获取主光源信息
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float NdotL = dot(normalWS, lightDir);

                // 计算Toon阴影
                float shadowRamp = smoothstep(
                    _ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness,
                    NdotL
                );
                float highlightRamp = smoothstep(
                    _HighlightThreshold - _HighlightSmoothness,
                    _HighlightThreshold + _HighlightSmoothness,
                    NdotL
                );

               // 阴影采样逻辑重构
                float shadowAtten = 1.0;
                #if defined(_MAIN_LIGHT_SHADOWS) && defined(_RECEIVE_SHADOWS)
                    shadowAtten = MainLightRealtimeShadow(IN.shadowCoord);
                #endif

                // 分离URP阴影衰减和Toon阴影
                shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _ReceiveShadows);
                shadowAtten = smoothstep(0, _ShadowSmoothness*5+0.05, shadowAtten);  // 平滑投射阴影的衰减

                // 混合阴影颜色
                float3 shadowColor = lerp(_ShadowColor2.rgb, _ShadowColor.rgb, shadowRamp* shadowAtten);
                shadowColor = lerp(shadowColor, float3(2, 2, 2), highlightRamp* shadowAtten);

                finalColor.rgb *= shadowColor * mainLight.color;
                return finalColor;
            }
            ENDHLSL
        }
    }
}