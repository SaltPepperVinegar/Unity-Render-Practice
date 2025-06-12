using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

//make sure RP send the light data to the GPU
public class Shadows
{
    const string bufferName = "Shadows";
    const int
        maxShadowedDirectionalLightCount = 4,
        maxCascades = 4;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;
    int ShadowedDirectionalLightCount;
    static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        //shadow transfomration matrix for each shadowed directional light
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        //x: (pass in cascade data according to world space texel size per cascade, )
        //y: 2* sqrt(2f) * texel size by cullingSphere.w / tileSize
        cascadeData = new Vector4[maxCascades];
    static Matrix4x4[]

        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    },
    cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER",
    };

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
        public float slopeScaleBias;
        //offset used to pulling the near plane back
        public float nearPlaneOffset;
    }

    ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];


    //return 
    //1. shadow strength and 
    //2. shadow tile offset
    //3. shadow normal bias
    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
        light.shadows != LightShadows.None && light.shadowStrength > 0f &&
        cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            shadowedDirectionalLights[ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            return new Vector3(
                light.shadowStrength,
                settings.directional.cascadeCount * ShadowedDirectionalLightCount++,
                light.shadowNormalBias
            );
        }
        return Vector3.zero;
    }

    //delegate rendering of directional shadows to another render directional shadows method
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

        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;

        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            // to render shadow for a single light 

            RenderDirectionalShadows(i, split, tileSize);
        }
        //sending cascade shadow map data to GPU 
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres
        );
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        //send the matrices to the GPU by calling buffer
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId,
            new Vector4(1f / settings.maxDistance,
                1f / settings.distanceFade,
                1f / (1f - f * f)
                )
            );
        SetKeywords(
            directionalFilterKeywords, (int) settings.directional.filter -1
        );
        SetKeywords(
            cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1
        );

        //stores the size in X component, and texel size in y component
        buffer.SetGlobalVector(
            shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)
        );
        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }
    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            // figure out view and prjojection matrices that match the light's orientation
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            //culling data is part of the splitdata that cullingresult outputs 
            //only for first light as the cascade for all lights are equivalent
            shadowSettings.splitData = splitData;
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            //contains information about how shadow casting object should be culled
            int tileIndex = tileOffset + i;
            //conversion matrix from world space to light space 
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), split
            );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            //adding a (constant bias, slope bias) to the depth of shadow casters
            //pushing the depth of shadow casters away from the light so incorrect self-shadowing no longer happens
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);

        }
    }

    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        //find the texel size in SetCascadeData by dividing the diameter of the culling sphere by the tile size.
        float texelSize = 2f * cullingSphere.w / tileSize;
        //adjust the normal bias to match the PCF filtering 
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        //avoid sampling outside of cascade's culling sphere by reducing the sphere radius
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        //store square radius instead to allow future comparison 
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            filterSize * 1.4142136f
        );

    }
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
        return offset;
    }

    //takes a light matrix, tile offset and split
    //returns a matrix that converts from world space to shadow tile space
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {   
            //flips the third row of the matrices
            m.m20 = -m.m20;
            m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
        }
        //sacling and offsetting the XYZ dimentions by half. 
		float scale = 1f / split;
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }
}