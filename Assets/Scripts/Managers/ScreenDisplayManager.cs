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

        StartCoroutine(ApplyDelayedBorder(borderIndex));
    }

    void SetResolution(Vector2 targetResolution, ScreenDisplayMode mode)
    {
        FullScreenMode desiredMode;

        if (mode == ScreenDisplayMode.WindowedFull)
            desiredMode = FullScreenMode.MaximizedWindow;  // Aquí tu modo preferido
        else if (mode == ScreenDisplayMode.Windowed)
            desiredMode = FullScreenMode.Windowed;
        else
            desiredMode = FullScreenMode.FullScreenWindow;

        if (lastResolution == targetResolution && lastFullScreenMode == desiredMode)
            return;  // evitar llamadas repetidas

        Screen.SetResolution((int)targetResolution.x, (int)targetResolution.y, desiredMode);

        lastResolution = targetResolution;
        lastFullScreenMode = desiredMode;
    }

    private IEnumerator ApplyDelayedBorder(int borderIndex)
    {
        yield return new WaitForEndOfFrame();

        if (BorderManager.Instance != null)
        {
            BorderManager.Instance.SetBorderMode((BorderMode)borderIndex);
        }
    }
}

public enum ScreenDisplayMode { Original, Full, Windowed, WindowedFull, Wide }