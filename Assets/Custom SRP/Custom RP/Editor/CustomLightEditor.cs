using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Runtime.Remoting.Messaging;
using UnityEditor.Presets;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (
            !settings.lightType.hasMultipleDifferentValues &&
            (LightType)settings.lightType.enumValueIndex == LightType.Spot
        )
        {
            settings.DrawInnerAndOuterSpotAngle();
            settings.ApplyModifiedProperties();
        }
    }
}
