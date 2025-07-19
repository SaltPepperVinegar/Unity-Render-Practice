using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public class MeshBall : MonoBehaviour
{
    static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    
    [SerializeField]
    Mesh mesh = default;

    [SerializeField]
    Material material = default;

    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];
    float[] metallic = new float[1023];
    float[] smoothness = new float[1023];

    MaterialPropertyBlock block;
	[SerializeField]
	LightProbeProxyVolume lightProbeVolume = null;

    void Awake()
    {
        for (int i = 0; i < matrices.Length; i++)
        {
            matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f,
                Quaternion.Euler(
                    Random.value * 360f, Random.value * 360f, Random.value * 360f
                ),
                Vector3.one * Random.Range(0.5f, 1.5f)
            );
            baseColors[i] =
                new Vector4(Random.value, Random.value, Random.value, 
                Random.Range(0.5f, 1f));
			metallic[i] = Random.value < 0.25f ? 1f : 0f;
			smoothness[i] = Random.Range(0.05f, 0.95f);
            }
    }

    void Update()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
            block.SetFloatArray(metallicId, metallic);
            block.SetFloatArray(smoothnessId, smoothness);
            if (!lightProbeVolume)
            {
                var positions = new Vector3[1023];
                for (int i = 0; i < matrices.Length; i++)
                {
                    positions[i] = matrices[i].GetColumn(3);
                }
                var lightProbes = new SphericalHarmonicsL2[1023];
                var occlusionProbes = new Vector4[1023];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                    positions, lightProbes, occlusionProbes
                );
                block.CopySHCoefficientArraysFrom(lightProbes);
                block.CopyProbeOcclusionArrayFrom(occlusionProbes);

            }

        }

        Graphics.DrawMeshInstanced(
            mesh, 0, material, matrices, 1023, block,
            UnityEngine.Rendering.ShadowCastingMode.On, true, 0,
            null, 
            lightProbeVolume ?
            LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
			lightProbeVolume

        );
        
        /*
        Graphics.DrawMeshInstanced(...) issues manual draw calls directly to Unity’s GPU command queue.
            It bypasses your render pipeline (SRP, URP, etc.),
            So no queue sorting, culling, or lightmode filtering
            
            Unity still knows about the geometry being drawn and does some automatic optimizations:
            Unity performs:
                CPU frustum culling: if all instances in the batch are outside the camera frustum, nothing is sent to the GPU.
                Possibly occlusion culling (depending on platform and Unity version).
            
            Unity performs culling at the batch level, not per instance.
            It computes a bounding volume that encapsulates all matrices in the list.
            If that volume is entirely outside the camera frustum, the whole batch is skipped.

        */
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
    }
}
