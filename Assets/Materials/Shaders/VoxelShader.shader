Shader "Custom/VoxelShader"
{
    Properties
    {
        _ColourTextures ("Textures", 2DArray) = "" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _Cutoff("Alpha Clipping", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        _SmoothnessSource("Smoothness Source", Float) = 0.0
        _SpecularHighlights("Specular Highlights", Float) = 1.0

        [HideInInspector] _BumpScale("Scale", Float) = 1.0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}

        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        [NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white" {}

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0
    }
    
    SubShader
    {
        
         Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        Pass
        {
            Name "Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]
            
            HLSLPROGRAM
            #pragma target 4.5
            // use "vert" function as the vertex shader
            #pragma vertex VoxelForwardVertex
            // use "frag" function as the pixel (fragment) shader
            #pragma fragment VoxelForwardFragment
            // texture arrays are not available everywhere,
            // only compile shader on platforms where they are
            #pragma require 2darray
            
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            //#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            //#pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            //#pragma multi_compile_fragment _ _SHADOWS_SOFT
            //#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            //#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            //#pragma multi_compile_fragment _ _LIGHT_LAYERS
            //#pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
			#pragma multi_compile _ DOTS_INSTANCING_ON
            //#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            
            #include <HLSLSupport.cginc>
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #define BUMP_SCALE_NOT_SUPPORTED 1
            
            UNITY_DECLARE_TEX2DARRAY(_ColourTextures);
            uniform float _uvSizes[2 * 5]; // Needs to be set from C#
            #include "Assets/Materials/Shaders/VoxelShaderInput.hlsl"
            #include "Assets/Materials/Shaders/VoxelForwardPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            // use "vert" function as the vertex shader
            #pragma vertex shadow_vert
            // use "frag" function as the pixel (fragment) shader
            #pragma fragment shadow_frag
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
			#pragma multi_compile _ DOTS_INSTANCING_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #include <HLSLSupport.cginc>
            UNITY_DECLARE_TEX2DARRAY(_ColourTextures);
            uniform float _uvSizes[2 * 5]; // Needs to be set from C#
            #include "Assets/Materials/Shaders/VoxelShaderInput.hlsl"
            
            // vertex shader inputs
            struct Attributes
            {
                int aData : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // vertex shader outputs
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            
            // vertex shader
            Varyings shadow_vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                
                // Get the chunk-relative position from the first 24 bits
                float3 position = float3(float(IN.aData&(255)), float((IN.aData >> 8)&(255)), float((IN.aData >> 16)&(255)));
                float4 positionWS = mul(UNITY_MATRIX_MVP, float4(position, 1.0)); // Use MVP matrix to get global position
                OUT.positionHCS = positionWS;
                return OUT;
            }
            
            half4 shadow_frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return 0;
            }
            ENDHLSL
        }

        
        // This pass is used when drawing to a _CameraNormalsTexture texture
        // Without it, the skybox has lower depth and this object is not drawn!
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex depth_vert
            #pragma fragment depth_frag
            
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
			#pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #include <HLSLSupport.cginc>
            UNITY_DECLARE_TEX2DARRAY(_ColourTextures);
            uniform float _uvSizes[2 * 5]; // Needs to be set from C#
            #include "Assets/Materials/Shaders/VoxelShaderInput.hlsl"
            
            // vertex shader inputs
            struct Attributes
            {
                int aData : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // vertex shader outputs
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            
            // vertex shader
            Varyings depth_vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                
                // Get the chunk-relative position from the first 24 bits
                float3 position = float3(float(IN.aData&(255)), float((IN.aData >> 8)&(255)), float((IN.aData >> 16)&(255)));
                float4 positionWS = mul(UNITY_MATRIX_MVP, float4(position, 1.0)); // Use MVP matrix to get global position
                OUT.positionHCS = positionWS;
                return OUT;
            }
            
            half4 depth_frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return 0;
            }
            ENDHLSL
        }
    }
    //FallBack "Diffuse"
}
