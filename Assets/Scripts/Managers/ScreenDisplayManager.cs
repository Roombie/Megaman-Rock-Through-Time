using UnityEngine;

public class ScreenDisplayManager : MonoBehaviour
{
    public static ScreenDisplayManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Aplicar configuración guardada
        var saved = PlayerPrefs.GetInt(SettingsKeys.ScreenKey, 0);
        Apply((ScreenDisplayMode)saved, save: false);
    }

    public void Apply(ScreenDisplayMode mode, bool save = true)
    {
        Debug.Log($"Screen display set to: {mode}");

        switch (mode)
        {
            case ScreenDisplayMode.Original:
                Screen.SetResolution(320 * 3, 240 * 3, FullScreenMode.FullScreenWindow); // NES x3 ()
                break;
            case ScreenDisplayMode.Full:
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                break;
            case ScreenDisplayMode.Windowed:
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
        }

        if (save)
            PlayerPrefs.SetInt(SettingsKeys.ScreenKey, (int)mode);
    }
}

public enum ScreenDisplayMode { Original, Full, Windowed }
