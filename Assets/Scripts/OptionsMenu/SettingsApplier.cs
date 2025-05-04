using UnityEngine;

public static class SettingsApplier
{
    public static void ApplyDisplaySetting(SettingType type, int index)
    {
        switch (type)
        {
            case SettingType.Screen:
                ScreenDisplayManager.Instance?.Apply((ScreenDisplayMode)index);
                break;
            case SettingType.DisplayMode:
                GamepadIconManager.Instance?.SetControlScheme(index);
                break;
            case SettingType.Filter:
                FilterManager.Instance?.SetFilter((FilterMode)index);
                break;
        }
    }

    public static void ApplyBoolSetting(SettingType type, bool value)
    {
        switch (type)
        {
            case SettingType.Border:
                BorderManager.Instance?.SetEnabled(value);
                break;
            case SettingType.VSync:
                QualitySettings.vSyncCount = value ? 1 : 0;
                break;
        }
    }
}