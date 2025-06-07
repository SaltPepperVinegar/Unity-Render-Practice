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
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }

    SubShader {
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
             
            #pragma shader_feature _CLIPPING
            
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
    }
}
