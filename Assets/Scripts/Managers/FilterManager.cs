using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += HandlePlayModeChange;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= HandlePlayModeChange;
#endif
    }

#if UNITY_EDITOR
    private void HandlePlayModeChange(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            crtRendererFeature?.SetActive(false);
            monitorFeature?.SetActive(false);
        }
    }
#endif

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