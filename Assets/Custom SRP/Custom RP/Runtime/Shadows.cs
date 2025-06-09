using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

//make sure RP send the light data to the GPU
public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowedDirectionalLightCount = 4;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;
    int ShadowedDirectionalLightCount;
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadow");
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.cullingResults = cullingResults;
        this.context = context;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    public void ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
        light.shadows != LightShadows.None && light.shadowStrength > 0f &&
        cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            shadowedDirectionalLights[ShadowedDirectionalLightCount++] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex
                };
        }
    }

    //delegate renering of directional shadows to another render directional shadows method
    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1,
             32,
            FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }


    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        //texture identifier,  
        // the size for both its width and height in pixels, 
        // amount of bit for the depth buffer 
        //filtermode,
        //render texture type 
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32,
        FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        //must also instruct the GPU to render to this texture instead of the camera's target
        buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int split = ShadowedDirectionalLightCount <= 1 ? 1 : 2;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            // to render shadow for a single light 

            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f,
            out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData
        );
        //contains information about how shadow casting object should be culled
        shadowSettings.splitData = splitData;
        SetTileViewPort(index, split, tileSize);
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
    void SetTileViewPort(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
    }
}