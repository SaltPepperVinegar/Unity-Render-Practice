using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//roughly equivalnet to scriptable renderers of the Universal RP. 
//Simple to support different rendering approaches per camera in the future. 
public class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;
    //unlit shader is a type of shader does not interact with light
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;

        if (!Cull())
        {
            return;
        }

        Setup();
        DrawVisibleGeometry();
        Submit();
    }

    /*
        To correctly render the scene, need to set up the view - Projection matrix (unity_MatrixVP)
        Projection matrix combines view matrix(1) with projection matrix(2) 
            1. camera's position and orientation 
            2. camera's perspective or orthographic projection 
    */
    void Setup()
    {
        //apply the camera's properties to the context
        context.SetupCameraProperties(camera);

        //clear the render target to get rid of its old contents
        //First two argument indicate whether the depth and color data should be cleared, which is true for both
        //third argument is Color used to clearing - use Color.clear
        buffer.ClearRenderTarget(true, true, Color.clear);

        buffer.BeginSample(bufferName);
        ExecuteBuffer();

    }
    void DrawVisibleGeometry()
    {
        //camera paramter is used to determine whether orthographic or distance-based sorting applies
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
            unlitShaderTagId, sortingSettings
        );
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
        buffer.EndSample(bufferName);
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
    bool Cull()
    {
        //Struct that keep track of multiple camera settings and matrices 
        ScriptableCullingParameters p;

        //retuns whether the parameters could be successfully retrived, as it migth fail for degenerate camera settings 
        //the out keyword tells us that the method is responsible for correctly setting the parameter 
        //p gets altered by calling the TryGetCullingParameters
        if (camera.TryGetCullingParameters(out p))
        {
            //ref is used as an optimization, to prevent passing a copy of scriptablecullingparameters struct. 
            //since stuct and value type passes value through function 
            cullingResults = context.Cull(ref p);

            return true;
        }
        return false;
    }

}
