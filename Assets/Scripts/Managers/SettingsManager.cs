using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Optional Subsystems")]
    public GamepadIconManager gamepadIconManager;
    public FilterManager filterManager;
    public ScreenDisplayManager screenDisplayManager;
    public BorderManager borderManager;
    public LocalizationManager localizationManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplySavedSettings();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySavedSettings();
    }

    public void ApplySavedSettings()
    {
        foreach (SettingType type in System.Enum.GetValues(typeof(SettingType)))
        {
            if (type == SettingType.MasterVolumeKey ||
                type == SettingType.MusicVolumeKey ||
                type == SettingType.SFXVolumeKey ||
                type == SettingType.VoiceVolumeKey) continue;

            int savedIndex = PlayerPrefs.GetInt(SettingsKeys.Get(type), 0);

            if (type == SettingType.VSync ||
                type == SettingType.SlideWithDownJumpKey || type == SettingType.ControllerVibrationKey)
            {
                SettingsApplier.ApplyBoolSetting(type, savedIndex == 1);
            }
            else
            {
                SettingsApplier.ApplyDisplaySetting(type, savedIndex);
            }
        }
    }
}