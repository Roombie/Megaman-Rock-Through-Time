using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Game Boy filter colours. Runs after post-processing, while the Pixel Perfect Camera is still rendering at
// game resolution, so every game pixel maps to exactly one of the 4 shades.
public class GameBoyPaletteFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    [Tooltip("Darkest to lightest (sRGB).")]
    public Color[] shades =
    {
        new Color32(0x0F, 0x38, 0x0F, 0xFF),
        new Color32(0x30, 0x62, 0x30, 0xFF),
        new Color32(0x8B, 0xAC, 0x0F, 0xFF),
        new Color32(0x9B, 0xBC, 0x0F, 0xFF),
    };

    private static readonly int[] ShadeIds =
    {
        Shader.PropertyToID("_Shade0"), Shader.PropertyToID("_Shade1"),
        Shader.PropertyToID("_Shade2"), Shader.PropertyToID("_Shade3"),
    };

    private Material material;
    private GameBoyPalettePass pass;

    public override void Create()
    {
        pass = new GameBoyPalettePass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
            requiresIntermediateTexture = true
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game || shades == null || shades.Length < 4)
            return;

        if (material == null)
        {
            if (shader == null) shader = Shader.Find("Hidden/Game Boy Palette");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        for (int i = 0; i < 4; i++)
            material.SetColor(ShadeIds[i], shades[i]);   // SetColor converts sRGB -> linear in linear projects

        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    private class GameBoyPalettePass : ScriptableRenderPass
    {
        private Material material;

        public void Setup(Material material) => this.material = material;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle color = resourceData.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(color);
            desc.name = "_GameBoyPaletteTemp";
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = false;
            TextureHandle temp = renderGraph.CreateTexture(desc);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(color, temp, material, 0), "Game Boy Palette");
            renderGraph.AddBlitPass(temp, color, Vector2.one, Vector2.zero, passName: "Game Boy Palette Copy");
        }
    }
}
