#ifndef  CUSTOM_UNITY_INPUT_INCLUDED
#define  CUSTOM_UNITY_INPUT_INCLUDED

//uniform value, set by GPU once per draw, 
//remaining constant,uniform for all invocation of the vertex and fragment functions during that draw
CBUFFER_START(UnityPerDraw)
    //These values change every time a new mesh is drawn, so they go in the PerDraw buffer.
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    real4 unity_LightData;
    real4 unity_LightIndices[2];
    //baked shadow mask data into light probe
    float4 unity_ProbesOcclusion;
	float4 unity_SpecCube0_HDR;

    float4 unity_LightmapST;
    //deprecated variable to ensure SRP batcher compatibility 
    float4 unity_DynamicLightmapST;
    //spherical harmonics (SH) coefficient for light probe 
    //used to compute ambient and indirect lighting 
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;
    float4 unity_ProbeVolumeParams;
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;
CBUFFER_END 

float3 _WorldSpaceCameraPos;
float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;
float4 _ProjectionParams;
 
#endif