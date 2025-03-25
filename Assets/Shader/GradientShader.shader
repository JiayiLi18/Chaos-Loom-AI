Shader "Custom/ToonGradientShader"
{
    Properties
    {
        [MainTexture] _MainTex("Albedo (RGB)", 2D) = "white" {}
        _ColorA("Color A", Color) = (1, 0, 0, 1)
        _ColorB("Color B", Color) = (0, 0, 1, 1)
        _GradientScale("Gradient Scale", Range(0, 2)) = 1.0
        _GradientOffset("Gradient Offset", Range(-1, 1)) = 0.0
        _ShadowThreshold("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness("Shadow Smoothness", Range(0, 0.2)) = 0.02
        _ShadowColor("Shadow Color", Color) = (0.2, 0.2, 0.2, 1)
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

        // ShadowCaster Pass (完全兼容URP 14.0.11)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // 关键修复：使用最小化依赖的头文件
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 shadowCoords : TEXCOORD3;
            };

            Varyings ShadowPassVertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                // Get the VertexPositionInputs for the vertex position  
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);

                // Convert the vertex position to a position on the shadow map
                float4 shadowCoordinates = GetShadowCoord(positions);

                // Pass the shadow coordinates to the fragment shader
                OUT.shadowCoords = shadowCoordinates;
                return OUT;
            }

            half4 ShadowPassFragment(Varyings IN) : SV_Target
            {
               // Get the value from the shadow map at the shadow coordinates
               half shadowAmount = MainLightRealtimeShadow(IN.shadowCoords);

               // Set the fragment color to the shadow value
               return shadowAmount;
            }
            ENDHLSL
        }

        // 主光照Pass (修复阴影混合问题)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 确保启用所有阴影宏
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ColorA;
            float4 _ColorB;
            float _GradientScale;
            float _GradientOffset;
            float _ShadowThreshold;
            float _ShadowSmoothness;
            float4 _ShadowColor;
            float _ReceiveShadows;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // 采样贴图并强制不透明
                float4 albedo = tex2D(_MainTex, IN.uv);
                albedo.a = 1.0;

                // 基于模型坐标的渐变
                float gradient = saturate(IN.positionOS.y * _GradientScale + _GradientOffset);
                float4 gradientColor = lerp(_ColorA, _ColorB, gradient);

                // 混合颜色
                float4 finalColor = albedo * gradientColor;
                finalColor.a = 1.0;

                // 关键修复：正确获取阴影坐标
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 计算法线点积光照
                float3 lightDir = mainLight.direction;
                float NdotL = dot(IN.normalWS, lightDir);

                // 硬边Toon阴影计算
                float toonShadow = smoothstep(
                    _ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness,
                    NdotL
                );
                float3 shadowColor = lerp(_ShadowColor.rgb, float3(1, 1, 1), toonShadow);

                // 分离URP阴影衰减和Toon阴影
                float shadowAttenuation = lerp(1.0, mainLight.shadowAttenuation, _ReceiveShadows);

                // 最终颜色混合（确保阴影衰减和Toon阴影正确叠加）
                finalColor.rgb *= shadowColor * shadowAttenuation * mainLight.color;

                return finalColor;
            }
            ENDHLSL
        }
    }
}