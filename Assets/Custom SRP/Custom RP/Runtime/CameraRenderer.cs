using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

//roughly equivalnet to scriptable renderers of the Universal RP. 
//Simple to support different rendering approaches per camera in the future. 
public partial class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;
    // a class in Unity SRP that represents a specific shader pass/tag in the rendering pipeline
    static ShaderTagId 
        //when pass doesn't have lightmode tag, use this tag as default
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();
    
    public void Render(
        ScriptableRenderContext context, Camera camera, bool useDynamicBatching,
        bool useGPUInstancing, ShadowSettings shadowDrawingSettings,
        bool useLightsPerObject
    )
    {
        this.context = context;
        this.camera = camera;
        //for profiler sampling purposes
        PrepareBuffer();
        PrepareForSceneWindow();

        //call context cull function and set CullingResults, return 
        if (!Cull(shadowDrawingSettings.maxDistance))
        {
            //if culling fails, early return
            return;
        }

        //sells unity's Profiler to begin measure performance for a block of render commands
        buffer.BeginSample(SampleName);

        ExecuteBuffer();

        lighting.Setup(context, cullingResults, shadowDrawingSettings, useLightsPerObject);

        buffer.EndSample(SampleName);

        Setup();

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);

        DrawUnsupportedShaders();

        DrawGizmos();

        lighting.Cleanup();

        Submit();
    }



    void Setup()
    {
        /*
        To correctly render the scene, need to set up the view - Projection matrix (unity_MatrixVP)
        Projection matrix combines view matrix(1) with projection matrix(2) 
            1. camera's position and orientation 
            2. camera's perspective or orthographic projection 
        */
        //apply the camera's properties to the context
        //set up the matrix as well as some other properties
        context.SetupCameraProperties(camera);


        //clear the render target to get rid of its old contents
        //First two argument indicate whether the depth and color data should be cleared, which is true for both
        //third argument is Color used to clearing - use Color.clear
            CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
				camera.backgroundColor.linear : Color.clear);

        buffer.BeginSample(SampleName);
        ExecuteBuffer();

    }
    void DrawVisibleGeometry(
        bool useDynamicBatching, bool useGPUInstancing,
        bool useLightsPerObject //indicate whether lights per object mode should be used. 
    ) {
        PerObjectData lightsPerObjectFlags = useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices :
            PerObjectData.None;
        //camera paramter is used to determine whether orthographic or distance-based sorting applies
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
            unlitShaderTagId, sortingSettings
        )
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            //flag from the PerObjectData which tells unity to include shader data 
            //perObjectData is a bit flag enum 
            perObjectData = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.ShadowMask |
            PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
            PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume |
            lightsPerObjectFlags
		};

        //setshaderpassname sets which shader pass to use for given index in the draw call. 
        drawingSettings.SetShaderPassName(1,litShaderTagId);

        //indicate which render queues are allowed. 
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );
        context.DrawSkybox(camera);
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );

    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }


    void ExecuteBuffer()
    {
        //copies the commands from the buffer but doesn't clear it 
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /*
    Culling objects fall outside of the viw frustum of the camer 
    */
    bool Cull(float maxShadowDistance)
    {
        //Struct that keep track of multiple camera settings and matrices 
        ScriptableCullingParameters p;

        //retuns whether the parameters could be successfully retrived, as it migth fail for degenerate camera settings 
        //the out keyword tells us that the method is responsible for correctly setting the parameter 
        //p gets altered by calling the TryGetCullingParameters
        if (camera.TryGetCullingParameters(out p))
        {   

            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            //ref is used as an optimization, to prevent passing a copy of scriptablecullingparameters struct. 
            //since stuct and value type passes value through function 
            cullingResults = context.Cull(ref p);

            return true;
        }
        return false;
    }

}
