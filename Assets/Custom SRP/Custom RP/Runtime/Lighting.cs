using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

//make sure RP send the light data to the GPU
public class Lighting
{
    const string bufferName = "Lighting";
    const int maxDirLightCount = 4, maxOtherLightCount = 64;
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    /*
        to send all the lights data to cpu, use vector4 arrays and interger for light count. 
    */

    static int
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId =
            Shader.PropertyToID("_DirectionalLightShadowData"),
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
        


    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount],
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount],
        otherLightSpotAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];
    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
    CullingResults cullingResults;
    Shadows shadows = new Shadows();
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights(useLightsPerObject);
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    void SetupLights(bool useLightsPerObject)
    {
        //native array provides a connection to a native memory buffer
        //makes possible to efficiently share data between managed C# code and the native Unity engine code.
        NativeArray<int> indexMap = useLightsPerObject ?
            cullingResults.GetLightIndexMap(Allocator.Temp) : default; // temporary array with light indices. 
        NativeArray <VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0,
            otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupDirectionalLight(dirLightCount++, i, ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
                    
            }
            if (useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }

        }
        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);

        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }
        buffer.SetGlobalInt(otherLightCountId, otherLightColorsId);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }

    }

    void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        //final color provided by the final color property 
        dirLightColors[index] = visibleLight.finalColor;
        // forward vector can be found via the third column of localToWorldMatrix
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirLightShadowData[index] =
			shadows.ReserveDirectionalShadows(visibleLight.light, visibleIndex);
    }
    void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        //position can be found via the fourth column of localToWorldMatrix
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        //store 1/range^2 in w component to reduce work in shader
        position.w =
            1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightSpotAngles[index] = new Vector4(0f, 1f); // point lights don't have spot angles
        Light light  = visibleLight.light;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);

    }

    void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w =
            1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        // forward vector can be found via the negate third column of localToWorldMatrix
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		Light light = visibleLight.light;
		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
		float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		otherLightSpotAngles[index] = new Vector4(
			angleRangeInv, -outerCos * angleRangeInv
		);
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);

    }
    public void Cleanup()
    {
        shadows.Cleanup();
    }
}