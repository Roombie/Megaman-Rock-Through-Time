using UnityEngine;

/// <summary>
/// Defines the public contract of the audio system.
/// Gameplay code should depend on this interface rather than AudioManager
/// directly, so the underlying implementation can be swapped (e.g. for
/// FMOD or Wwise middleware) without touching any gameplay scripts.
/// </summary>
public interface IAudioService
{
    // ── Playback ──────────────────────────────────────────────────────────
    AudioHandle Play(AudioEvent audioEvent);
    AudioHandle PlayAtPosition(AudioEvent audioEvent, Vector3 position);
    AudioHandle Play(AudioClip clip, SoundCategory category, float volume, float pitch, bool loop);
    AudioHandle PlayOnce(AudioEvent audioEvent);
    AudioHandle PlayOrReplace(AudioEvent audioEvent);
    AudioHandle PlayMusic(AudioClip clip, float volume, bool loop, float fadeSeconds, float startTime);

    // ── Volume ────────────────────────────────────────────────────────────
    void  SetVolume(AudioBus bus, float linearVolume);
    float GetVolume(AudioBus bus, float fallback);

    // ── Mute ──────────────────────────────────────────────────────────────
    void MuteAll(bool mute);
    void MuteBus(AudioBus bus, bool mute);
    void MuteSFX(bool mute);
    void MuteBGM(bool mute);
    void MuteUI(bool mute);
    void MuteVoice(bool mute);

    // ── Stop / Pause / Resume ─────────────────────────────────────────────
    void StopMusic();
    void FadeOutMusic(float seconds);
    void StopCategory(SoundCategory category);
    void StopAll();
    void PauseAll();
    void ResumeAll();
    void PauseCategory(SoundCategory category);
    void ResumeCategory(SoundCategory category);

    // ── Query ─────────────────────────────────────────────────────────────
    bool IsPlaying(AudioClip clip);
    int  ActiveVoiceCount { get; }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Static facade that gives any script clean global access to the audio
/// system without coupling to the AudioManager MonoBehaviour directly.
///
/// ── Usage ────────────────────────────────────────────────────────────────
///
///     // Instead of:
///     Audio.Play(jumpEvent);
///
///     // Use:
///     Audio.Play(jumpEvent);
///
/// ── Why this matters ─────────────────────────────────────────────────────
/// If you later integrate FMOD or Wwise, you replace the AudioManager
/// implementation (which implements IAudioService) without touching the
/// hundreds of gameplay scripts that call Audio.Play().
///
/// ── Initialization ───────────────────────────────────────────────────────
/// AudioManager calls Audio.Register(this) in its Awake().
/// </summary>
public static class Audio
{
    static IAudioService service;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetService() => service = null;

    /// <summary>Called by AudioManager.Awake() to register itself.</summary>
    public static void Register(IAudioService impl) => service = impl;

    /// <summary>Clears the service only when the same implementation still owns it.</summary>
    public static void Unregister(IAudioService impl)
    {
        if (object.ReferenceEquals(service, impl)) service = null;
    }

    public static bool IsReady => service != null;

    // ── Playback ──────────────────────────────────────────────────────────

    public static AudioHandle Play(AudioEvent audioEvent)
        => service?.Play(audioEvent) ?? AudioHandle.Invalid;

    public static AudioHandle PlayAtPosition(AudioEvent audioEvent, Vector3 position)
        => service?.PlayAtPosition(audioEvent, position) ?? AudioHandle.Invalid;

    public static AudioHandle Play(AudioClip clip,
        SoundCategory category = SoundCategory.SFX,
        float volume = 1f, float pitch = 1f, bool loop = false)
        => service?.Play(clip, category, volume, pitch, loop) ?? AudioHandle.Invalid;

    public static AudioHandle PlayOnce(AudioEvent audioEvent)
        => service?.PlayOnce(audioEvent) ?? AudioHandle.Invalid;

    public static AudioHandle PlayOrReplace(AudioEvent audioEvent)
        => service?.PlayOrReplace(audioEvent) ?? AudioHandle.Invalid;

    /// <summary>
    /// Music with an explicit crossfade (seconds; &lt; 0 = AudioManager default) and an
    /// optional start position for resuming a track.
    /// </summary>
    public static AudioHandle PlayMusic(AudioClip clip, float volume = 1f, bool loop = true,
        float fadeSeconds = -1f, float startTime = 0f)
        => service?.PlayMusic(clip, volume, loop, fadeSeconds, startTime) ?? AudioHandle.Invalid;

    // ── Volume ────────────────────────────────────────────────────────────

    public static void SetVolume(AudioBus bus, float value) => service?.SetVolume(bus, value);
    public static float GetVolume(AudioBus bus, float fallback = 1f)
        => service?.GetVolume(bus, fallback) ?? fallback;

    // ── Mute ──────────────────────────────────────────────────────────────

    public static void MuteAll(bool mute) => service?.MuteAll(mute);
    public static void MuteBus(AudioBus bus, bool mute) => service?.MuteBus(bus, mute);
    public static void MuteSFX(bool mute) => service?.MuteSFX(mute);
    public static void MuteBGM(bool mute) => service?.MuteBGM(mute);
    public static void MuteUI(bool mute) => service?.MuteUI(mute);
    public static void MuteVoice(bool mute) => service?.MuteVoice(mute);

    // ── Stop / Pause / Resume ─────────────────────────────────────────────

    public static void StopMusic()                        => service?.StopMusic();
    public static void FadeOutMusic(float seconds)        => service?.FadeOutMusic(seconds);
    public static void StopCategory(SoundCategory cat)   => service?.StopCategory(cat);
    public static void StopAll()                          => service?.StopAll();
    public static void PauseAll()                         => service?.PauseAll();
    public static void ResumeAll()                        => service?.ResumeAll();
    public static void PauseCategory(SoundCategory cat)  => service?.PauseCategory(cat);
    public static void ResumeCategory(SoundCategory cat) => service?.ResumeCategory(cat);

    // ── Query ─────────────────────────────────────────────────────────────

    public static bool IsPlaying(AudioClip clip) => service?.IsPlaying(clip) ?? false;
    public static int  ActiveVoiceCount          => service?.ActiveVoiceCount ?? 0;
}