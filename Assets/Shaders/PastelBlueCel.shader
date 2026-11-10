Shader "TurtleGame/Cozy World Style"
{
    Properties
    {
        _Strength ("World Style Strength", Range(0, 1)) = 0.85
        _CelStrength ("Soft Cel Shading", Range(0, 1)) = 0.35
        _Bands ("Light Bands", Range(3, 10)) = 5
        _BandSoftness ("Band Transition", Range(0.05, 0.49)) = 0.24
        _Saturation ("Shared Saturation", Range(0, 1.5)) = 0.78
        _PaletteStrength ("Palette Harmony", Range(0, 1)) = 0.38
        _TextureSoftness ("Texture Softness", Range(0, 1)) = 0.28
        _ShadowTint ("Lavender Shadows", Color) = (0.82, 0.83, 1, 1)
        _MidTint ("Peach Midtones", Color) = (1, 0.94, 0.88, 1)
        _HighlightTint ("Cream Highlights", Color) = (1, 0.97, 0.86, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            Name "Cozy World Style"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _CelStrength;
                float _Bands;
                float _BandSoftness;
                float _Saturation;
                float _PaletteStrength;
                float _TextureSoftness;
                half4 _ShadowTint;
                half4 _MidTint;
                half4 _HighlightTint;
            CBUFFER_END

            half3 Perceptual(half3 color)
            {
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    return LinearToSRGB(max(color, 0.0));
                #else
                    return max(color, 0.0);
                #endif
            }

            // Smooth small texture variations without mixing across silhouettes.
            float SceneDistance(float depth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        depth = 1.0 - depth;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, depth);
                }
                return LinearEyeDepth(depth, _ZBufferParams);
            }

            void AccumulateNeighbor(float2 uv, float depth, half3 center,
                inout half3 sum, inout float totalWeight)
            {
                float neighborDepth = SampleSceneDepth(uv);
                float centerEye = SceneDistance(depth);
                float neighborEye = SceneDistance(neighborDepth);
                float depthWeight = saturate(1.0 - abs(centerEye - neighborEye) / max(centerEye * 0.01, 0.00001));
                half3 sampleColor = Perceptual(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb);
                float colorDistance = length(sampleColor - center);
                float weight = depthWeight * exp2(-colorDistance * colorDistance * 160.0);
                sum += sampleColor * weight;
                totalWeight += weight;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (depth <= 0.000001) return source;
                #else
                    if (depth >= 0.999999) return source;
                #endif

                // No geometry in depth means sky: return it byte-for-byte unchanged.
                // Use the source texture dimensions so render scale is respected.
                half3 color = Perceptual(source.rgb);
                half3 sum = color;
                float totalWeight = 1.0;
                float2 texel = _BlitTexture_TexelSize.xy;
                AccumulateNeighbor(saturate(uv + float2(texel.x, 0)), depth, color, sum, totalWeight);
                AccumulateNeighbor(saturate(uv - float2(texel.x, 0)), depth, color, sum, totalWeight);
                AccumulateNeighbor(saturate(uv + float2(0, texel.y)), depth, color, sum, totalWeight);
                AccumulateNeighbor(saturate(uv - float2(0, texel.y)), depth, color, sum, totalWeight);
                color = lerp(color, sum / totalWeight, _TextureSoftness);
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float brightness = saturate(luminance);
                float intervals = max(round(_Bands) - 1.0, 2.0);
                float scaled = brightness * intervals;
                float softness = min(0.49, max(_BandSoftness, fwidth(scaled)));
                float stepped = (floor(scaled) + smoothstep(0.5 - softness,
                    0.5 + softness, frac(scaled))) / intervals;

                // Bound the tonal change: never replace albedo with a flat palette.
                // Fade out in deep shadows and specular highlights to prevent blotches.
                float midtoneMask = smoothstep(0.04, 0.20, brightness)
                    * (1.0 - smoothstep(0.72, 0.98, brightness));
                float delta = clamp(stepped - brightness, -0.045, 0.045)
                    * _CelStrength * midtoneMask;
                float gain = 1.0 + delta / max(luminance, 0.12);
                half3 styled = color * gain;
                float styledLuminance = dot(styled, half3(0.2126, 0.7152, 0.0722));
                styled = lerp(styledLuminance.xxx, styled, _Saturation);
                // A shared split tone coordinates every material without replacing
                // its albedo. Normalize the tint to preserve the scene's brightness.
                half3 shadow = Perceptual(_ShadowTint.rgb);
                half3 middle = Perceptual(_MidTint.rgb);
                half3 highlight = Perceptual(_HighlightTint.rgb);
                half3 tint = lerp(shadow, middle, smoothstep(0.08, 0.48, brightness));
                tint = lerp(tint, highlight, smoothstep(0.48, 0.90, brightness));
                tint /= max(dot(tint, half3(0.2126, 0.7152, 0.0722)), 0.01);
                // Preserve emissive whites and bloom while harmonizing surfaces.
                float paletteMask = 1.0 - smoothstep(0.9, 1.3, luminance);
                styled *= lerp(half3(1, 1, 1), tint, _PaletteStrength * paletteMask);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    styled = SRGBToLinear(max(styled, 0.0));
                #endif
                return half4(lerp(source.rgb, styled, _Strength), source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
