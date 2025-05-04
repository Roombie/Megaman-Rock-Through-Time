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

    // Getters
    public string[] GetOptions(SettingType type)
    {
        switch (type)
        {
            case SettingType.Resolution:
                return GetResolutionOptions();

            case SettingType.GraphicsQuality:
                return GetResolutionOptions();

            case SettingType.Language:
                return GetLanguageOptions();

            case SettingType.DisplayMode:
                return GetLocalizedDisplayModeOptions();
                
            case SettingType.Screen:
                return GetLocalizedStrings("Screen_Original", "Screen_Full", "Screen_Windowed");

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
        // Asume que tienes 5 modos: Auto, Xbox, PlayStation, Nintendo, Custom
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
        }
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (graphicsTextHandle.IsValid()) Addressables.Release(graphicsTextHandle);
    }
}