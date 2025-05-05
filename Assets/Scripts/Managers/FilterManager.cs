using UnityEngine;
using UnityEngine.Rendering;

public class FilterManager : MonoBehaviour
{
    public static FilterManager Instance { get; private set; }

    [Header("Post-Processing Volume")]
    public Volume crtVolume;
    public Material crtMaterial;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetFilter((FilterMode)PlayerPrefs.GetInt(SettingsKeys.FilterKey, 0));
    }

    public void SetFilter(FilterMode mode)
    {
        Debug.Log($"Filter set to: {mode}");

        switch (mode)
        {
            case FilterMode.None:
                if (crtVolume != null) crtVolume.gameObject.SetActive(false);
                break;

            case FilterMode.TV:
                if (crtVolume != null) crtVolume.gameObject.SetActive(true);
                break;

            case FilterMode.Monitor:
                // Si tienes un segundo filtro estilo "monitor", podrías activarlo aquí
                if (crtVolume != null) crtVolume.gameObject.SetActive(false);
                break;
        }
    }
}

public enum FilterMode { None, TV, Monitor }