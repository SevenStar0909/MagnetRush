using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 磁化オブジェクトに Edge Detection アウトラインを描画する Renderer Feature。
/// Pass1: Magnetized レイヤーのオブジェクトの法線を専用RTに描画
/// Pass2: Roberts Cross エッジ検出で全辺にアウトライン
/// </summary>
public class OutlineRendererFeature : ScriptableRendererFeature
{
    [Header("設定")]
    [SerializeField] private LayerMask outlineLayerMask = 0;
    [SerializeField] private Material normalsMaterial;
    [SerializeField] private Material edgeDetectionMaterial;

    [Header("パラメータ")]
    [SerializeField] private float outlineThickness = 1.0f;
    [SerializeField] private float depthThreshold = 0.5f;
    [SerializeField] private float normalThreshold = 0.4f;
    [SerializeField] private Color outlineColorS = new Color(0.2f, 0.4f, 1f, 1f);
    [SerializeField] private Color outlineColorN = new Color(1f, 0.2f, 0.2f, 1f);

    private NormalsPrePass m_normalsPass;
    private EdgeDetectionPass m_edgePass;

    public override void Create()
    {
        m_normalsPass = new NormalsPrePass(outlineLayerMask, normalsMaterial);
        m_normalsPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        m_edgePass = new EdgeDetectionPass(edgeDetectionMaterial, outlineThickness,
            depthThreshold, normalThreshold, outlineColorS, outlineColorN);
        m_edgePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques + 1;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (normalsMaterial == null || edgeDetectionMaterial == null) return;

        renderer.EnqueuePass(m_normalsPass);
        renderer.EnqueuePass(m_edgePass);
    }

    /// <summary>Magnetized レイヤーのオブジェクトをビュー空間法線で描画するパス。</summary>
    class NormalsPrePass : ScriptableRenderPass
    {
        private readonly LayerMask m_layerMask;
        private readonly Material m_normalsMaterial;
        private readonly List<ShaderTagId> m_shaderTagIds = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        private RTHandle m_normalsRT;

        public NormalsPrePass(LayerMask layerMask, Material normalsMaterial)
        {
            m_layerMask = layerMask;
            m_normalsMaterial = normalsMaterial;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.ARGBFloat;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateHandleIfNeeded(ref m_normalsRT, desc, name: "_ViewSpaceNormals");

            ConfigureTarget(m_normalsRT);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("NormalsPrePass");

            var drawingSettings = CreateDrawingSettings(m_shaderTagIds, ref renderingData, SortingCriteria.CommonOpaque);
            drawingSettings.overrideMaterial = m_normalsMaterial;

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, m_layerMask);

            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

            cmd.SetGlobalTexture("_ViewSpaceNormals", m_normalsRT);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public void Dispose()
        {
            m_normalsRT?.Release();
        }
    }

    /// <summary>フルスクリーン Roberts Cross エッジ検出パス。</summary>
    class EdgeDetectionPass : ScriptableRenderPass
    {
        private readonly Material m_material;
        private readonly float m_thickness;
        private readonly float m_depthThreshold;
        private readonly float m_normalThreshold;
        private readonly Color m_colorS;
        private readonly Color m_colorN;

        private RTHandle m_tempRT;

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

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_tempRT, desc, name: "_EdgeDetectionTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("EdgeDetection");

            m_material.SetFloat("_OutlineThickness", m_thickness);
            m_material.SetFloat("_DepthThreshold", m_depthThreshold);
            m_material.SetFloat("_NormalThreshold", m_normalThreshold);
            m_material.SetColor("_OutlineColorS", m_colorS);
            m_material.SetColor("_OutlineColorN", m_colorN);

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            Blitter.BlitCameraTexture(cmd, source, m_tempRT, m_material, 0);
            Blitter.BlitCameraTexture(cmd, m_tempRT, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            m_tempRT?.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_normalsPass?.Dispose();
        m_edgePass?.Dispose();
    }
}
