using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    static int cutoffId = Shader.PropertyToID("_Cutoff");
    static int metallicId = Shader.PropertyToID("_Metallic");
    static int smoothnessId = Shader.PropertyToID("_Smoothness");
	static int emissionColorId = Shader.PropertyToID("_EmissionColor");

    static MaterialPropertyBlock block;
    [SerializeField]
    Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f, metallic = 0f, smoothness = 0.5f;
    [SerializeField, ColorUsage(false, true)]
	Color emissionColor = Color.black;

    void Awake()
    {   
        //make sure OnValidate get invoked by invoking color when awaking 
        OnValidate();   
    }

    void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutoffId, cutoff);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
		block.SetColor(emissionColorId, emissionColor);

        GetComponent<Renderer>().SetPropertyBlock(block);
    }
    
}
