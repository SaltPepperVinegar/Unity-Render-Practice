#ifndef  CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define  CUSTOM_SHADOW_CASTER_PASS_INCLUDED

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
bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes input){
    //SV_POSITION as the position output 
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.baseUV =  TransformBaseUV(input.baseUV);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);
	if (_ShadowPancaking) {
		#if UNITY_REVERSED_Z
			output.positionCS.z = 
				min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
		#else 
			output.positionCS.z = 
				max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
		#endif
	}
    return output;
}

void ShadowCasterPassFragment (Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);
	ClipLOD(input.positionCS.xy, unity_LODFade.x);

	InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);
    #if defined(_SHADOWS_CLIP)
        clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #elif defined(_SHADOWS_DITHER)
		float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
		clip(base.a - dither);
	#endif
} 

 
#endif