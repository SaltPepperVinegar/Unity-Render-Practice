#ifndef  CUSTOM_META_PASS_INCLUDED
#define  CUSTOM_META_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"


struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
};

struct Varyings {
    float4 positionCS: SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
};

Varyings MetaPassVertex(Attributes input){
    Varyings output;
    output.baseUV =  TransformBaseUV(input.baseUV);
    output.positionCS = 0.0;
    return output;
}

//float should be used for positions and texture coodinates only and half everything elseif optimizing for mobile 
float4 MetaPassFragment(Varyings input)  : SV_TARGET {
    float4 base = GetBase(input.baseUV);
    Surface surface;
    ZERO_INITIALIZE(Surface, surface);
    surface.color = base.rgb;
    surface.metallic = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV);
    BRDF brdf = GetBRDF(surface, true);
    float4 meta = 0.0;
	return meta;
} 

  
#endif