using System;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

//make sure RP send the light data to the GPU
public class Shadows
{
    const string bufferName = "Shadows";
    const int
        maxShadowedDirectionalLightCount = 4,
        maxShadowedOtherLightCount = 16,
        maxCascades = 4;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;
    int shadowedDirectionalLightCount;
    int shadowedOtherLightCount;
    //xy components are the atlas size and texel size of the directional atlas, 
    //ZW components are the atlas size and texel size of other atlas
    Vector4 atlasSizes;

    static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        //shadow transfomration matrix for each shadowed directional light

        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");


    static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        //x: (pass in cascade data according to world space texel size per cascade, )
        //y: 2* sqrt(2f) * texel size by cullingSphere.w / tileSize
        cascadeData = new Vector4[maxCascades],
        otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
    static Matrix4x4[]

        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    },
    cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER",
    };

    //control whether shadow mask is used 
    static string[] shadowMaskKeywords = {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    static string[] otherFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    bool useShadowMask;
    
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        //offset used to pulling the near plane back
        public float nearPlaneOffset;
    }
    ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        //offset used to pulling the near plane back
        public float normalBias;
        public bool isPoint;
    }
    ShadowedOtherLight[] shadowedOtherLights =
        new ShadowedOtherLight[maxShadowedOtherLightCount];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.cullingResults = cullingResults;
        this.context = context;
        this.settings = settings;
        shadowedDirectionalLightCount = shadowedOtherLightCount = 0;
        useShadowMask = false;
    }
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if (shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
        ExecuteBuffer();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
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
    //return 
    //1. shadow strength and 
    //2. shadow tile offset
    //3. shadow normal bias
    //4. maskChannel
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {

        if (
            shadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f
        )
        {
            float maskChannel = -1f;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            if (!cullingResults.GetShadowCasterBounds(
                visibleLightIndex, out Bounds b
            ))
            {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }

            shadowedDirectionalLights[shadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            return new Vector4(
                light.shadowStrength,
                settings.directional.cascadeCount * shadowedDirectionalLightCount++,
                light.shadowNormalBias, maskChannel
            );
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    //return 
    //1. shadow strength and 
    //2. shadow otherLightCount
    //3. if is a point light 
    //4. mask Channel
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);

        }
        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
        )
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }
        bool isPoint = light.type == LightType.Point;
        int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
        if (
            newLightCount > maxShadowedOtherLightCount ||
            !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b) //no shadow to render for this light
        )
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }
        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };

        Vector4 data = new Vector4(
            light.shadowStrength, shadowedOtherLightCount, isPoint ? 1f : 0f,
            maskChannel
        );
        shadowedOtherLightCount = newLightCount;
        return data;
    }

    //delegate rendering of directional shadows to another render directional shadows method
    public void Render()
    {
        if (shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //not claiming a texture will lead to problems for WebGL 2.0
            //  - since it binds textures and samplers togerther 
            //avoid this by get a 1*1 dummy texture when no shadows are needed. 
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1,
            32,
            FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        if (shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            //if there aren't other shadows, use the directional shadow atlas as the dummy 
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }
        buffer.BeginSample(bufferName);
        //set only when shadow mask is used
        SetKeywords(shadowMaskKeywords, useShadowMask ?
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 :
            -1
        );
        buffer.SetGlobalInt(
            cascadeCountId,
            shadowedDirectionalLightCount > 0 ? settings.directional.cascadeCount : 0
        );
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId,
            new Vector4(1f / settings.maxDistance,
                1f / settings.distanceFade,
                1f / (1f - f * f)
            )
        );
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }



    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;


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
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;

        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedDirectionalLightCount; i++)
        {
            // to render shadow for a single light 

            RenderDirectionalShadows(i, split, tileSize);
        }
        //sending cascade shadow map data to GPU 
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres
        );
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        //send the matrices to the GPU by calling buffer
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        SetKeywords(
            directionalFilterKeywords, (int)settings.directional.filter - 1
        );
        SetKeywords(
            cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1
        );

        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];

        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);

        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;
        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
        for (int i = 0; i < cascadeCount; i++)
        {
            // figure out view and prjojection matrices that match the light's orientation
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            //cull some of shadow casters cascades that result covered in a small cascades
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
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
            //execute each object's shadowcaster subshader pass  
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);

        }
    }

    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;


        //texture identifier,  
        // the size for both its width and height in pixels, 
        // amount of bit for the depth buffer 
        //filtermode,
        //render texture type 
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32,
        FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        //must also instruct the GPU to render to this texture instead of the camera's target
        buffer.SetRenderTarget(
            otherShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;

        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i++;
            }
        }
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);

        SetKeywords(
            otherFilterKeywords, (int)settings.other.filter - 1
        );

        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }

    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];

        var shadowSettings =
            new ShadowDrawingSettings(
                cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Perspective);

            cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
            );

        shadowSettings.splitData = splitData;
		
        //factor light's normal bias immeidately.
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        Vector2 offset = SetTileViewport(index, split, tileSize);
        float tileScale = 1f / split;
        SetOtherTileData(index,offset, tileScale, bias);

            //conversion matrix from world space to light space 
        otherShadowMatrices[index] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix, offset, tileScale
        );
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

        //adding a (constant bias, slope bias) to the depth of shadow casters
        //pushing the depth of shadow casters away from the light so incorrect self-shadowing no longer happens
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        //execute each object's shadowcaster subshader pass  
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);

    }

    
    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
        cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Perspective);

        //factor light's normal bias immeidately.
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;

        float fovBias =
			Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (int i = 0; i < 6; i++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, (CubemapFace)i, fovBias, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
            );
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowSettings.splitData = splitData;
            int tileIndex = index + i;

            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            SetOtherTileData(tileIndex, offset, tileScale, bias);

            //conversion matrix from world space to light space 
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                    projectionMatrix * viewMatrix, offset, tileScale
            );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            //adding a (constant bias, slope bias) to the depth of shadow casters
            //pushing the depth of shadow casters away from the light so incorrect self-shadowing no longer happens
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            //execute each object's shadowcaster subshader pass  
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);

        }

    }
    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data = Vector4.zero;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
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
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
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