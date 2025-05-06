using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FilterManager : MonoBehaviour
{
    public static FilterManager Instance { get; private set; }

    [Header("Post-Processing Volume")]
    public Volume crtVolume;
    [SerializeField] private ScriptableRendererFeature crtRendererFeature;
    [SerializeField] private ScriptableRendererFeature monitorFeature;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetFilter((FilterMode)PlayerPrefs.GetInt(SettingsKeys.FilterKey, 0));
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += HandlePlayModeChange;
#endif
        ApplySavedFilter();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeChange;
#endif
    }

    private void HandlePlayModeChange(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
        {
            crtRendererFeature?.SetActive(false);
            monitorFeature?.SetActive(false);
        }
    }

    private void ApplySavedFilter()
    {
        var savedMode = (FilterMode)PlayerPrefs.GetInt(SettingsKeys.FilterKey, 0);
        SetFilter(savedMode);
    }

    public void SetFilter(FilterMode mode)
    {
        Debug.Log($"Filter set to: {mode}");

        if (crtVolume != null) crtVolume.gameObject.SetActive(false);
        if (crtRendererFeature != null) crtRendererFeature.SetActive(false);
        if (monitorFeature != null) monitorFeature.SetActive(false);

        switch (mode)
        {
            case FilterMode.TV:
                crtVolume?.gameObject.SetActive(true);
                crtRendererFeature?.SetActive(true);
                break;

            case FilterMode.Monitor:
                monitorFeature?.SetActive(true);
                break;

            case FilterMode.None:
            default:
                break;
        }

        PlayerPrefs.SetInt(SettingsKeys.FilterKey, (int)mode);
        PlayerPrefs.Save();
    }
}

public enum FilterMode { None, TV, Monitor }