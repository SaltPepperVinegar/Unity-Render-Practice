#ifndef  CUSTOM_UNLIT_PASS_INCLUDED
#define  CUSTOM_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

/*
SRP batcher:
    all material properties have to be defined inside a concrete memory buffer 
*/
//also allowing per-instance material data 
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    //segragates _BaseColor by putting it in a speciic constant memory buffer, although it remains accessble at the global level
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor);
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
    float3 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 UnlitPassVertex(Attributes input) : SV_POSITION{
    //SV_POSITION as the position output 
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    return TransformWorldToHClip(positionWS);
}

//float should be used for positions and texture coodinates only and half everything elseif optimizing for mobile 
float4 UnlitPassFragment()  : SV_TARGET {
    //SV_TARGET as the pixel color output 
	return _BaseColor;
} 

 
#endif