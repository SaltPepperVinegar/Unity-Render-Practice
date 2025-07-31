using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    Shader shader = default;


    [System.NonSerialized]
    Material material;

    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0f, 16)]
        [Tooltip("limits the maximun number of downsampling")]
        public int maxIteration;

        [Min(1f)]
        [Tooltip("the lowest resolution level")]
        public int downscaleLimit;
        public bool bicubicUpsampling;
        [Min(0f)]
        public float threshold;
        [Range(0f, 1f)]
        public float thresholdKnee;
        [Min(0f)]
        public float intensity;


    }

    [SerializeField]
    BloomSettings bloom = default;

    public BloomSettings Bloom => bloom;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }
    


    
}
