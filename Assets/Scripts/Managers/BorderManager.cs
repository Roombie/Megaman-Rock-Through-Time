using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BorderManager : MonoBehaviour
{
    public static BorderManager Instance { get; private set; }

    [SerializeField] private PixelPerfectCamera pixelPerfectCamera;

    [Tooltip("Layer of the scene's border artwork (the RawImage rendered by the Background Camera).")]
    [SerializeField] private string borderImageLayer = "PixelPerfectOutput";

    [Tooltip("Minimum border left on every side in Boxed mode, as a fraction of the screen height.")]
    [SerializeField, Range(0.01f, 0.25f)] private float boxedMinMargin = 0.05f;

    private static bool isApplying = false;

    private BorderMode currentMode = BorderMode.None;
    private ScreenDisplayMode screenMode = ScreenDisplayMode.Original;

    // URP's 2D renderer clears the whole back buffer to black when a Pixel Perfect crop frame is active,
    // which wipes whatever the Background Camera drew. The border artwork is redrawn here on an overlay
    // canvas (rendered after every camera) as slices around the game view.
    private Canvas overlayCanvas;
    private readonly RawImage[] slices = new RawImage[4]; // left, right, bottom, top
    private RawImage sourceImage;
    private bool sourceSearched;

    private Rect lastGameRect;
    private Rect lastSourceRect;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        screenMode = (ScreenDisplayMode)PlayerPrefs.GetInt(SettingsKeys.ScreenKey, 0);
        CreateOverlay();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void Start()
    {
        SearchForCamera();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sourceImage = null;
        sourceSearched = false;
        lastGameRect = Rect.zero;

        pixelPerfectCamera = null;
        SearchForCamera();
        AssignGameCameraToCanvases();
    }

    /// <summary>
    /// Look for the game camera again (e.g. after the Options screen was shown over the game and unloaded).
    /// </summary>
    public void RefreshCamera()
    {
        pixelPerfectCamera = null;
        sourceImage = null;
        sourceSearched = false;
        SearchForCamera();
        ApplyFraming();
        AssignGameCameraToCanvases();
    }

    // A "Screen Space - Camera" canvas without a camera is drawn like an overlay: on top of everything, outside
    // the game frame and after every filter (TV / Monitor / Game Boy colours never reach it). Give those canvases
    // the game camera so they are framed, pixel perfect and filtered like the rest of the game.
    private void AssignGameCameraToCanvases()
    {
        if (pixelPerfectCamera == null) return;
        var gameCamera = pixelPerfectCamera.GetComponent<Camera>();

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                canvas.worldCamera = gameCamera;
        }
    }

    private void SearchForCamera()
    {
        if (pixelPerfectCamera == null)
            pixelPerfectCamera = FindFirstObjectByType<PixelPerfectCamera>();

        if (pixelPerfectCamera == null)
        {
            Debug.LogWarning("PixelPerfectCamera not found in BorderManager.");
        }
    }

    public void SetBorderMode(BorderMode mode)
    {
        if (isApplying) return;

        isApplying = true;

        currentMode = mode;
        ApplyFraming();

        PlayerPrefs.SetInt(SettingsKeys.BorderKey, (int)mode);
        PlayerPrefs.Save();

        Debug.Log($"Border set to: {mode}");

        isApplying = false;
    }

    /// <summary>
    /// Screen setting: the size and shape of the game frame (always exactly refResolutionY lines tall).
    /// </summary>
    public void SetScreenMode(ScreenDisplayMode mode)
    {
        screenMode = mode;
        ApplyFraming();
    }

    private void ApplyFraming()
    {
        SearchForCamera();
        if (pixelPerfectCamera == null) return;

        // The crop decides what the camera renders (always 240 lines in gameplay, so the view never
        // goes past the top or bottom of a 15-tile level); GetGameRect decides where it lands on screen.
        pixelPerfectCamera.cropFrame = screenMode switch
        {
            ScreenDisplayMode.Wide => PixelPerfectCamera.CropFrame.Letterbox,           // full rows, extra columns
            ScreenDisplayMode.Full => PixelPerfectCamera.CropFrame.StretchFill,         // smooth non-integer scale
            ScreenDisplayMode.WindowedFull => PixelPerfectCamera.CropFrame.StretchFill,
            _ => PixelPerfectCamera.CropFrame.Windowbox                                 // pixel perfect
        };

        lastGameRect = Rect.zero;
    }

    public void ApplyBorderClean(BorderMode mode)
    {
        StartCoroutine(DelayedBorderApplyWithUIRefresh(mode));
    }

    private IEnumerator DelayedBorderApplyWithUIRefresh(BorderMode mode)
    {
        // Wait for the screen change to be applied
        yield return new WaitForEndOfFrame(); // resolution
        yield return null;                    // layouts and UI scaler

        SetBorderMode(mode);

        // Clean UI
        yield return UIUtils.DelayedRefreshUI();
    }

    private void LateUpdate()
    {
        // Re-subscribe every frame so this runs after PixelPerfectCamera's own beginCameraRendering
        // handler (it subscribes in OnEnable, after this persistent manager) and can override its rect
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

        if (currentMode == BorderMode.None || pixelPerfectCamera == null || !FindSourceImage())
        {
            SetSlicesActive(false);
            return;
        }

        Rect gameRect = GetGameRect();
        // Same framing in every scene: the art covers the whole screen, keeping its aspect, centred.
        // (Each scene's own background canvas positioned it differently.)
        Rect sourceRect = CoverRect(sourceImage.texture, Screen.width, Screen.height);
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        if (gameRect == lastGameRect && sourceRect == lastSourceRect && screenSize == lastScreenSize)
            return;

        lastGameRect = gameRect;
        lastSourceRect = sourceRect;
        lastScreenSize = screenSize;

        float w = screenSize.x;
        float h = screenSize.y;

        UpdateSlice(slices[0], new Rect(0f, 0f, gameRect.xMin, h), sourceRect);
        UpdateSlice(slices[1], new Rect(gameRect.xMax, 0f, w - gameRect.xMax, h), sourceRect);
        UpdateSlice(slices[2], new Rect(gameRect.xMin, 0f, gameRect.width, gameRect.yMin), sourceRect);
        UpdateSlice(slices[3], new Rect(gameRect.xMin, gameRect.yMax, gameRect.width, h - gameRect.yMax), sourceRect);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (pixelPerfectCamera == null || camera.gameObject != pixelPerfectCamera.gameObject) return;

        // URP reads the camera rect after this callback, so the final blit lands in our frame
        camera.pixelRect = GetGameRect();
    }

    // Game pixels the camera renders: refResolutionY rows, and refResolutionX columns except in Wide,
    // where the column count follows the screen aspect (same formula as PixelPerfectCamera's Letterbox).
    private Vector2Int GetSourceSize()
    {
        int refX = pixelPerfectCamera.refResolutionX;
        int refY = pixelPerfectCamera.refResolutionY;

        if (screenMode != ScreenDisplayMode.Wide)
            return new Vector2Int(refX, refY);

        int zoom = Mathf.Max(1, Mathf.Min(Screen.height / refY, Screen.width / refX));
        int columns = pixelPerfectCamera.gridSnapping == PixelPerfectCamera.GridSnapping.UpscaleRenderTexture ? Screen.width / zoom / 2 * 2 : Screen.width / zoom;
        return new Vector2Int(Mathf.Max(columns, refX), refY);
    }

    // Where the game frame goes on screen:
    // - Original / Windowed / Wide: biggest integer scale (pixel perfect)
    // - Full / WindowedFull: biggest scale that fits, non-integer
    // - Boxed border keeps a margin on every side; if not even 1x fits, the frame shrinks to fit
    private Rect GetGameRect()
    {
        Vector2Int source = GetSourceSize();
        int screenW = Screen.width;
        int screenH = Screen.height;

        int margin = currentMode == BorderMode.Windowbox ? Mathf.Max(1, Mathf.RoundToInt(screenH * boxedMinMargin)) : 0;
        int maxW = Mathf.Max(2, screenW - margin * 2);
        int maxH = Mathf.Max(2, screenH - margin * 2);

        float fit = Mathf.Min((float)maxW / source.x, (float)maxH / source.y);
        float scale = fit;

        bool integerScale = screenMode == ScreenDisplayMode.Original
                            || screenMode == ScreenDisplayMode.Windowed
                            || screenMode == ScreenDisplayMode.Wide;
        if (integerScale && fit >= 1f)
            scale = Mathf.Floor(fit);

        float frameW = Mathf.Max(2f, Mathf.Floor(source.x * scale / 2f) * 2f);
        float frameH = Mathf.Max(2f, Mathf.Floor(source.y * scale / 2f) * 2f);

        return new Rect(Mathf.Floor((screenW - frameW) / 2f), Mathf.Floor((screenH - frameH) / 2f), frameW, frameH);
    }

    /// <summary>
    /// Where the game is drawn on screen (pixels) and how many screen pixels one game pixel takes.
    /// </summary>
    public bool TryGetGameView(out Rect gameRect, out float pixelScale)
    {
        gameRect = default;
        pixelScale = 1f;

        if (pixelPerfectCamera == null || !pixelPerfectCamera.isActiveAndEnabled)
            return false;

        gameRect = GetGameRect();
        pixelScale = gameRect.height / pixelPerfectCamera.refResolutionY;
        return gameRect.width > 0f && gameRect.height > 0f;
    }

    private bool FindSourceImage()
    {
        if (sourceImage != null) return true;
        if (sourceSearched) return false;

        sourceSearched = true;
        int layer = LayerMask.NameToLayer(borderImageLayer);

        foreach (var image in FindObjectsByType<RawImage>(FindObjectsSortMode.None))
        {
            if (image.gameObject.layer == layer && image.texture != null)
            {
                sourceImage = image;
                return true;
            }
        }

        Debug.LogWarning($"BorderManager: no border RawImage found on layer '{borderImageLayer}'.");
        return false;
    }

    private void CreateOverlay()
    {
        var canvasObject = new GameObject("Border Overlay", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = -100;

        for (int i = 0; i < slices.Length; i++)
        {
            var sliceObject = new GameObject($"Border Slice {i}", typeof(RectTransform), typeof(RawImage));
            sliceObject.transform.SetParent(canvasObject.transform, false);

            var rect = (RectTransform)sliceObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;

            slices[i] = sliceObject.GetComponent<RawImage>();
            slices[i].raycastTarget = false;
            sliceObject.SetActive(false);
        }
    }

    // Shows the part of the source image that lies under sliceRect, so the slices look exactly
    // like the Background Camera's full-screen image with the game view cut out.
    private void UpdateSlice(RawImage slice, Rect sliceRect, Rect sourceRect)
    {
        bool visible = sliceRect.width > 0f && sliceRect.height > 0f && sourceRect.width > 0f && sourceRect.height > 0f;
        slice.gameObject.SetActive(visible);
        if (!visible) return;

        var rect = slice.rectTransform;
        rect.anchoredPosition = sliceRect.position / overlayCanvas.scaleFactor;
        rect.sizeDelta = sliceRect.size / overlayCanvas.scaleFactor;

        Rect uv = new Rect(0f, 0f, 1f, 1f);
        slice.texture = sourceImage.texture;
        slice.material = sourceImage.material;
        slice.color = sourceImage.color;
        slice.uvRect = new Rect(
            uv.x + (sliceRect.x - sourceRect.x) / sourceRect.width * uv.width,
            uv.y + (sliceRect.y - sourceRect.y) / sourceRect.height * uv.height,
            sliceRect.width / sourceRect.width * uv.width,
            sliceRect.height / sourceRect.height * uv.height);
    }

    // Screen rect of a texture scaled to cover the whole screen (keeping its aspect), centred
    private static Rect CoverRect(Texture texture, float screenW, float screenH)
    {
        if (texture == null || texture.width == 0 || texture.height == 0)
            return new Rect(0f, 0f, screenW, screenH);
        float scale = Mathf.Max(screenW / texture.width, screenH / texture.height);
        float w = texture.width * scale, h = texture.height * scale;
        return new Rect((screenW - w) / 2f, (screenH - h) / 2f, w, h);
    }


    private void SetSlicesActive(bool active)
    {
        foreach (var slice in slices)
        {
            if (slice != null && slice.gameObject.activeSelf != active)
                slice.gameObject.SetActive(active);
        }

        if (!active) lastGameRect = Rect.zero;
    }
}

public enum BorderMode { None, Pillarbox, Windowbox }
