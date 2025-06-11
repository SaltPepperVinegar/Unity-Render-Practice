#ifndef  CUSTOM_SHADOWS_INCLUDED
#define  CUSTOM_SHADOWS_INCLUDED
#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4
//atlas isn't a regular texture
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
//explicit sampler state
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount; 
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
};
//the cascade index is determined per fragment than per light
struct ShadowData {
	int cascadeIndex;
	float strength;
};

float FadeShadowStrength (float distance, float scale, float fade){
	return saturate((1.0 - distance * scale) *fade);
}
//return the shadow data for a world-sace surface 
ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	data.strength = FadeShadowStrength(
		surfaceWS.depth,  _ShadowDistanceFade.x, _ShadowDistanceFade.y
	);
	// loop through all cascade culling spheres until find one that contains the surface position

	int i; 
	for (i = 0; i< _CascadeCount; i++){
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr =DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) {
			if (i == _CascadeCount - 1){
				data.strength *= FadeShadowStrength(
					distanceSqr, 1.0 / sphere.w, _ShadowDistanceFade.z
				);
			}
			break;
		}
	}

	//strength set to 0 if end up beyond the last cascade
	if (i == _CascadeCount) {
		data.strength = 1.0;
	}

	data.cascadeIndex = i;
	return data;
}

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