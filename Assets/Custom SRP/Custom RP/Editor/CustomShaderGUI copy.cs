using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Runtime.Remoting.Messaging;
using UnityEditor.Presets;
public class CustomShaderGUI : ShaderGUI
{
    bool showPresets;
    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;
    bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    bool PremultiplyAlpha
    {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }

    BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float)value);
    }

    bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }
    bool HasProperty(string name) =>
        FindProperty(name, properties, false) != null;
    bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");

    enum ShadowMode
    {
        On, Clip, Dither, Off
        //dither as semi transparent shadow
    }
    ShadowMode shadows
    {
        set
        {
            if (SetProperty("_Shadows", (float)value))
            {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }
    //disable shadowcaster pass per object 
    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        if (shadows == null || shadows.hasMixedValue)
        {
            return;
        }
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();
        base.OnGUI(materialEditor, properties);
        this.editor = materialEditor;
        this.materials = materialEditor.targets;
        this.properties = properties;

        BakedEmission();

        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }
        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
            CopyLightMappingProperties();
        }
    }

    void BakedEmission()
    {
        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in editor.targets)
            {
                //disable the default emission  flag when the emission mode is changed.
                m.globalIlluminationFlags &=
                    ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    private void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }
    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }


    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
        {
            SetKeyword(keyword, value);
        }
    }

    //render queue is set by assigning to the renderqueue property of all materials 
    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    // return true then it is pressed.
    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            //register to the undo step with the editor 
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }
    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent"))
        {
            if (PresetButton("Transparent"))
            {

                Clipping = false;
                PremultiplyAlpha = true;
                SrcBlend = BlendMode.One;
                DstBlend = BlendMode.OneMinusSrcAlpha;
                ZWrite = false;
                RenderQueue = RenderQueue.Transparent;
            }
        }


    }

    //make sure the maintext points to the same texture as the basemap
    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
        if (color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }
}
