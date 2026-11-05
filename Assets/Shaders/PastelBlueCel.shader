Shader "TurtleGame/Pastel Blue Cel"
{
    Properties
    {
        _Strength ("Effect Strength", Range(0, 1)) = 1
        _ShadowColor ("Blue Shadows", Color) = (0.32, 0.46, 0.68, 1)
        _MidColor ("Pastel Blue", Color) = (0.58, 0.75, 0.9, 1)
        _LightColor ("Ice Blue Highlights", Color) = (0.88, 0.95, 1, 1)
        _Exposure ("Brightness", Range(0.25, 4)) = 1.65
        _Bands ("Cel Bands", Range(2, 8)) = 4
        _BandSoftness ("Band Softness", Range(0.01, 0.49)) = 0.16
        _OriginalColor ("Original Color Detail", Range(0, 1)) = 0.08
        _OutlineColor ("Blue Outline", Color) = (0.23, 0.35, 0.55, 1)
        _OutlineStrength ("Outline Strength", Range(0, 1)) = 0.45
        _OutlineWidth ("Outline Width (Pixels)", Range(0.5, 3)) = 1
        _OutlineThreshold ("Outline Depth Threshold", Range(0.005, 0.2)) = 0.035
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Pastel Blue Cel"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half4 _MidColor;
                half4 _LightColor;
                half4 _OutlineColor;
                float _Strength;
                float _Exposure;
                float _Bands;
                float _BandSoftness;
                float _OriginalColor;
                float _OutlineStrength;
                float _OutlineWidth;
                float _OutlineThreshold;
            CBUFFER_END

            // Work in perceptual color so the bands stay readable in the night scene.
            half3 ToPerceptual(half3 color)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return saturate(color);
                #else
                    return LinearToSRGB(saturate(color));
                #endif
            }

            half3 FromPerceptual(half3 color)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return color;
                #else
                    return SRGBToLinear(color);
                #endif
            }

            float EyeDepth(float2 uv)
            {
                float depth = SampleSceneDepth(saturate(uv));
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        depth = 1.0 - depth;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, depth);
                }
                return LinearEyeDepth(depth, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // Keep the night sky and its small HDR stars out of the pastel palette.
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.000001)
                        return source;
                #else
                    if (rawDepth >= 0.999999)
                        return source;
                #endif
                half3 original = ToPerceptual(source.rgb);
                float luminance = saturate(dot(original, half3(0.2126, 0.7152, 0.0722)) * _Exposure);

                float intervals = max(round(_Bands) - 1.0, 1.0);
                float scaled = luminance * intervals;
                float softness = max(_BandSoftness, fwidth(scaled));
                float band = (floor(scaled) + smoothstep(0.5 - softness, 0.5 + softness, frac(scaled))) / intervals;
                half3 shadow = ToPerceptual(_ShadowColor.rgb);
                half3 middle = ToPerceptual(_MidColor.rgb);
                half3 highlight = ToPerceptual(_LightColor.rgb);
                half3 pastel = band < 0.5
                    ? lerp(shadow, middle, band * 2.0)
                    : lerp(middle, highlight, band * 2.0 - 1.0);

                // Retain a little texture variation while keeping a shared blue palette.
                pastel = lerp(pastel, original, _OriginalColor);

                float2 texel = _OutlineWidth / _ScaledScreenParams.xy;
                float center = EyeDepth(uv);
                float left = EyeDepth(uv - float2(texel.x, 0));
                float right = EyeDepth(uv + float2(texel.x, 0));
                float down = EyeDepth(uv - float2(0, texel.y));
                float up = EyeDepth(uv + float2(0, texel.y));
                // Second differences reject smooth ground slopes and keep silhouettes.
                float curvature = max(abs(left + right - 2.0 * center), abs(down + up - 2.0 * center));
                float edge = curvature / max(center, 0.1);
                edge = smoothstep(_OutlineThreshold, _OutlineThreshold * 2.0, edge);
                pastel = lerp(pastel, ToPerceptual(_OutlineColor.rgb), edge * _OutlineStrength);

                return half4(lerp(source.rgb, FromPerceptual(pastel), _Strength), source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
