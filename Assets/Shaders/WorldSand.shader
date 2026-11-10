Shader "TurtleGame/World Sand"
{
    Properties
    {
        _BaseMap ("Sand Color", 2D) = "white" {}
        _BaseColor ("Sand Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Sand Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.119
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WorldSandVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define _NORMALMAP 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            Varyings WorldSandVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
                // LitPassVertex applies BaseMap tiling/offset to these world coordinates.
                input.texcoord = worldPosition.xz;
                // Ignore the imported UV tangent basis so rotated nest meshes match Terrain.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 tangentWS = cross(float3(0, 0, 1), normalWS);
                if (dot(tangentWS, tangentWS) < 0.0001)
                    tangentWS = float3(-1, 0, 0);
                input.tangentOS = float4(TransformWorldToObjectDir(-normalize(tangentWS)), -GetOddNegativeScale());
                return LitPassVertex(input);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
