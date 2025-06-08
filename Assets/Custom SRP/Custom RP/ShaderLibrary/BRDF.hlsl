#ifndef  CUSTOM_BRDF_INCLUDED
#define  CUSTOM_BRDF_INCLUDED
#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MIN_REFLECTIVITY 0.04

struct BRDF {
    float3 diffuse;
    float3 specular;
    float roughness;
};

// in reality some light alsoo bounces off dielectric surface, giving them highlight
// the reflectivity of non metal is 0.04
float OneMinusReflectivity (float metallic) {
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

float SpecularStrength (Surface surface, BRDF brdf, Light light) {
	float3 h = SafeNormalize(light.direction + surface.viewDirection);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2 / (d2 * max(0.1, lh2) * normalization);
}

float3 DirectBRDF (Surface surface, BRDF brdf, Light light) {
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}
BRDF GetBRDF(Surface surface) {
    BRDF brdf;
    brdf.diffuse = surface.color * OneMinusReflectivity(surface.metallic);
    //amount of outgoing light cannot exceed the amount of incoming light
    //using the metallic property to interpolate between the minimum reflectivity and the surface color.
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);

    brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    return brdf;
};



#endif 