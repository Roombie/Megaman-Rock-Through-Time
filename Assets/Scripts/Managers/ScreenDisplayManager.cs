using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ScreenDisplayManager : MonoBehaviour
{
    public static ScreenDisplayManager Instance { get; private set; }

    private Vector2 lastResolution = Vector2.zero;
    private FullScreenMode lastFullScreenMode = FullScreenMode.FullScreenWindow;

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 20), $"Orthographic Size: {Camera.main.orthographicSize}");
        GUI.Label(new Rect(10, 30, 300, 20), $"Screen Resolution: {Screen.width}x{Screen.height}");
        GUI.Label(new Rect(10, 50, 300, 20), $"Aspect Ratio: {(float)Screen.width / Screen.height:F2}");
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Apply(ScreenDisplayMode mode, bool save = true)
    {
        Debug.Log($"Screen display set to: {mode}");

        int borderIndex = PlayerPrefs.GetInt(SettingsKeys.BorderKey, 0);
        Vector2 targetResolution = Vector2.zero;

        switch (mode)
        {
            case ScreenDisplayMode.Original:
                targetResolution = new Vector2(1195f, 896f);
                break;

            case ScreenDisplayMode.Full:
                targetResolution = new Vector2(1440, 1080);
                break;

            case ScreenDisplayMode.Windowed:
                targetResolution = new Vector2(1024, 720);
                break;

            case ScreenDisplayMode.WindowedFull:
                targetResolution = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);
                break;

            case ScreenDisplayMode.Wide:
                targetResolution = new Vector2(1920, 1080);
                break;
        }

        SetResolution(targetResolution, mode);

        if (save)
        {
            PlayerPrefs.SetInt(SettingsKeys.ScreenKey, (int)mode);
            PlayerPrefs.Save();
        }

        StartCoroutine(ApplyDisplayAndBorder(borderIndex, mode));
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

        // Apply minimum resolution for Windowbox only if not in MaximizedWindow
        if (selectedBorder == BorderMode.Windowbox && mode != ScreenDisplayMode.WindowedFull)
        {
            var cam = FindFirstObjectByType<PixelPerfectCamera>();
            if (cam != null)
            {
                int minWidth = cam.refResolutionX * 2;
                int minHeight = cam.refResolutionY * 2;

                if (Screen.width < minWidth || Screen.height < minHeight)
                {
                    Debug.Log("[ScreenDisplayManager] Forcing resolution minimum for Windowbox");
                    Screen.SetResolution(minWidth, minHeight, Screen.fullScreenMode);
                    yield return new WaitForEndOfFrame(); // wait for resize to complete
                }
            }
        }

        if (BorderManager.Instance != null)
            BorderManager.Instance.SetBorderMode(selectedBorder);
    }
}

public enum ScreenDisplayMode { Original, Full, Windowed, WindowedFull, Wide }