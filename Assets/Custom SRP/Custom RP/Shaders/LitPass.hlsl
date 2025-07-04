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
    float2 baseUV : TEXCOORD0;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//intermediate values passed from the vertex shader to the fragment shader
struct Varyings {
    float4 positionCS: SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;
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
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

//float should be used for positions and texture coodinates only and half everything elseif optimizing for mobile 
float4 LitPassFragment(Varyings input)  : SV_TARGET {
    UNITY_SETUP_INSTANCE_ID(input);
    float4 base = GetBase(input.baseUV);
    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(input. baseUV));
    #endif
    //SV_TARGET as the pixel color output 

    Surface surface;
    surface.normal = normalize(input.normalWS);
    surface.color = base.rgb;
    surface.alpha = base.a;
	surface.position = input.positionWS;
    surface.metallic = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV);
    //generates a rotated tile dither pattern given a screen-space XY position (clip-space XY position)
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
    
    //macro defined in GI.hlsl
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface);
    float3 color = GetLighting(surface, brdf, gi);
	return float4(color,surface.alpha);
} 

 
#endif