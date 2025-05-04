using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

public class ToggleSettingHandler : MonoBehaviour, ISettingHandler
{
    [Header("Setting Config")]
    [SettingTypeFilter(SettingType.VSync, SettingType.SlideWithDownJumpKey, SettingType.ControllerVibrationKey)]
    public SettingType settingType;

    [Header("UI")]
    public Toggle toggle;
    public Image targetImage;

    [Header("Localization")]
    public LanguageSprites languageSprites;

    private bool currentValue;
    public SettingType SettingType => settingType;

    #if UNITY_EDITOR
    private void OnValidate()
    {
        var allowed = new SettingType[]
        {
            SettingType.VSync,
            SettingType.SlideWithDownJumpKey,
            SettingType.ControllerVibrationKey
        };

        if (!System.Array.Exists(allowed, t => t == settingType))
        {
            Debug.LogWarning($"{nameof(ToggleSettingHandler)}: Invalid SettingType '{settingType}' assigned. Resetting to default.");
            settingType = allowed[0];
        }
    }
    #endif

    void Start()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.AddListener(OnToggleChanged);
        }

        ApplyFromSaved();
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }

    private void OnToggleChanged(bool value)
    {
        Apply(value);
        Save();
    }

    public void Toggle()
    {
        Apply(!currentValue);
        Save();

        if (toggle != null)
            toggle.isOn = currentValue;
    }

    public void Apply(bool value)
    {
        currentValue = value;

        if (toggle != null)
            toggle.isOn = currentValue;

        if (targetImage != null && languageSprites != null && LocalizationSettings.SelectedLocale != null)
        {
            var sprite = languageSprites.GetSprite(LocalizationSettings.SelectedLocale, currentValue);
            if (sprite != null)
                targetImage.sprite = sprite;
        }

        SettingsApplier.ApplyBoolSetting(settingType, currentValue);
    }

    public void Apply(int index) { }

    public void ApplyFromSaved()
    {
        currentValue = PlayerPrefs.GetInt(SettingsKeys.Get(settingType), 0) == 1;
        Apply(currentValue);
    }

    public void Save() => PlayerPrefs.SetInt(SettingsKeys.Get(settingType), currentValue ? 1 : 0);
    public void RefreshUI() => Apply(currentValue);
}