using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;
using LightType = UnityEngine.LightType;    
// defines the rendering pipeline's functionality 
public partial class CustomRenderPipeline : RenderPipeline
{
    partial void InitializeForEditor();
#if UNITY_EDITOR
    partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(lightsDelegate);
    }

    //clean up and reset the delegate when the piline is disposed 
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }
    static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            var lightData = new LightDataGI();
            //configure a lightdatagi struct for each light and add it to output 
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                switch (light.type)
                {
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                    case LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        //set the inner angle and falloff

                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;

                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        //does not support realtime area lights
                        if (light.bakingOutput.lightmapBakeType != LightmapBakeType.Baked)
                        {
                            Debug.LogWarning("Realtime area lights are not supported in this render pipeline. " +
                                "Please use baked area lights instead.");
                            rectangleLight.mode = LightMode.Baked;
                        }
                        lightData.Init(ref rectangleLight);
                        break;
                }
                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }

        }; 
        
#endif
}
