#ifndef  CUSTOM_UNLIT_INPUT_INCLUDED
#define  CUSTOM_UNLIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
/*
SRP batcher:
    all material properties have to be defined inside a concrete memory buffer 
*/
//also allowing per-instance material data 
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    //segragates _BaseColor by putting it in a specific constant memory buffer, although it remains accessble at the global level
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV (float2 baseUV){
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (float2 baseUV){
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return map * color; 
}

float GetCutoff (float2 baseUV) {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic (float2 baseUV) {
    return 0.0;
}

float GetSmoothness (float2 baseUV) {
    return 0.0;
}
float3 GetEmission (float2 baseUV) {
	return GetBase(baseUV).rgb;
}

float GetFresnel (float2 baseUV) {
	return 0.0;
}

#endif