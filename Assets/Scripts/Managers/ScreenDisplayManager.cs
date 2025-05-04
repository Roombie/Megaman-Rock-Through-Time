using UnityEngine;

public class ScreenDisplayManager : MonoBehaviour
{
    public static ScreenDisplayManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Apply((ScreenDisplayMode)PlayerPrefs.GetInt(SettingsKeys.ScreenKey, 0));
    }

    public void Apply(ScreenDisplayMode mode)
    {
        // Aquí aplicas visualmente tu modo de pantalla
        Debug.Log($"Screen display set to: {mode}");
        // TODO: cambia layout, HUD, aspect ratio, etc.
    }
}

public enum ScreenDisplayMode { Original, Full, Windowed }