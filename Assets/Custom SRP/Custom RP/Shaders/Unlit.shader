Shader "Custom RP/Unlit"
{   
    //define material prop
    Properties {
        _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }

    SubShader {
        //posible to have other type of non-HLSL code in pass block 
        Pass { 
            //HLSL key words  for HLSL script 
            HLSLPROGRAM
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
    }
}
