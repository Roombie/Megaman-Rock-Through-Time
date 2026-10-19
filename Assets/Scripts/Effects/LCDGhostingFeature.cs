using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Monitor filter ghosting. Runs after post-processing, while the Pixel Perfect Camera is still
// rendering at game resolution, so the blend is per game pixel and cheap.
public class LCDGhostingFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    [Tooltip("How much of the previous frame stays on screen (0 = off).")]
    [Range(0f, 0.9f)] public float amount = 0f;

    private Material material;
    private LCDGhostingPass pass;

    public override void Create()
    {
        pass = new LCDGhostingPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
            requiresIntermediateTexture = true
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (amount <= 0f || renderingData.cameraData.cameraType != CameraType.Game)
            return;

        if (material == null)
        {
            if (shader == null) shader = Shader.Find("Hidden/LCD Ghosting");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        material.SetFloat("_Amount", amount);
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
        material = null;
    }

    private class LCDGhostingPass : ScriptableRenderPass
    {
        private class PassData
        {
            public TextureHandle history;
            public Material material;
        }

        private Material material;
        private RTHandle history;
        private bool historyValid;
        private int lastFrame = -1;

        public void Setup(Material material)
        {
            this.material = material;

            // Don't blend in a stale frame from the last time the filter was on
            if (lastFrame != Time.frameCount - 1 && lastFrame != Time.frameCount)
                historyValid = false;
            lastFrame = Time.frameCount;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle color = resourceData.activeColorTexture;

            TextureDesc desc = renderGraph.GetTextureDesc(color);
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = false;
            desc.filterMode = UnityEngine.FilterMode.Point;
            desc.wrapMode = TextureWrapMode.Clamp;

            if (RenderingUtils.ReAllocateHandleIfNeeded(ref history, desc, "_LCDGhostingHistory"))
                historyValid = false;

            TextureHandle historyHandle = renderGraph.ImportTexture(history);

            if (historyValid)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("LCD Ghosting", out var passData))
                {
                    passData.history = historyHandle;
                    passData.material = material;

                    builder.UseTexture(historyHandle);
                    builder.SetRenderAttachment(color, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, data.history, new Vector4(1f, 1f, 0f, 0f), data.material, 0));
                }
            }

            // The blended result becomes the next frame's history (exponential trail like a slow LCD)
            renderGraph.AddBlitPass(color, historyHandle, Vector2.one, Vector2.zero, passName: "LCD Ghosting History");
            historyValid = true;
        }

        public void Dispose()
        {
            history?.Release();
            history = null;
        }
    }
}
