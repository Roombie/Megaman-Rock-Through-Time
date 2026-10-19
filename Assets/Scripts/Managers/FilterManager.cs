using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FilterManager : MonoBehaviour
{
    public static FilterManager Instance { get; private set; }

    /// <summary>Raised whenever the filter changes (used by rows that only apply to one filter).</summary>
    public static event System.Action<FilterMode> FilterChanged;

    public FilterMode CurrentFilter => currentMode;

    [Header("TV (CRT)")]
    [Tooltip("Bloom + vignette volume used by the TV filter.")]
    public Volume crtVolume;
    [Tooltip("Composite softness pass (CRTCompositeFeature) on the gameplay renderer.")]
    [SerializeField] private ScriptableRendererFeature crtRendererFeature;
    [SerializeField] private Shader crtScanlineShader;
    [Tooltip("How dark the gaps between scanlines get. Brightness is compensated, so the average stays the same.")]
    [SerializeField, Range(0f, 1f)] private float scanlineStrength = 0.35f;
    [Tooltip("Faint R/G/B phosphor columns; only shows from about 4x scale up.")]
    [SerializeField, Range(0f, 0.5f)] private float apertureGrilleStrength = 0.12f;
    [Tooltip("TV CURVED only: how much the picture bends (URP Lens Distortion intensity).")]
    [SerializeField, Range(0f, 0.6f)] private float curvedWarp = 0.25f;

    [Header("Monitor (LCD)")]
    [SerializeField] private Shader lcdGridShader;
    [SerializeField, Range(0f, 1f)] private float gridStrength = 0.3f;
    [Tooltip("Width of the dark gap between game pixels, as a fraction of a game pixel (never thinner than 1 screen pixel).")]
    [SerializeField, Range(0f, 0.5f)] private float gridGapWidth = 0.12f;
    [Tooltip("Faint RGB stripes inside each game pixel; only shows from about 5x scale up.")]
    [SerializeField, Range(0f, 0.5f)] private float subpixelStrength = 0.15f;
    [Tooltip("Handheld screens are a little washed out compared to a TV.")]
    [SerializeField, Range(-100f, 0f)] private float saturation = -15f;
    [SerializeField, Range(-100f, 0f)] private float contrast = -8f;

    [Header("Game Boy")]
    [Tooltip("Palette pass (GameBoyPaletteFeature) on the gameplay renderer.")]
    [SerializeField] private GameBoyPaletteFeature gameBoyFeature;
    [SerializeField, Range(0f, 1f)] private float gameBoyGridStrength = 0.2f;
    [Tooltip("Options menu GB PALETTE order: Green (DMG), Pocket, Light, Super Game Boy. 4 shades each, darkest first (sRGB).")]
    [SerializeField] private GameBoyPalette[] gameBoyPalettes =
    {
        new GameBoyPalette("Green", "0F380F", "306230", "8BAC0F", "9BBC0F"),
        new GameBoyPalette("Pocket", "1E1E1E", "4E4E4E", "8A8A8A", "C5C5BD"),
        new GameBoyPalette("Light", "0D2B2A", "1E6A5C", "43C1A2", "8AF2D5"),
        new GameBoyPalette("Super", "331E50", "A63725", "D68E49", "F7E7C6"),
    };

    [System.Serializable]
    public struct GameBoyPalette
    {
        public string name;
        public Color[] shades;

        public GameBoyPalette(string name, params string[] hex)
        {
            this.name = name;
            shades = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                ColorUtility.TryParseHtmlString("#" + hex[i], out shades[i]);
        }
    }

    [Header("Ghosting (Options menu toggle)")]
    [Tooltip("How much of the previous frame stays on screen when Ghosting is ON.")]
    [SerializeField, Range(0f, 0.9f)] private float ghosting = 0.35f;
    [FormerlySerializedAs("monitorFeature")]
    [SerializeField] private LCDGhostingFeature ghostingFeature;

    private const float NesLines = 240f;     // scanlines of the original hardware
    private const float MenuPixelSize = 2f;  // menus (768x720) draw their art on a 2 render px grid

    private FilterMode currentMode = FilterMode.None;
    private bool ghostingEnabled;

    // One overlay image over the game view; its material is the LCD grid, the CRT scanlines or the Game Boy grid
    private RawImage filterOverlay;
    private Material lcdGridMaterial;
    private Material gameBoyGridMaterial;
    private int gameBoyPaletteIndex;
    private Material crtScanlineMaterial;
    private Volume monitorVolume;
    private Volume warpVolume;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateFilterOverlay();
        monitorVolume = CreateRuntimeVolume("Monitor Volume", 10f, profile =>
        {
            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.Override(saturation);
            colorAdjustments.contrast.Override(contrast);
        });
        // Own volume (switched on/off as a whole) so the warp reliably follows the filter choice
        warpVolume = CreateRuntimeVolume("TV Curved Warp Volume", 11f, profile =>
        {
            var warp = profile.Add<LensDistortion>(true);
            warp.intensity.Override(curvedWarp);
            warp.xMultiplier.Override(1f);
            warp.yMultiplier.Override(1f);
            warp.scale.Override(1f);
        });
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += HandlePlayModeChange;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= HandlePlayModeChange;
#endif
    }

    private void OnDestroy()
    {
        if (lcdGridMaterial != null) Destroy(lcdGridMaterial);
        if (gameBoyGridMaterial != null) Destroy(gameBoyGridMaterial);
        if (crtScanlineMaterial != null) Destroy(crtScanlineMaterial);
        if (monitorVolume != null && monitorVolume.sharedProfile != null) Destroy(monitorVolume.sharedProfile);
        if (warpVolume != null && warpVolume.sharedProfile != null) Destroy(warpVolume.sharedProfile);
    }

