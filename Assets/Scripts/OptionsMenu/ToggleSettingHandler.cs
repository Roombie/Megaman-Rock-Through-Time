using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class ToggleSettingHandler : MonoBehaviour, ISettingHandler
{
    [Header("Setting Config")]
    [SettingTypeFilter(SettingType.VSync, SettingType.SlideWithDownJumpKey, SettingType.ControllerVibrationKey, SettingType.Ghosting)]
    public SettingType settingType;
    [SerializeField] private AudioEvent toggleSound;

    [Header("UI")]
    public Toggle toggle;
    [Tooltip("Shows ON / OFF (localized) next to the setting.")]
    public TMP_Text valueText;
    [SerializeField] private Color onColor = new Color32(0x32, 0xC0, 0xFF, 0xFF);
    [SerializeField] private Color offColor = new Color32(0xFF, 0x32, 0x32, 0xFF);

    [Header("Localization (GameText table)")]
    [SerializeField] private string onKey = "Toggle_On";
    [SerializeField] private string offKey = "Toggle_Off";

    private bool currentValue;

    public SettingType SettingType => settingType;

    void Start()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.AddListener(OnToggleChanged);
        }

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        ApplyFromSaved();
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale locale) => UpdateValueText();

    private void UpdateValueText()
    {
        if (valueText == null) return;

        string key = currentValue ? onKey : offKey;
        string localized = LocalizationSettings.StringDatabase.GetLocalizedString("GameText", key);
        valueText.text = string.IsNullOrEmpty(localized) ? (currentValue ? "ON" : "OFF") : localized;
        valueText.color = currentValue ? onColor : offColor;
    }

    private void OnToggleChanged(bool value)
    {
        Apply(value);
        Save();
        if (toggleSound != null && Audio.IsReady)
        {
            Audio.Play(toggleSound);
        }
        else
        {
            Debug.LogWarning("Either a toggleSound wasn't referenced or the AudioManager isn't in the scene");
        }
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
            toggle.SetIsOnWithoutNotify(currentValue);

        if (LocalizationSettings.SelectedLocale == null)
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[0];
        }

        UpdateValueText();

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