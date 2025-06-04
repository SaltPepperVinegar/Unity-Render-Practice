using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    /*
    because the camera array parameter requires allocating memory every frame 
    an alternative has been introduced that has a list parameter instead on unit 2022s
    */
    CameraRenderer renderer = new CameraRenderer();
    protected override void Render(
    ScriptableRenderContext context, List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.Render(context, cameras[i]);
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
