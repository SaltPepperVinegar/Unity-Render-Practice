#ifndef  CUSTOM_COMMON_INCLUDED
#define  CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

#include "UnityInput.HLSL"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

// Unity Instancing only get instanced automatically when SHADOWS_SHADOWMASK is defined.
#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

/*
float3 TransformObjectToWorld (float3 positionOS){
    //swizzle operation
    return mul(unity_ObjectToWorld, float4(positionOS,1.0)).xyz;
};

float4 TransformWorldToHClip (float3 positionWS){
    return mul(unity_MatrixVP, float4(positionWS, 1.0));
}
*/


float Square (float v) {
	return v * v;
}
 
float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}

void ClipLOD (float2 positionCS, float fade) {
    #if defined(LOD_FADE_CROSSFADE)
        float dither = InterleavedGradientNoise(positionCS.xy, 0);
        // 1) Build a comparison value around zero:
        //    – If fade < 0, we're before the start of the fade zone,
        //      so we add noise (+dither) to “jitter” some pixels back in.
        //    – If fade ≥ 0, we're inside or past the fade zone,
        //      so we subtract noise (–dither) to jitter some pixels out.
        // 2) Clip if the fade value is less than the dither value.
        clip(fade+ (fade < 0.0 ? dither : -dither));
    #endif
}

float3 DecodeNormal (float4 sample, float scale) {
    //DXT is a compression format that partitions the texture into 4x4 blocks.
    #if defined(UNITY_NO_DXT5nm)
        return normalize(UnpackNormalRGB(sample, scale));
    #else 
        return normalize(UnpackNormalmapRGorAG(sample, scale));
    #endif
}

//conversion matrix from tangent space to world space
float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

#endif