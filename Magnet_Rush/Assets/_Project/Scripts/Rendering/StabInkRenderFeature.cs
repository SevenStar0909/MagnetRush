using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// スタブ着弾の「効果だけ黒・他は全部白」を描く Renderer Feature。Unity 6 RenderGraph 対応。
/// Global Volume を使わない。専用レンダリングレイヤー(StabInkEffect.RenderingLayerBit)を持つ
/// スタブVFXだけをマスクRTへ描き、フルスクリーン合成で「マスクのある所＝黒 / 他＝白」に塗る。
/// StabInkEffect.Active が true の間だけパスを積む（演出外はゼロコスト）。
/// 構造は OutlineRendererFeature（マスク前パス＋フルスクリーン合成）と同じ。
/// </summary>
public class StabInkRenderFeature : ScriptableRendererFeature
{
    [SerializeField] private Material m_compositeMaterial;

    private MaskPass m_maskPass;
    private CompositePass m_compositePass;

    public override void Create()
    {
        StabInkEffect.Strength = 0f;   // ドメインリロード跨ぎで焼き付かないよう、起動時は必ず通常画面
        m_maskPass = new MaskPass(StabInkEffect.RenderingLayerBit)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
        m_compositePass = new CompositePass(m_compositeMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_compositeMaterial == null) { ChannelLogger.LogGuardReturn("Game", "StabInk合成マテリアル未設定"); return; }
        if (StabInkEffect.Strength <= 0f) return;   // 演出外はパスを積まない（ゼロコスト）
        if (renderingData.cameraData.cameraType == CameraType.SceneView) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;
        renderer.EnqueuePass(m_maskPass);
        renderer.EnqueuePass(m_compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        m_maskPass?.Cleanup();
    }

    // 専用レンダリングレイヤーのオブジェクト（＝スタブVFX）だけを、各自のマテリアルでマスクRTへ描く。
    class MaskPass : ScriptableRenderPass
    {
        private readonly uint m_renderingLayer;
        private readonly List<ShaderTagId> m_shaderTagIds = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };
        private RTHandle m_maskRT;

        public MaskPass(uint renderingLayer)
        {
            m_renderingLayer = renderingLayer;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            var desc = cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.ARGB32;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_maskRT, desc, name: "_StabInkMask");

            var drawingSettings = RenderingUtils.CreateDrawingSettings(
                m_shaderTagIds, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
            var filteringSettings = new FilteringSettings(RenderQueueRange.all, ~0);
            filteringSettings.renderingLayerMask = m_renderingLayer;   // スタブVFXだけ通す
            var rlParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererListHandle = renderGraph.CreateRendererList(rlParams);

            using (var builder = renderGraph.AddUnsafePass<MaskPassData>("StabInkMask", out var passData))
            {
                passData.maskRT = m_maskRT;
                passData.rendererList = rendererListHandle;

                builder.UseRendererList(rendererListHandle);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc<MaskPassData>(static (data, ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    cmd.SetRenderTarget(data.maskRT);
                    cmd.ClearRenderTarget(false, true, Color.white);   // 白背景の上に効果を合成する＝後ろの景色は出さない（透けさせない）
                    cmd.DrawRendererList(data.rendererList);
                    cmd.SetGlobalTexture("_StabInkMask", data.maskRT);
                });
            }
        }

        class MaskPassData
        {
            public RTHandle maskRT;
            public RendererListHandle rendererList;
        }

        public void Cleanup()
        {
            m_maskRT?.Release();
        }
    }

    // フルスクリーンで「マスクのある所＝黒 / 他＝白」に塗り替える（元の色は捨てる）。
    class CompositePass : ScriptableRenderPass
    {
        private readonly Material m_material;

        public CompositePass(Material material)
        {
            m_material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            m_material.SetFloat("_Strength", Mathf.Clamp01(StabInkEffect.Strength));

            var source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_StabInkTemp";
            var tempTex = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddUnsafePass<BlitPassData>("StabInkComposite", out var passData))
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

                    // source → temp（白黒インク合成）
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
