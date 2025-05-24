using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Linq;
using System.Collections;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using UnityEngine.InputSystem.Samples.RebindUI;
using UnityEngine.AddressableAssets;

public class OptionsMenu : MonoBehaviour, ISettingsProvider
{
    private Resolution[] resolutions;
    private AsyncOperationHandle<string> graphicsTextHandle;
    private const string DisplayModeKeyPrefix = "DisplayMode_";

    void Start()
    {
        resolutions = Screen.resolutions;
        StartCoroutine(InitializeAfterLocalizationReady());
    }

    private IEnumerator InitializeAfterLocalizationReady()
    {
        yield return LocalizationSettings.InitializationOperation;
        yield return new WaitForEndOfFrame();

        int languageIndex = GetSavedIndex(SettingType.Language);
        if (languageIndex >= 0 && languageIndex < LocalizationSettings.AvailableLocales.Locales.Count)
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[languageIndex];
        }

        foreach (var selector in FindObjectsByType<OptionSelectorSettingHandler>(FindObjectsSortMode.None))
        {
            selector.RefreshUI();
        }

        foreach (var toggle in FindObjectsByType<ToggleSettingHandler>(FindObjectsSortMode.None))
        {
            toggle.RefreshUI();
        }
    }

    private void SetAndSaveVolume(SettingType type, float value)
    {
        PlayerPrefs.SetFloat(SettingsKeys.Get(type), value);
        AudioManager.Instance?.SetVolume(type, value);
    }

    public void ResetSettingsToDefault()
    {
        // VOLUME
        SetAndSaveVolume(SettingType.MasterVolumeKey, 1f);
        SetAndSaveVolume(SettingType.MusicVolumeKey, 0.8f);
        SetAndSaveVolume(SettingType.SFXVolumeKey, 0.8f);
        SetAndSaveVolume(SettingType.VoiceVolumeKey, 1f);

        // DISPLAY
        // ApplySetting(SettingType.GraphicsQuality, 2);
        // SaveSetting(SettingType.GraphicsQuality, 2);

        // ApplySetting(SettingType.Resolution, 0);
        // SaveSetting(SettingType.Resolution, 0);

        ApplySetting(SettingType.Screen, 1); // Full (no lines)
        SaveSetting(SettingType.Screen, 1);

        ApplySetting(SettingType.Border, 0);
        SaveSetting(SettingType.Border, 0);

        ApplySetting(SettingType.Filter, 0);
        SaveSetting(SettingType.Filter, 0);

        PlayerPrefs.SetInt(SettingsKeys.VSyncKey, 1);
        QualitySettings.vSyncCount = 1;

        ApplySetting(SettingType.Language, 0);
        SaveSetting(SettingType.Language, 0);

        // GAMEPLAY OPTIONS
        // PlayerPrefs.SetInt(SettingsKeys.SlideWithDownJumpKey, 1);
        // PlayerPrefs.SetInt(SettingsKeys.ControllerVibrationKey, 1);
        // ApplySetting(SettingType.DisplayMode, 0);
        // SaveSetting(SettingType.DisplayMode, 0);

        PlayerPrefs.Save();

        // REFRESH UI
        foreach (var selector in FindObjectsByType<OptionSelectorSettingHandler>(FindObjectsSortMode.None))
            selector.RefreshUI();

        foreach (var toggle in FindObjectsByType<ToggleSettingHandler>(FindObjectsSortMode.None))
            toggle.RefreshUI();

        foreach (var volume in FindObjectsByType<VolumeSettingHandler>(FindObjectsSortMode.None))
            volume.ApplyFromSaved();

        Debug.Log("All settings reset to default.");
    }

    // Getters
    public string[] GetOptions(SettingType type)
    {
        switch (type)
        {
            case SettingType.Resolution:
                return GetResolutionOptions();

            case SettingType.GraphicsQuality:
                return GetGraphicsQualityOptions();

            case SettingType.Language:
                return GetLanguageOptions();

            case SettingType.DisplayMode:
                return GetLocalizedDisplayModeOptions();
                
            case SettingType.Screen:
                return GetLocalizedStrings("Screen_Original", "Screen_Full", "Screen_Windowed", "Screen_WindowedFull", "Screen_Wide");

            case SettingType.Filter:
                return GetLocalizedStrings("Filter_None", "Filter_TV", "Filter_Monitor");

            case SettingType.Border:
                return GetLocalizedStrings("Border_None", "Border_Pillar", "Border_Windowbox");

            default:
                return new string[0];
        }
    }

    private string[] GetLocalizedStrings(params string[] tableKeys)
    {
        return tableKeys.Select(key => LocalizationSettings.StringDatabase.GetLocalizedString("GameText", key)).ToArray();
    }

    public int GetSavedIndex(SettingType type)
    {
        return type switch
        {
            SettingType.GraphicsQuality => PlayerPrefs.GetInt(SettingsKeys.GraphicsQualityKey, 2),
            SettingType.Resolution => PlayerPrefs.GetInt(SettingsKeys.ResolutionKey, 0),
            SettingType.Language => PlayerPrefs.GetInt(SettingsKeys.LanguageKey, 0),
            SettingType.DisplayMode => PlayerPrefs.GetInt(SettingsKeys.DisplayModeKey, 0),
            SettingType.Screen => PlayerPrefs.GetInt(SettingsKeys.ScreenKey, 0),
            SettingType.Filter => PlayerPrefs.GetInt(SettingsKeys.FilterKey, 0),
            SettingType.Border => PlayerPrefs.GetInt(SettingsKeys.BorderKey, 0),
            _ => 0
        };
    }

    public string[] GetGraphicsQualityOptions()
    {
        string[] qualityNames = QualitySettings.names;
        string[] localizedNames = new string[qualityNames.Length];
        for (int i = 0; i < qualityNames.Length; i++)
            localizedNames[i] = LocalizationSettings.StringDatabase.GetLocalizedString("GameText", qualityNames[i]);
        return localizedNames;
    }

    public string[] GetResolutionOptions()
    {
        return resolutions.Select(r => $"{r.width}x{r.height}").ToArray();
    }

    private string[] GetLocalizedDisplayModeOptions()
    {
        // It assumes 5 modes: Auto, Xbox, PlayStation, Nintendo, Custom
        string[] keys = { "Auto", "Xbox", "PlayStation", "Nintendo", "Custom" };
        return keys.Select(key => LocalizationSettings.StringDatabase.GetLocalizedString("GameText", DisplayModeKeyPrefix + key)).ToArray();
    }

    public string[] GetLanguageOptions()
    {
        return LocalizationSettings.AvailableLocales.Locales
            .Select(locale => locale.Identifier.CultureInfo?.NativeName.Split('(')[0].Trim()).ToArray();
    }

    // Setters
    private IEnumerator SetLanguageAsync(int index)
    {
        yield return LocalizationSettings.InitializationOperation;
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
    }

    private void ApplyDisplayMode(int index)
    {
        var icons = FindFirstObjectByType<GamepadIconsExample>();
        if (icons == null) return;

        var mode = (GamepadIconsExample.ControlScheme)index;
        if (mode == GamepadIconsExample.ControlScheme.Auto)
            icons.SetAutoScheme();
        else if (mode == GamepadIconsExample.ControlScheme.Custom)
            icons.SetCustomSprites();
        else
            icons.SetControlScheme(mode);

        icons.RefreshAllIcons();
    }

    public void ApplySetting(SettingType type, int index)
    {
        switch (type)
        {
            case SettingType.GraphicsQuality:
                QualitySettings.SetQualityLevel(index);
                break;

            case SettingType.Resolution:
                if (index >= 0 && index < resolutions.Length)
                {
                    var res = resolutions[index];
                    Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
                }
                break;

            case SettingType.Language:
                PlayerPrefs.SetInt(SettingsKeys.LanguageKey, index);
                StartCoroutine(SetLanguageAsync(index));
                break;

            case SettingType.DisplayMode:
                ApplyDisplayMode(index);
                break;

            case SettingType.Screen:
                PlayerPrefs.SetInt(SettingsKeys.ScreenKey, index);
                ScreenDisplayManager.Instance?.Apply((ScreenDisplayMode)index);
                break;

            case SettingType.Border:
                PlayerPrefs.SetInt(SettingsKeys.BorderKey, index);
                BorderManager.Instance?.ApplyBorderClean((BorderMode)index);
                break;

            case SettingType.Filter:
                PlayerPrefs.SetInt(SettingsKeys.FilterKey, index);
                FilterManager.Instance?.SetFilter((FilterMode)index);
                break;
        }
    }

    public void SaveSetting(SettingType type, int index)
    {
        switch (type)
        {
            case SettingType.GraphicsQuality:
                PlayerPrefs.SetInt(SettingsKeys.GraphicsQualityKey, index);
                break;

            case SettingType.Resolution:
                PlayerPrefs.SetInt(SettingsKeys.ResolutionKey, index);
                break;

            case SettingType.Language:
                PlayerPrefs.SetInt(SettingsKeys.LanguageKey, index);
                break;

            case SettingType.DisplayMode:
                PlayerPrefs.SetInt(SettingsKeys.DisplayModeKey, index);
                break;

            case SettingType.Screen:
                PlayerPrefs.SetInt(SettingsKeys.ScreenKey, index);
                break;

            case SettingType.Border:
                PlayerPrefs.SetInt(SettingsKeys.BorderKey, index);
                break;

            case SettingType.Filter:
                PlayerPrefs.SetInt(SettingsKeys.FilterKey, index);
                break;
        }

        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (graphicsTextHandle.IsValid()) Addressables.Release(graphicsTextHandle);
    }
}