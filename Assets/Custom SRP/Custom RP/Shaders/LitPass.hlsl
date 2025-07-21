#ifndef  CUSTOM_LIT_PASS_INCLUDED
#define  CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

//input data to the vertex shader
struct Attributes {
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 baseUV : TEXCOORD0;
    //macro defined in GI.hlsl
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//intermediate values passed from the vertex shader to the fragment shader
struct Varyings {
    float4 positionCS: SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
    float4 tangentWS : VAR_TANGENT;
    float2 baseUV : VAR_BASE_UV;
    float2 detailUV : VAR_DETAIL_UV;
    //macro defined in GI.hlsl
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input){
    //SV_POSITION as the position output 
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.baseUV =  TransformBaseUV(input.baseUV);
    output.detailUV = TransformDetailUV(input.baseUV);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS =
		float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    return output;
}

//float should be used for positions and texture coodinates only and half everything elseif optimizing for mobile 
float4 LitPassFragment(Varyings input)  : SV_TARGET {
    UNITY_SETUP_INSTANCE_ID(input);

    //x component of unity_LODFade is the LOD fade factor
        //a factor that is from 0 to 1, 1: next LOD, 0: current LOD
    //y component is fade factor quantized to sixteen steps
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    InputConfig config = GetInputConfig(input.baseUV, input.detailUV);
    #if defined(_MASK_MAP)
		config.useMask = true;
	#endif
    #if defined(_DETAIL_MAP)
        config.detailUV = input.detailUV;
        config.useDetail = true;
    #endif

    float4 base = GetBase(config);
    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(config));
    #endif
    //SV_TARGET as the pixel color output 

    Surface surface;
    //get the final mapped normal from the normal map
	surface.normal = NormalTangentToWorld(
		GetNormalTS(config), input.normalWS, input.tangentWS
	);
    surface.interpolatedNormal = input.normalWS;
    surface.color = base.rgb;
    surface.alpha = base.a;
	surface.position = input.positionWS;
    surface.metallic = GetMetallic(config);
    surface.occlusion = GetOcclusion(config);
    surface.smoothness = GetSmoothness(config);
    //generates a rotated tile dither pattern given a screen-space XY position (clip-space XY position)
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.fresnelStrength = getFresnel(config);
	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
    
    //macro defined in GI.hlsl
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(config); 
	return float4(color,surface.alpha);
} 

 
#endif