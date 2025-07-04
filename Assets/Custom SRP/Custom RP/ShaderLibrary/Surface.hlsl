#ifndef  CUSTOM_SURFACE_INCLUDED
#define  CUSTOM_SURFACE_INCLUDED

struct Surface{
    float3 normal;
    float3 viewDirection;
    float depth;
    float3 color;
    float3 position;
    float alpha;
    float metallic;
    float smoothness;
    float dither;

};

#endif