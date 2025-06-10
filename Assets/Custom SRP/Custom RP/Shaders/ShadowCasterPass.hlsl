#ifndef  CUSTOM_LIT_PASS_INCLUDED
#define  CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
/*
SRP batcher:
    all material properties have to be defined inside a concrete memory buffer 
*/
//also allowing per-instance material data 
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    //segragates _BaseColor by putting it in a speciic constant memory buffer, although it remains accessble at the global level
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

//input data to the vertex shader
struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

//intermediate values passed from the vertex shader to the fragment shader
struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowCasterPassVertex(Attributes input){
    //SV_POSITION as the position output 
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);

    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    output.baseUV =  input.baseUV * baseST.xy + baseST.zw;
    return output;
}

void ShadowCasterPassFragment (Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
    #if defined(_CLIPPING)
        clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif
} 

 
#endif