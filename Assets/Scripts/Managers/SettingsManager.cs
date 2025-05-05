using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Optional Subsystems")]
    public GamepadIconManager gamepadIconManager;
    public FilterManager filterManager;
    public ScreenDisplayManager screenDisplayManager;
    public BorderManager borderManager;

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