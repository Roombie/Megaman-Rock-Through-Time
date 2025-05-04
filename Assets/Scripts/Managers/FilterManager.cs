using UnityEngine;

public class FilterManager : MonoBehaviour
{
    public static FilterManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetFilter((FilterMode)PlayerPrefs.GetInt(SettingsKeys.FilterKey, 0));
    }

    public void SetFilter(FilterMode mode)
    {
        // Aplica shaders, post-processing, overlays, etc.
        Debug.Log($"CRT filter set to: {mode}");
        // TODO: cambia efectos de pantalla
    }
}

public enum FilterMode { None, CRT, Monitor }