#if UNITY_EDITOR
    private void HandlePlayModeChange(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            crtRendererFeature?.SetActive(false);
            gameBoyFeature?.SetActive(false);
            ghostingFeature?.SetActive(false);
        }
    }
#endif

    public void SetFilter(FilterMode mode)
    {
        Debug.Log($"Filter set to: {mode}");

        currentMode = mode;

        bool tv = mode == FilterMode.TV || mode == FilterMode.TVCurved;
        bool monitor = mode == FilterMode.Monitor;
        bool gameBoy = mode == FilterMode.GameBoy;

        if (crtVolume != null) crtVolume.gameObject.SetActive(tv);
        // TV Curved bends the picture like the original filter did (URP Lens Distortion)
        if (warpVolume != null) warpVolume.gameObject.SetActive(mode == FilterMode.TVCurved);
        if (crtRendererFeature != null) crtRendererFeature.SetActive(tv);
        if (monitorVolume != null) monitorVolume.gameObject.SetActive(monitor);
        if (gameBoyFeature != null)
        {
            ApplyGameBoyPalette();
            gameBoyFeature.SetActive(gameBoy);
        }

        if (filterOverlay != null)
        {
            filterOverlay.material = tv ? crtScanlineMaterial : gameBoy ? gameBoyGridMaterial : lcdGridMaterial;
            if (!tv && !monitor && !gameBoy) filterOverlay.gameObject.SetActive(false);
        }

        PlayerPrefs.SetInt(SettingsKeys.FilterKey, (int)mode);
        PlayerPrefs.Save();

        FilterChanged?.Invoke(mode);
    }

    /// <summary>
    /// GB PALETTE option: which 4 shades the Game Boy filter uses.
    /// </summary>
    public void SetGameBoyPalette(int index)
    {
        gameBoyPaletteIndex = Mathf.Clamp(index, 0, Mathf.Max(0, gameBoyPalettes.Length - 1));
        ApplyGameBoyPalette();
        PlayerPrefs.SetInt(SettingsKeys.GameBoyPaletteKey, gameBoyPaletteIndex);
    }

    private void ApplyGameBoyPalette()
    {
        if (gameBoyFeature == null || gameBoyPalettes.Length == 0) return;
        var palette = gameBoyPalettes[Mathf.Clamp(gameBoyPaletteIndex, 0, gameBoyPalettes.Length - 1)];
        if (palette.shades != null && palette.shades.Length >= 4)
            gameBoyFeature.shades = palette.shades;
    }

    /// <summary>
    /// Ghosting is its own Options toggle and works with any filter (slow LCD / CRT phosphor trails).
    /// </summary>
    public void SetGhosting(bool enabled)
    {
        ghostingEnabled = enabled;

        if (ghostingFeature != null)
        {
            ghostingFeature.amount = ghosting;
            ghostingFeature.SetActive(enabled && ghosting > 0f);
        }
    }

    private void LateUpdate()
    {
        if (filterOverlay == null) return;

        bool show = currentMode != FilterMode.None
                    && filterOverlay.material != null
                    && BorderManager.Instance != null
                    && BorderManager.Instance.TryGetGameView(out Rect gameRect, out float pixelScale)
                    && ApplyOverlay(gameRect, pixelScale);

        if (filterOverlay.gameObject.activeSelf != show)
            filterOverlay.gameObject.SetActive(show);
    }

    private bool ApplyOverlay(Rect gameRect, float pixelScale)
    {
        Material material = filterOverlay.material;
        float referenceLines = gameRect.height / pixelScale;

        // TV: always the NES's 240 scanlines, also on the 768x720 menus (3 render px per line there).
        // Monitor: the grid follows the art's pixels; the menus are drawn on a 2 render px grid.
        if (material == crtScanlineMaterial)
            pixelScale = gameRect.height / NesLines;
        else if (referenceLines > NesLines)
            pixelScale *= MenuPixelSize;

        // Keep the overlay aligned with those pixels: one UV unit = one pixel/scanline
        var rect = filterOverlay.rectTransform;
        if (rect.anchoredPosition != gameRect.position) rect.anchoredPosition = gameRect.position;
        if (rect.sizeDelta != gameRect.size) rect.sizeDelta = gameRect.size;

        var uv = new Rect(0f, 0f, gameRect.width / pixelScale, gameRect.height / pixelScale);
        if (filterOverlay.uvRect != uv) filterOverlay.uvRect = uv;

        material.SetFloat("_Zoom", pixelScale);

        if (material == crtScanlineMaterial)
        {
            material.SetFloat("_ScanlineStrength", scanlineStrength);
            material.SetFloat("_MaskStrength", apertureGrilleStrength);
        }
        else
        {
            // Game Boy: same LCD grid, lighter and without RGB stripes (monochrome screen)
            bool gameBoy = material == gameBoyGridMaterial;
            material.SetFloat("_GridStrength", gameBoy ? gameBoyGridStrength : gridStrength);
            material.SetFloat("_GapWidth", gridGapWidth);
            material.SetFloat("_SubpixelStrength", gameBoy ? 0f : subpixelStrength);
        }
        return true;
    }

    private void CreateFilterOverlay()
    {
        if (lcdGridShader == null) lcdGridShader = Shader.Find("UI/LCD Grid");
        if (crtScanlineShader == null) crtScanlineShader = Shader.Find("UI/CRT Scanlines");

        if (lcdGridShader != null)
        {
            lcdGridMaterial = new Material(lcdGridShader);
            gameBoyGridMaterial = new Material(lcdGridShader);
        }
        else Debug.LogWarning("FilterManager: LCD Grid shader not found, Monitor filter grid disabled.");

        if (crtScanlineShader != null) crtScanlineMaterial = new Material(crtScanlineShader);
        else Debug.LogWarning("FilterManager: CRT Scanlines shader not found, TV filter scanlines disabled.");

        // Overlay canvases are drawn after every camera, straight onto the final image
        var canvasObject = new GameObject("Filter Overlay", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var overlayObject = new GameObject("Filter Overlay Image", typeof(RectTransform), typeof(RawImage));
        overlayObject.transform.SetParent(canvasObject.transform, false);

        var overlayRect = (RectTransform)overlayObject.transform;
        overlayRect.anchorMin = overlayRect.anchorMax = overlayRect.pivot = Vector2.zero;

        filterOverlay = overlayObject.GetComponent<RawImage>();
        filterOverlay.raycastTarget = false;
        overlayObject.SetActive(false);
    }

    private Volume CreateRuntimeVolume(string volumeName, float priority, System.Action<VolumeProfile> setup)
    {
        var volumeObject = new GameObject(volumeName);
        volumeObject.transform.SetParent(transform, false);
        volumeObject.layer = 0; // cameras' volume mask is Default

        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        setup(profile);
        volume.sharedProfile = profile;

        volumeObject.SetActive(false);
        return volume;
    }
}

public enum FilterMode { None, TV, TVCurved, Monitor, GameBoy }
