using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// TV filter softness. Runs after post-processing, while the Pixel Perfect Camera is still rendering at
// game resolution, so the blur is measured in game pixels (the scanlines are drawn later at screen resolution).
public class CRTCompositeFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    [Tooltip("How much each pixel bleeds into its left/right neighbours (0 = off).")]
    [Range(0f, 0.33f)] public float blur = 0.15f;

    private Material material;
    private CRTCompositePass pass;

    public override void Create()
    {
        pass = new CRTCompositePass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
            requiresIntermediateTexture = true
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (blur <= 0f || renderingData.cameraData.cameraType != CameraType.Game)
            return;

        if (material == null)
        {
            if (shader == null) shader = Shader.Find("Hidden/CRT Composite");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        material.SetFloat("_Blur", blur);
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    private class CRTCompositePass : ScriptableRenderPass
    {
        private Material material;

        public void Setup(Material material) => this.material = material;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle color = resourceData.activeColorTexture;
            RenderTargetInfo info = renderGraph.GetRenderTargetInfo(color);
            material.SetVector("_SourceTexelSize", new Vector4(1f / info.width, 1f / info.height, info.width, info.height));

            TextureDesc desc = renderGraph.GetTextureDesc(color);
            desc.name = "_CRTCompositeTemp";
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = false;
            TextureHandle temp = renderGraph.CreateTexture(desc);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(color, temp, material, 0), "CRT Composite");
            renderGraph.AddBlitPass(temp, color, Vector2.one, Vector2.zero, passName: "CRT Composite Copy");
        }
    }
}
