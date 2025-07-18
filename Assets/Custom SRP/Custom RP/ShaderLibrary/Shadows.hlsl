#ifndef  CUSTOM_SHADOWS_INCLUDED
#define  CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP  SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP  SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP  SampleShadow_ComputeSamples_Tent_7x7
#endif

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
	float4 _CascadeData[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
};
//to know whether a shadow mask is in use,
struct ShadowMask {
	bool distance;
	float4 shadows;
};

struct ShadowData {
	//the cascade index is determined per fragment than per light
	int cascadeIndex;
	//cascade blend to make the cascade transition less noticeable
	float cascadeBlend;
	float strength;
	ShadowMask shadowMask;
};

float FadedShadowStrength (float distance, float scale, float fade){
	return saturate((1.0 - distance * scale) *fade);
}

//return the shadow data for a world-sace surface 
ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(
		surfaceWS.depth,  _ShadowDistanceFade.x, _ShadowDistanceFade.y
	);
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;
	// loop through all cascade culling spheres until find one that contains the surface position

	int i; 
	for (i = 0; i< _CascadeCount; i++){
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr =DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) {
			float fade = FadedShadowStrength(
				distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
			);
			if (i == _CascadeCount - 1){
				data.strength *= fade;
			} else {
				data.cascadeBlend = fade;
			}
			break;
		}
	}

	//strength set to 0 if end up beyond the last cascade
	if (i == _CascadeCount) {
		data.strength = 0.0;
	}
	#if defined(_CASCADE_BLEND_DITHER)
		else if (data.cascadeBlend < surfaceWS.dither){
			i += 1;
		}
	#endif
	#if !defined(_CASCADE_BLEND_SOFT)
		data.cascadeBlend = 1.0;
	#endif
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

//when Directional filter setup is defined it needs to sample multiple times 
//otherwise it can invoke SampleDirectionalShadowAtlas  only once
float FilterDirectionalShadow (float3 positionSTS) {
	#if defined(DIRECTIONAL_FILTER_SETUP)
		float weights[DIRECTIONAL_FILTER_SAMPLES];
		float2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++){
			shadow += weights[i] * SampleDirectionalShadowAtlas(
				float3(positions[i].xy, positionSTS.z)
			);
		}
		return shadow;
	#else 
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif 
}

float GetCascadeShadow(
	DirectionalShadowData directional,, ShadowData global, Surface surfaceWS
){
	//multiply the surface normal with the offset to find the normal bias 
	float3 normalBias = surfaceWS.normal *(directional.normalBias * _CascadeData[global.cascadeIndex].y);
	//use the tile offset to retrieve the correct matrix;
	//     - added normal bias to world position before calculating the position in  shadow tile space 
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow (positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.normal * 
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1.0],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
}

//return the shadow attenuation 
//given directional shadow data and a surface defined in world space 
float GetDirectionalShadowAttenuation (DirectionalShadowData directional, ShadowData global, Surface surfaceWS){
    #if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	float shadow;
	if (directional.strength <= 0.0){
        shadow = 1.0;
    } else {
		shadow = GetCascadeShadow(directional, global, surfaceWS);
		shadow = lerp(1.0, shadow, directional.strength);
	}


	return shadow;
}


#endif