using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.XR;
using UnityEngine.Profiling;

//roughly equivalnet to scriptable renderers of the Universal RP. 
//Simple to support different rendering approaches per camera in the future. 
public partial class CameraRenderer
{
    partial void DrawGizmosBeforeFX();
    partial void DrawGizmosAfterFX();

    partial void DrawUnsupportedShaders();
    partial void PrepareForSceneWindow();
    partial void PrepareBuffer();

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM"),
    };
    string SampleName { get; set; }
    static Material errorMaterial;
    partial void DrawGizmosBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);

        }
    }

    partial void DrawGizmosAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
    partial void DrawUnsupportedShaders()
    {
        if (errorMaterial == null)
        {
            errorMaterial =
                new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(
            legacyShaderTagIds[0], new SortingSettings(camera)
        )
        {
            overrideMaterial = errorMaterial
        };

        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );
    }

    partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {   
            //Add UI to the world geometry when rendering for the scene window
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }

	partial void PrepareBuffer () {
		Profiler.BeginSample("Editor Only");
		buffer.name = SampleName = camera.name;
		Profiler.EndSample();
	}

    #else
    const string SampleName = bufferName;

#endif
}
