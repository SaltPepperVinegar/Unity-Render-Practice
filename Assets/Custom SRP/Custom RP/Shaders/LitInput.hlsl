#ifndef  CUSTOM_LIT_INPUT_INCLUDED
#define  CUSTOM_LIT_INPUT_INCLUDED
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_DetailMap);
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
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)

    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float4 GetMask (float2 baseUV) {
    return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, baseUV);
}

float2 TransformBaseUV (float2 baseUV){
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetDetail (float2 detailUV) {
    float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_BaseMap, detailUV);
    return map * 2.0 - 1.0; //convert to range [-1, 1]
}


float4 GetBase (float2 baseUV, float2 detailUV = 0.0){
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP(_BaseColor);

    //only r channel affects the albedo
    float detail = GetDetail(detailUV).r * INPUT_PROP(_DetailAlbedo);
    float mask = GetMask(baseUV).b;
    map.rgb = (lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask));
    map.rgb *= map.rgb;
    return map * color; 
}

float GetCutoff (float2 baseUV) {
    return INPUT_PROP(_Cutoff);
}


float GetSmoothness (float2 baseUV, float2 detailUV = 0.0) {
    float smoothness = INPUT_PROP(_Smoothness);
    smoothness *= GetMask(baseUV).a;
    float detail = GetDetail(detailUV).b * INPUT_PROP(_DetailSmoothness);
    float mask = GetMask(baseUV).b;
    smoothness = (lerp(sqrt(smoothness), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask));

    return smoothness;
}

float3 GetEmission (float2 baseUV) {
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_EmissionColor);
	return map.rgb * color.rgb;
}

float getFresnel (float2 baseUV) {
    return INPUT_PROP(_Fresnel);
}

float GetMetallic (float2 baseUV) {
    float metallic = INPUT_PROP(_Metallic);
    metallic *= GetMask(baseUV).r;
    return metallic;
}

float GetOcclusion (float2 baseUV) {
	float strength = INPUT_PROP(_Occlusion);
	float occlusion = GetMask(baseUV).g;
	occlusion = lerp(occlusion, 1.0, strength);
	return occlusion;
}

float2 TransformDetailUV (float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}




#endif