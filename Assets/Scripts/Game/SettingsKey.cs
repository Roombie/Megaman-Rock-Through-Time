using System.Collections.Generic;

public enum SettingType
{
    MasterVolumeKey,
    MusicVolumeKey,
    SFXVolumeKey,
    VoiceVolumeKey,
    GraphicsQuality,
    Resolution,
    DisplayMode,
    Screen,
    Border,
    Filter,
    VSync,
    SlideWithDownJumpKey,
    ControllerVibrationKey,
    Language
}

public static class SettingsKeys
{
    private static readonly Dictionary<SettingType, string> keys = new()
    {
        { SettingType.GraphicsQuality, GraphicsQualityKey },
        { SettingType.Resolution, ResolutionKey },
        { SettingType.DisplayMode, DisplayModeKey },
        { SettingType.Screen, ScreenKey },
        { SettingType.Border, BorderKey },
        { SettingType.Filter, FilterKey },
        { SettingType.VSync, VSyncKey },
        { SettingType.SlideWithDownJumpKey, SlideWithDownJumpKey},
        { SettingType.ControllerVibrationKey, ControllerVibrationKey},
        { SettingType.Language, LanguageKey }
    };

    public static string Get(SettingType type) => keys.TryGetValue(type, out var value) ? value : type.ToString();

    // Audio
    public const string MasterVolumeKey = "MasterVolume";
    public const string MusicVolumeKey = "MusicVolume";
    public const string SFXVolumeKey = "SFXVolume";
    public const string VoiceVolumeKey = "VoiceVolume";

    // General
    public const string FullscreenKey = "Fullscreen";
    public const string ResolutionKey = "Resolution";
    public const string VSyncKey = "VSync";
    public const string GraphicsQualityKey = "GraphicsQuality";
    public const string DisplayModeKey = "DisplayMode";        // Control scheme (Xbox/PS/Nintendo)
    public const string ScreenKey = "ScreenDisplayMode";       // Original / Full / Wide
    public const string BorderKey = "Border";           // OFF / Pillar / Window
    public const string FilterKey = "Filter";                  // OFF / TV (CRT Scanlines) / Monitor (LCD)
    public const string LanguageKey = "Language";

    // Gameplay
    public const string SlideWithDownJumpKey = "SlideWithDown";
    public const string ControllerVibrationKey = "ControllerVibration";
}