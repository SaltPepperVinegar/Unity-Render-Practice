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
    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;

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
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        context.SetupCameraProperties(camera);
    }
    void DrawVisibleGeometry()
    {
        context.DrawSkybox(camera);
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
}
