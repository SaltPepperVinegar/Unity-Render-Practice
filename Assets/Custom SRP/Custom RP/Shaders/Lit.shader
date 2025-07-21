Shader "Custom RP/Lit"
{   
    //define material prop
    Properties {
        //alpha map for semitransparent material
        //default texture set to white 
        _BaseMap("Texture", 2D) = "white" {}
        //change the default color to gray, as fully white surface can appear very bright
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha("Premultiply Apha", Float) = 0
        [NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
        _Metallic("Metalllic", Range(0,1)) = 0
        _Occlusion("Occlusion", Range(0,1)) = 1
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Fresnel("Fresnel", Range(0,1)) = 1

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
        [Toggle (_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
		[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)
        _DetailMap("Details", 2D) = "linearGrey" {}
        _DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
        _DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
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
            //used to define certain properties that shader pass will associate with 
            Tags {
                "LightMode" = "CustomLit"
            }
            Blend [_SrcBlend] [_DstBlend]
            //control depth is written or not via Zwrite
            Zwrite [_ZWrite]
            
            //HLSL key words  for HLSL script 
            
            HLSLPROGRAM
            //raising the target level of shader pass to 3.5 
            #pragma target 3.5
            
            #pragma shader_feature _RECEIVE_SHADOWS 
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            //render lightmapped objects with light map on shader variants
            #pragma multi_compile _ LIGHTMAP_ON
            
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            //allowing GPU instancing 
            #pragma multi_compile_instancing

            /*
                vertext kernel/program/shader
                transforming the vertex coordinates from 3D space to 2D visualization space
            */
            //use function LitPassVertex in vertex stage
            #pragma vertex LitPassVertex


            /*
                fragment kernl/program/shader
                filling all pixels that are covered by the resulting triangle
            */
            //use function LitPassFragment in fragment stage
            #pragma fragment LitPassFragment

            #include "LitPass.hlsl"
            
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

            #pragma multi_compile _ LOD_FADE_CROSSFADE

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
