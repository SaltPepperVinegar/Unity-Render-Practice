#ifndef  CUSTOM_SHADOWS_INCLUDED
#define  CUSTOM_SHADOWS_INCLUDED
#define MAX_DIRECTIONAL_LIGHT_COUNT 4
//atlas isn't a regular texture
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
//explicit sampler state
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
    float4x4 _DirectionalShadowMatrices[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
};


//samples the shadow atlas via the SAMPLE_TEXTURE2D_SHADOW macro
//pass in atlas, shadow sampler, position in shadow texture space 
float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

//return the shadow attenuation 
//given directional shadow data and a surface defined in world space 
float GetDirectionalShadowAttenuation (DirectionalShadowData data, Surface surfaceWS){
    if (data.strength <= 0.0){
        return 1.0;
    }
    //use the tile offset to retrieve the correct matrix;
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[data.tileIndex],
		float4(surfaceWS.position, 1.0)
	).xyz;
	float shadow = SampleDirectionalShadowAtlas(positionSTS);
	return lerp(1.0, shadow, data.strength);
}
#endif