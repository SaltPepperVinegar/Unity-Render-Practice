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
#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP  SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP  SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP  SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4
//atlas isn't a regular texture
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
//explicit sampler state
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;  // _CascadeCount = 0 when no directional shadow exist 
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};
//to know whether a shadow mask is in use,
struct ShadowMask {
	bool always;
	bool distance;
	float4 shadows;
};

struct ShadowData {
	//the cascade index is determined per fragment than per light
	int cascadeIndex;
	//cascade blend to make the cascade transition less noticeable
	float cascadeBlend;
	float strength;
	//global strength is used to determin whether can skip realtime shadow, 
	// - either beyond the shadow distance or outside the largest cascade spere. 
	ShadowMask shadowMask;
};

struct OtherShadowData {
	float strength;
	int tileIndex;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 spotDirectionWS;
};



float FadedShadowStrength (float distance, float scale, float fade){
	return saturate((1.0 - distance * scale) *fade);
}

//return the shadow data for a world-sace surface 
ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	data.shadowMask.always = false;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;

	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(
		surfaceWS.depth,  _ShadowDistanceFade.x, _ShadowDistanceFade.y
	);

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
	if (i == _CascadeCount && _CascadeCount > 0) {
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
float SampleOtherShadowAtlas (float3 positionSTS, float3 bounds) {
	positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
	return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
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

float FilterOtherShadow (float3 positionSTS, float3 bounds) {
	#if defined(OTHER_FILTER_SETUP)
		float weights[OTHER_FILTER_SAMPLES];
		float2 positions[OTHER_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.wwzz;		//size = (w,w,z,z)
		OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < OTHER_FILTER_SAMPLES; i++){
			shadow += weights[i] * SampleOtherShadowAtlas(
				float3(positions[i].xy, positionSTS.z), bounds
			);
		}
		return shadow;
	#else 
		return SampleOtherShadowAtlas(positionSTS, bounds);
	#endif 
}

float GetCascadedShadow(
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
){
	//multiply the surface normal with the offset to find the normal bias 
	float3 normalBias = surfaceWS.interpolatedNormal
		 *(directional.normalBias * _CascadeData[global.cascadeIndex].y);
	//use the tile offset to retrieve the correct matrix;
	//     - added normal bias to world position before calculating the position in  shadow tile space 
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow (positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.interpolatedNormal * 
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1.0],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return shadow;
}

float GetBakedShadow (ShadowMask mask, int channel) {
	float shadow = 1.0;
	if (mask.always || mask.distance) {
		if (channel >= 0){
			shadow = mask.shadows[channel];
		}
	}
	return shadow;
}

float GetBakedShadow (ShadowMask mask, int channel, float strength) {
	if (mask.always || mask.distance) {
		return lerp(1.0, GetBakedShadow(mask, channel), strength);
	}
	return 1.0;
}

float MixBakedAndRealtimeShadows (
	ShadowData global, float shadow, int shadowMaskChannel, float strength
) {
	float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	if (global.shadowMask.always) {
		shadow = lerp(1.0, shadow, global.strength);
		shadow = min(baked, shadow);
		return lerp(1.0, shadow, strength);
	}
	if (global.shadowMask.distance) {
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}
	return lerp(1.0, shadow, strength * global.strength);
}


//return the shadow attenuation 
//given directional shadow data and a surface defined in world space 
float GetDirectionalShadowAttenuation (DirectionalShadowData directional, ShadowData global, Surface surfaceWS){
    #if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	float shadow;
	if (directional.strength * global.strength<= 0.0){
        shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength));
    } else {
		shadow = GetCascadedShadow(directional, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(
			global, shadow, directional.shadowMaskChannel, directional.strength);

	}


	return shadow;
}

float GetOtherShadow(
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	float4 tileData = _OtherShadowTiles[other.tileIndex];
	float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float distanceToLightPlane = dot(surfaceToLight, other.spotDirectionWS);
	float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane *tileData.w);
	float4 positionSTS = mul(
		_OtherShadowMatrices[other.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}



//use the same approach as for directional shadows, but only have strength and mask channel
float GetOtherShadowAttenutation (
	OtherShadowData other, ShadowData global, Surface surfaceWS
){
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif

	float shadow;
	if (other.strength * global.strength <= 0.0) {
		//return the baked light only 
		shadow = GetBakedShadow(
			global.shadowMask, other.shadowMaskChannel, abs(other.strength)
		);
	} 
	else {
		shadow = GetOtherShadow(other, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(
			global, shadow, other.shadowMaskChannel, other.strength
		);
	}
	return shadow;

}

#endif