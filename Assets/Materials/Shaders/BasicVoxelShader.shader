Shader "Custom/BasicVoxelShader"
{
    Properties
    {
        _ColourTextures ("Textures", 2DArray) = "" {}
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
            /*Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]*/
            
            HLSLPROGRAM
            #pragma target 4.5
            // use "vert" function as the vertex shader
            #pragma vertex VoxelForwardVertex
            // use "frag" function as the pixel (fragment) shader
            #pragma fragment VoxelForwardFragment
            // texture arrays are not available everywhere,
            // only compile shader on platforms where they are
            #pragma require 2darray
            
            /* GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
			#pragma multi_compile _ DOTS_INSTANCING_ON*/
            //#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            //#pragma multi_compile_instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            //#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            //uniform float _uvSizes[2 * 6]; // Needs to be set from C#

            
            struct Attributes
            {
                int packedData    : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float3 uv          : TEXCOORD0;
                //float3 positionWS  : TEXCOORD1;   
                half3  normalWS    : TEXCOORD1;
                float4 positionHCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            #ifdef DOTS_INSTANCING_ON
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _Color)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
                #define _Color UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Color)
            #endif

            Varyings VoxelForwardVertex(Attributes input)
            {

                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                //float3 positionOS = float3(float(input.packedData&(255)), float((input.packedData >> 8)&(255)), float((input.packedData >> 16)&(255)));
                float4 positionOS = float4(float(input.packedData&(255)), float((input.packedData >> 8)&(255)), float((input.packedData >> 16)&(255)), 1);
                int normal = int((input.packedData >> 29)&(7));
                //float3 normalOS;
                float4 normalOS;
                
                if(normal == 0)
                {
                    normalOS = float4(0,1,0,0);
                }
                else if(normal == 1)
                {
                    normalOS= float4(0,-1,0,0);
                }
                else if(normal == 2)
                {
                    normalOS = float4(1,0,0,0);
                }
                else if(normal == 3)
                {
                    normalOS = float4(-1,0,0,0);
                }
                else if(normal == 4)
                {
                    normalOS = float4(0,0,1,0);
                }
                else
                {
                    normalOS = float4(0,0,-1,0);
                }
                
                //float4 tangentOS = float4(0,0,0,1);
                //VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
                //VertexNormalInputs normalInput = GetVertexNormalInputs(normalOS, tangentOS);
                
                // 5 bits for textureID
                output.uv.z = int((input.packedData >> 24)&(31));
                if (normal < 2)
                {
                    output.uv.xy = positionOS.xz;
                }
                else
                {
                    output.uv.xy  = (normal < 4 ? positionOS.zy : positionOS.xy);
                }
                
                //output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                //output.positionWS.xyz = vertexInput.positionWS;
                output.positionHCS = GetVertexPositionInputs(positionOS.xyz).positionCS;
                output.normalWS = normalOS;

                return output;
            }
            
            //UNITY_DECLARE_TEX2DARRAY(_ColourTextures);
            
            half4 VoxelForwardFragment(Varyings input) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return (1,1,1,1);
            }
            
            
            ENDHLSL
        }
        
        
    }
    
}
