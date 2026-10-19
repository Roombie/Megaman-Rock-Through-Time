using System.Collections;
using UnityEngine;

public class ScreenDisplayManager : MonoBehaviour
{
    public static ScreenDisplayManager Instance { get; private set; }

    private Vector2 lastResolution = Vector2.zero;
    private FullScreenMode lastFullScreenMode = FullScreenMode.FullScreenWindow;

    [Tooltip("Draws camera / resolution info in the top-left corner (debug).")]
    [SerializeField] private bool showDebugInfo = true;

    void OnGUI()
    {
        if (!showDebugInfo) return;
        // No main camera while the Options overlay hides the game's cameras, or during a scene change
        var cam = Camera.main;
        GUI.Label(new Rect(10, 10, 300, 20), cam != null ? $"Orthographic Size: {cam.orthographicSize}" : "Orthographic Size: (no main camera)");
        GUI.Label(new Rect(10, 30, 300, 20), $"Screen Resolution: {Screen.width}x{Screen.height}");
        GUI.Label(new Rect(10, 50, 300, 20), $"Aspect Ratio: {(float)Screen.width / Screen.height:F2}");
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // NES resolution, used to size the Windowed mode window
    private const int GameWidth = 256;
    private const int GameHeight = 240;

    public void Apply(ScreenDisplayMode mode, bool save = true)
    {
        Debug.Log($"Screen display set to: {mode}");

        int borderIndex = PlayerPrefs.GetInt(SettingsKeys.BorderKey, 0);

        // Fullscreen always runs at the monitor's native resolution: any other size gets stretched by the
        // GPU/monitor with smoothing, which blurs pixel art. The frame shape is handled by BorderManager.
        DisplayInfo display = Screen.mainWindowDisplayInfo;
        Vector2 nativeResolution = display.width > 0
            ? new Vector2(display.width, display.height)
            : new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);

        Vector2 targetResolution = mode == ScreenDisplayMode.Windowed
            ? GetWindowedResolution(display)
            : nativeResolution;

        SetResolution(targetResolution, mode);
        BorderManager.Instance?.SetScreenMode(mode);

        if (save)
        {
            PlayerPrefs.SetInt(SettingsKeys.ScreenKey, (int)mode);
            PlayerPrefs.Save();
        }

        StartCoroutine(ApplyDisplayAndBorder(borderIndex, mode));
    }

    // Biggest whole multiple of the NES resolution that fits the desktop work area
    // (minus room for the title bar), e.g. 1024x960 on 1080p and 512x480 on 768p
    private static Vector2 GetWindowedResolution(DisplayInfo display)
    {
        int areaW = display.workArea.width > 0 ? display.workArea.width : Screen.currentResolution.width;
        int areaH = display.workArea.height > 0 ? display.workArea.height : Screen.currentResolution.height;

        const int windowChrome = 40;
        int scale = Mathf.Max(1, Mathf.Min(areaW / GameWidth, (areaH - windowChrome) / GameHeight));
        return new Vector2(GameWidth * scale, GameHeight * scale);
    }

    void SetResolution(Vector2 targetResolution, ScreenDisplayMode mode)
    {
        FullScreenMode desiredMode;

        if (mode == ScreenDisplayMode.WindowedFull)
            desiredMode = FullScreenMode.MaximizedWindow;
        else if (mode == ScreenDisplayMode.Windowed)
            desiredMode = FullScreenMode.Windowed;
        else
            desiredMode = FullScreenMode.FullScreenWindow;

        if (lastResolution == targetResolution && lastFullScreenMode == desiredMode)
            return;  // avoid repetitive calls

        Screen.SetResolution((int)targetResolution.x, (int)targetResolution.y, desiredMode);

        lastResolution = targetResolution;
        lastFullScreenMode = desiredMode;
    }

    private IEnumerator ApplyDisplayAndBorder(int borderIndex, ScreenDisplayMode mode)
    {
        yield return new WaitForSecondsRealtime(0.1f); // Wait for resolution & screen mode to settle

        BorderMode selectedBorder = (BorderMode)borderIndex;

        if (BorderManager.Instance != null)
            BorderManager.Instance.SetBorderMode(selectedBorder);
    }
}

public enum ScreenDisplayMode { Original, Full, Windowed, WindowedFull, Wide }