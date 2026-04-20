using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Serialization;

/// <summary>
/// 磁化オブジェクトに Edge Detection アウトラインを描画する Renderer Feature。
/// Unity 6 RenderGraph 対応。
/// </summary>
public class OutlineRendererFeature : ScriptableRendererFeature
{
    [Header("設定")]
    [SerializeField] private uint m_outlineRenderingLayerMask = 256;  // 1u << 8 = Magnetized
    [FormerlySerializedAs("normalsMaterial")]
    [SerializeField] private Material m_normalsMaterial;
    [FormerlySerializedAs("edgeDetectionMaterial")]
    [SerializeField] private Material m_edgeDetectionMaterial;

    [Header("パラメータ")]
    [FormerlySerializedAs("outlineThickness")]
    [SerializeField] private float m_outlineThickness = 1.0f;
    [FormerlySerializedAs("depthThreshold")]
    [SerializeField] private float m_depthThreshold = 0.5f;
    [FormerlySerializedAs("normalThreshold")]
    [SerializeField] private float m_normalThreshold = 0.4f;
    [FormerlySerializedAs("outlineColorS")]
    [SerializeField] private Color m_outlineColorS = new Color(0.2f, 0.4f, 1f, 1f);
    [FormerlySerializedAs("outlineColorN")]
    [SerializeField] private Color m_outlineColorN = new Color(1f, 0.2f, 0.2f, 1f);

    private NormalsPrePass m_normalsPass;
    private EdgeDetectionPass m_edgePass;

    public override void Create()
    {
        m_normalsPass = new NormalsPrePass(m_outlineRenderingLayerMask, m_normalsMaterial);
        m_normalsPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        m_edgePass = new EdgeDetectionPass(m_edgeDetectionMaterial, m_outlineThickness,
            m_depthThreshold, m_normalThreshold, m_outlineColorS, m_outlineColorN);
        m_edgePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques + 1;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_normalsMaterial == null || m_edgeDetectionMaterial == null) { ChannelLogger.LogGuardReturn("Game", "アウトライン用マテリアル未設定"); return; }
        if (renderingData.cameraData.cameraType == CameraType.SceneView) { ChannelLogger.LogGuardReturn("Game", "SceneViewカメラはスキップ"); return; }
        if (renderingData.cameraData.cameraType == CameraType.Preview) { ChannelLogger.LogGuardReturn("Game", "Previewカメラはスキップ"); return; }

        renderer.EnqueuePass(m_normalsPass);
        renderer.EnqueuePass(m_edgePass);
    }

    protected override void Dispose(bool disposing)
    {
        m_normalsPass?.Cleanup();
    }

    //  Rendering Layer Mask対象オブジェクトの法線を専用RTに描画
    class NormalsPrePass : ScriptableRenderPass
    {
        private readonly uint m_renderingLayerMask;
        private readonly Material m_normalsMaterial;
        private readonly List<ShaderTagId> m_shaderTagIds = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };
        private RTHandle m_normalsRT;
        private RTHandle m_depthRT;

        public NormalsPrePass(uint renderingLayerMask, Material normalsMaterial)
        {
            m_renderingLayerMask = renderingLayerMask;
            m_normalsMaterial = normalsMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            // 法線テクスチャ
            var colorDesc = cameraData.cameraTargetDescriptor;
            colorDesc.colorFormat = RenderTextureFormat.ARGBFloat;
            colorDesc.depthBufferBits = 0;
            colorDesc.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_normalsRT, colorDesc, name: "_ViewSpaceNormals");

            // 磁化オブジェクト専用深度テクスチャ
            var depthDesc = cameraData.cameraTargetDescriptor;
            depthDesc.colorFormat = RenderTextureFormat.Depth;
            depthDesc.depthBufferBits = 32;
            depthDesc.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_depthRT, depthDesc, name: "_MagnetizedDepthTexture");

            var drawingSettings = RenderingUtils.CreateDrawingSettings(
                m_shaderTagIds, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
            drawingSettings.overrideMaterial = m_normalsMaterial;
            // Physics LayerフィルタリングはRendering Layer Maskに移行済み。全Physics Layer通過。
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, ~0);
            filteringSettings.renderingLayerMask = m_renderingLayerMask;
            var rlParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererListHandle = renderGraph.CreateRendererList(rlParams);

            using (var builder = renderGraph.AddUnsafePass<NormalsPassData>("NormalsPrePass", out var passData))
            {
                passData.normalsRT = m_normalsRT;
                passData.depthRT = m_depthRT;
                passData.rendererList = rendererListHandle;

                builder.UseRendererList(rendererListHandle);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc<NormalsPassData>(static (data, ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    cmd.SetRenderTarget(data.normalsRT, data.depthRT);
                    cmd.ClearRenderTarget(true, true, Color.clear);
                    cmd.DrawRendererList(data.rendererList);
                    cmd.SetGlobalTexture("_ViewSpaceNormals", data.normalsRT);
                    cmd.SetGlobalTexture("_MagnetizedDepthTexture", data.depthRT);
                });
            }
        }

        class NormalsPassData
        {
            public RTHandle normalsRT;
            public RTHandle depthRT;
            public RendererListHandle rendererList;
        }

        public void Cleanup()
        {
            m_normalsRT?.Release();
            m_depthRT?.Release();
        }
    }

    // フルスクリーン Roberts Cross エッジ検出
    class EdgeDetectionPass : ScriptableRenderPass
    {
        private readonly Material m_material;
        private readonly float m_thickness;
        private readonly float m_depthThreshold;
        private readonly float m_normalThreshold;
        private readonly Color m_colorS;
        private readonly Color m_colorN;

        public EdgeDetectionPass(Material material, float thickness,
            float depthThreshold, float normalThreshold, Color colorS, Color colorN)
        {
            m_material = material;
            m_thickness = thickness;
            m_depthThreshold = depthThreshold;
            m_normalThreshold = normalThreshold;
            m_colorS = colorS;
            m_colorN = colorN;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            m_material.SetFloat("_OutlineThickness", m_thickness);
            m_material.SetFloat("_DepthThreshold", m_depthThreshold);
            m_material.SetFloat("_NormalThreshold", m_normalThreshold);
            m_material.SetColor("_OutlineColorS", m_colorS);
            m_material.SetColor("_OutlineColorN", m_colorN);

            var source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_EdgeDetectionTemp";
            var tempTex = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddUnsafePass<BlitPassData>("EdgeDetection", out var passData))
            {
                passData.material = m_material;
                passData.source = source;
                passData.temp = tempTex;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(tempTex, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc<BlitPassData>(static (data, ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                    // source → temp（エッジ検出マテリアル適用）
                    cmd.SetRenderTarget(data.temp);
                    Blitter.BlitTexture(cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);

                    // temp → source（書き戻し）
                    cmd.SetRenderTarget(data.source);
                    Blitter.BlitTexture(cmd, data.temp, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        class BlitPassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle temp;
        }
    }
}
