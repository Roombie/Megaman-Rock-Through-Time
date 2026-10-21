using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Only adapter between the reusable Roombie audio package (Assets/Packages/com.roombie.audio) and this
/// project's SettingType / PlayerPrefs settings. The package doesn't know how settings are stored.
/// </summary>
public static class AudioSettingsBridge
{
    public static bool TryGetBus(SettingType type, out AudioBus bus)
    {
        switch (type)
        {
            case SettingType.MasterVolumeKey: bus = AudioBus.Master; return true;
            case SettingType.MusicVolumeKey:  bus = AudioBus.BGM;    return true;
            case SettingType.SFXVolumeKey:    bus = AudioBus.SFX;    return true;
            case SettingType.VoiceVolumeKey:  bus = AudioBus.Voice;  return true;
            default: bus = default; return false;
        }
    }

    /// <summary>Applies a linear 0..1 volume to the mixer (saving it is up to the caller).</summary>
    public static void Apply(SettingType type, float linearVolume)
    {
        if (!TryGetBus(type, out AudioBus bus))
        {
            Debug.LogWarning($"[AudioSettingsBridge] SettingType.{type} is not an audio setting.");
            return;
        }
        Audio.SetVolume(bus, linearVolume);
    }

    public static void ApplySavedVolumes()
    {
        foreach (var type in new[] { SettingType.MasterVolumeKey, SettingType.MusicVolumeKey, SettingType.SFXVolumeKey, SettingType.VoiceVolumeKey })
            Apply(type, PlayerPrefs.GetFloat(SettingsKeys.Get(type), 1f));
    }

    // The AudioManager prefab lives in the scenes; once it has registered (its Awake), restore the saved volumes.
    // Every scene load re-checks, so it also works when the first scene has no AudioManager.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        TryApplyOnce();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static bool applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => applied = false;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryApplyOnce();

    private static void TryApplyOnce()
    {
        if (applied || !Audio.IsReady) return;
        applied = true;
        ApplySavedVolumes();
    }
}
