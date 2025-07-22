using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
// defines the rendering pipeline's functionality 
public partial class  CustomRenderPipeline : RenderPipeline
{

    /*
    because the camera array parameter requires allocating memory every frame 
    an alternative has been introduced that has a list parameter instead on unit 2022s
    */
    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    CameraRenderer renderer = new CameraRenderer();
    ShadowSettings shadowSettings;
    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, ShadowSettings shadowSettings)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //convert the final light intensity to linear space. 
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
    }
    protected override void Render(
    ScriptableRenderContext context, List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.Render(context, cameras[i], useDynamicBatching, useGPUInstancing, shadowSettings, useLightsPerObject);
        }
    }

    //keep the array version as it is declared abstract
    protected override void Render(
        ScriptableRenderContext context, Camera[] cameras)
    {
        Render(context, new List<Camera>(cameras));
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
