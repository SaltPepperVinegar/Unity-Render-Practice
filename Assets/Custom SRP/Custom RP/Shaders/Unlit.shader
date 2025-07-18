Shader "Custom RP/Unlit"
{   
    //define material prop
    Properties {
        //alpha map for semitransparent material
        _BaseMap("Texture", 2D) = "white" {}
        [HDR]_BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
		[HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)

    }

    SubShader {
        HLSLINCLUDE 
        #include "../ShaderLibrary/Common.hlsl"
        #include "LitInput.hlsl"
        ENDHLSL
        //posible to have other type of non-HLSL code in pass block 
        Pass { 

            Blend [_SrcBlend] [_DstBlend]
            //control depth is written or not via Zwrite
            Zwrite [_ZWrite]
            //HLSL key words  for HLSL script 
            HLSLPROGRAM
             
            #pragma shader_feature _CLIPPING
            //allowing GPU instancing 
            #pragma multi_compile_instancing

            /*
                vertext kernel/program/shader
                transforming the vertex coordinates from 3D space to 2D visualization space
            */
            //use function UnlitPassVertex in vertex stage
            #pragma vertex UnlitPassVertex


            /*
                fragment kernl/program/shader
                filling all pixels that are covered by the resulting triangle
            */
            //use function UnlitPassFragment in fragment stage
            #pragma fragment UnlitPassFragment

            #include "UnlitPass.hlsl"
            
            ENDHLSL

        }
        
        //depth only rendering pass
        //output depth texture
        Pass{
            Tags {
                "LightMode" = "ShadowCaster"
            }

            ColorMask 0

            HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
        }

        //the editor and lightmappers use when baking
        Pass{
            Tags {
                "LightMode" = "Meta"
            }
            //cullings always be off, 

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "MetaPass.hlsl"
            ENDHLSL
        }
        
    }
    CustomEditor "CustomShaderGUI"

}
