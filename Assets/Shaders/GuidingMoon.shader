Shader "TurtleGame/Guiding Moon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Lunar surface", 2D) = "white" {}
        [MainColor] _BaseColor ("Moon tint", Color) = (0.88, 0.94, 1, 1)
        _Brightness ("Surface glow", Range(0, 5)) = 2.2
        _ShadowBrightness ("Unlit side visibility", Range(0, 1)) = 0.35
        _LightDirection ("Light direction (view space)", Vector) = (-0.45, 0.6, 1, 0)
        [HDR] _RimColor ("Edge glow", Color) = (0.55, 0.75, 1, 1)
        _RimStrength ("Edge glow strength", Range(0, 1)) = 0.18
        _RimPower ("Edge glow falloff", Range(1, 8)) = 4
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "LunarSurface"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RimColor;
                float4 _LightDirection;
                half _Brightness;
                half _ShadowBrightness;
                half _RimStrength;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 normalVS = TransformWorldToViewDir(normalWS, true);
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // A soft, camera-relative phase keeps the guide readable along the whole route.
                half phase = saturate(dot(normalVS, SafeNormalize(_LightDirection.xyz)));
                half illumination = lerp(_ShadowBrightness, 1.0h, phase);
                half3 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                // Texture modulates the self-light so the lunar markings remain visible at night.
                half3 color = surface * _BaseColor.rgb * (_Brightness * illumination);
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirection)), _RimPower);
                color += _RimColor.rgb * (_RimStrength * rim);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
