Shader "TurtleGame/Sand Trail"
{
    Properties
    {
        [MainTexture] _BaseMap ("Ground sand texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Sand tint", Color) = (0.8117648, 0.7019608, 0.5529412, 1)
        _WorldTiling ("Ground sand tiling", Float) = 0.05
        _EdgeSoftness ("Cloud edge softness", Range(0.01, 0.15)) = 0.045
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "SandDust"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _EdgeSoftness;
                float _WorldTiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                // Overlapping round lobes form a compact cartoon dust cloud.
                float cloud = length(centered - float2(-0.4, -0.12)) - 0.36;
                cloud = min(cloud, length(centered - float2(0.0, 0.18)) - 0.48);
                cloud = min(cloud, length(centered - float2(0.42, -0.08)) - 0.34);
                cloud = min(cloud, length(centered - float2(0.02, -0.28)) - 0.36);
                float softness = max(_EdgeSoftness, fwidth(cloud));
                half edge = 1.0 - smoothstep(-softness, softness, cloud);
                half rim = smoothstep(-0.09, -0.035, cloud);
                half shade = lerp(0.96, 1.04, step(-0.05, centered.y));
                half3 textureColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                    input.positionWS.xz * _WorldTiling).rgb;
                Light sunlight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 illumination = SampleSH(half3(0, 1, 0)) + sunlight.color
                    * saturate(sunlight.direction.y) * sunlight.shadowAttenuation;
                half3 sand = textureColor * input.color.rgb * illumination * shade * lerp(1.0, 0.9, rim);
                return half4(MixFog(sand, input.fog), input.color.a * edge);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
