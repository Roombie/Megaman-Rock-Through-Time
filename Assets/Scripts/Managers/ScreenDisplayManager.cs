using System.Collections;
using UnityEngine;

public class ScreenDisplayManager : MonoBehaviour
{
    public static ScreenDisplayManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Apply(ScreenDisplayMode mode, bool save = true)
    {
        Debug.Log($"Screen display set to: {mode}");

        switch (mode)
        {
            case ScreenDisplayMode.Original:
                Screen.SetResolution(960, 720, FullScreenMode.FullScreenWindow); // 4:3, NES x3
                break;

            case ScreenDisplayMode.Full:
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                break;

            case ScreenDisplayMode.Windowed:
                Screen.SetResolution(960, 720, FullScreenMode.Windowed); // Fijo en 4:3, ventana
                break;

            case ScreenDisplayMode.WidedExpand:
            {
                // Usa resolución 16:9 para expandir horizontalmente
                int width = 960;  // o 1280, 1920 según lo que prefieras
                int height = 540; // 16:9 ratio
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);

                Camera mainCam = Camera.main;
                if (mainCam != null && mainCam.orthographic)
                {
                    float baseOrthoSize = 240f / 2f / 16f; // PPU = 16 → 240px alto base
                    mainCam.orthographicSize = baseOrthoSize;

                    // Esto dejará que se vea más mundo horizontal por el aspect ratio
                    float windowAspect = (float)width / height;
                    Debug.Log($"[WidedExpand] Aspect: {windowAspect}, OrthoSize: {mainCam.orthographicSize}");
                }

                break;
            }
        }

        if (save)
            PlayerPrefs.SetInt(SettingsKeys.ScreenKey, (int)mode);

        StartCoroutine(ApplyDelayedBorder());
    }

    private IEnumerator ApplyDelayedBorder()
    {
        yield return new WaitForEndOfFrame();

        if (BorderManager.Instance != null)
        {
            int borderIndex = PlayerPrefs.GetInt(SettingsKeys.BorderKey, 0);
            BorderManager.Instance.ApplyBorderClean((BorderMode)borderIndex);
        }
    }
}

public enum ScreenDisplayMode { Original, Full, Windowed, WidedExpand }
