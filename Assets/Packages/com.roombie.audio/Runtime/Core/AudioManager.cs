using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Reusable central audio service.
///
/// ── Features ──────────────────────────────────────────────────────────────
///  • AudioEvent ScriptableObject-driven playback (primary gameplay path)
///  • Raw AudioClip playback (no AudioEvent needed)
///  • AudioSource object pooling with safe cleanup via isPlaying polling
///  • Voice limiter — oldest low-priority sound is evicted when pool is full
///  • Music crossfade (two sources alternating, time-scale independent),
///    per-call fade length, resume offset and fade-out to silence
///  • Positional / 3D audio (PlayAtPosition) with distance culling
///  • AudioMixer volume control (SetVolume — linear → dB conversion)
///  • Pause / Resume all or per-category
///  • StopCategory, StopAll, StopMusic
///  • IsPlaying(AudioClip) — list scan, no GetComponents allocations
///  • AudioHandle returns for per-instance runtime control
///  • Data-driven channels via AudioChannelConfig ScriptableObject
///
/// ── Unity Setup ───────────────────────────────────────────────────────────
///  1. Create an AudioChannelConfig asset (Assets → Create → Audio → Channel Config).
///  2. Assign each channel's AudioMixerGroup in the config asset.
///  3. Expose matching volume parameters in the AudioMixer and name them
///     to match the volumeParam strings in the config.
///  4. Assign the config asset to AudioManager.channelConfig in the Inspector.
///  5. Open Window → Audio → Output Manager to verify routing visually.
///
/// ── Project Settings (do once) ────────────────────────────────────────────
///  Audio Import: BGM clips → Streaming; SFX clips → Vorbis
///  Project Settings → Audio → DSP Buffer Size: Best Latency
///  Project Settings → Audio → Max Real Voices: 32–64
///  Project Settings → Audio → Max Virtual Voices: 512
/// </summary>
[DefaultExecutionOrder(-1000)]
public class AudioManager : MonoBehaviour, IAudioService
{
    // Private lifecycle guard only. Consumers use the interface-backed Audio facade rather than
    // locating this concrete MonoBehaviour globally.
    static AudioManager activeManager;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Audio Database")]
    [Tooltip("Optional. Assign an AudioDatabase asset to enable Play(\"key\") lookups.")]
    public AudioDatabase audioDatabase;

    [Header("Channel Config")]
    [Tooltip("Defines all mixer groups, volume params, and categories. " +
             "Create via Assets → Create → Audio → Channel Config.")]
    public AudioChannelConfig channelConfig;

    [Header("Pooling & Limits")]
    [Tooltip("AudioSources pre-created at startup. Increase for busy scenes.")]
    public int poolSize  = 16;
    [Tooltip("Hard cap on simultaneous voices. Lowest-priority voice is evicted when full.")]
    public int maxVoices = 32;

    [Header("Per-Category Voice Budgets")]
    [Tooltip("Max simultaneous UI sounds. 0 = unlimited (up to maxVoices).")]
    public int maxUIVoices    = 8;
    [Tooltip("Max simultaneous Voice/dialogue sounds. 0 = unlimited.")]
    public int maxVoiceVoices = 4;
    [Tooltip("Max simultaneous SFX sounds. 0 = unlimited (falls back to maxVoices).")]
    public int maxSFXVoices   = 24;

    [Header("Music Crossfade")]
    [Tooltip("Seconds to blend between two BGM tracks.")]
    public float crossfadeDuration = 1f;

    [Header("Volume Debug")]
    [SerializeField] private bool showVolumeDebugLogs = true;

    // -------------------------------------------------------------------------
    // Private – pool & tracking
    // -------------------------------------------------------------------------

    readonly Queue<AudioSource> pool         = new();
    readonly List<ActiveSound>  activeSounds = new();

    // Runtime values only. Persistence belongs to the host project.
    readonly Dictionary<AudioBus, float> busVolumes = new()
    {
        { AudioBus.Master, 1f },
        { AudioBus.SFX,    1f },
        { AudioBus.BGM,    1f },
        { AudioBus.UI,     1f },
        { AudioBus.Voice,  1f },
    };

    AudioSource musicSourceA;
    AudioSource musicSourceB;
    AudioSource activeMusicSource;
    AudioSource inactiveMusicSource;
    Coroutine   crossfadeCoroutine;

    /// <summary>The currently active music AudioSource. Used by AudioDebugger.</summary>
    public AudioSource MusicSource => activeMusicSource;

    AudioListener cachedListener;

    /// <summary>
    /// The scene's AudioListener, cached lazily. Exposed so optional playback adapters
    /// can perform the same distance-cull check without duplicating discovery logic.
    /// </summary>
    public AudioListener CachedListener
    {
        get
        {
            if (cachedListener == null) cachedListener = FindFirstObjectByType<AudioListener>();
            return cachedListener;
        }
    }

    // -------------------------------------------------------------------------
    // Internal helper type
    // -------------------------------------------------------------------------

    struct ActiveSound
    {
        public AudioSource source;
        public int         priority;
    }

    // =========================================================================
    // Lifecycle
    // =========================================================================

    void Awake()
    {
        if (activeManager != null && activeManager != this)
        {
            Destroy(gameObject);
            return;
        }

        activeManager = this;
        Audio.Register(this);
        DontDestroyOnLoad(gameObject);

        if (audioDatabase != null) audioDatabase.Initialize();

        BuildPool();
        BuildMusicSources();
    }

    void OnDestroy()
    {
        if (activeManager != this) return;
        Audio.Unregister(this);
        activeManager = null;
    }

    void Update()
    {
        for (int i = activeSounds.Count - 1; i >= 0; i--)
        {
            AudioSource src = activeSounds[i].source;
            // Finished or stopped (Unity rewinds both to 0) - including loops stopped through an AudioHandle.
            // Paused sources keep their position, so they aren't recycled while paused.
            if (src != null && !src.isPlaying && src.timeSamples == 0)
            {
                ReturnSource(src);
                activeSounds.RemoveAt(i);
            }
        }
    }

    // =========================================================================
    // Pool initialisation
    // =========================================================================

    void BuildPool()
    {
        for (int i = 0; i < poolSize; i++)
            pool.Enqueue(MakeSource());
    }

    void BuildMusicSources()
    {
        musicSourceA = gameObject.AddComponent<AudioSource>();
        musicSourceB = gameObject.AddComponent<AudioSource>();

        var bgmGroup = GetMixerGroup(SoundCategory.BGM);
        foreach (var src in new[] { musicSourceA, musicSourceB })
        {
            src.outputAudioMixerGroup = bgmGroup;
            src.loop                  = true;
            src.playOnAwake           = false;
            src.volume                = 0f;
        }

        activeMusicSource   = musicSourceA;
        inactiveMusicSource = musicSourceB;
    }

    AudioSource MakeSource()
    {
        var src         = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        return src;
    }

    AudioSource GetSource(SoundCategory category = SoundCategory.SFX)
    {
        int categoryMax = GetCategoryBudget(category);
        if (categoryMax > 0)
        {
            int categoryCount = 0;
            AudioMixerGroup targetGroup = GetMixerGroup(category);
            foreach (var s in activeSounds)
                if (s.source.outputAudioMixerGroup == targetGroup) categoryCount++;

            if (categoryCount >= categoryMax)
            {
                Debug.LogWarning($"[AudioManager] Voice budget reached for {category} ({categoryMax}).");
                return null;
            }
        }

        if (pool.Count > 0)
            return pool.Dequeue();

        if (activeSounds.Count >= maxVoices)
        {
            int worstIndex     = 0;
            int lowestPriority = activeSounds[0].priority;

            for (int i = 1; i < activeSounds.Count; i++)
            {
                if (activeSounds[i].priority < lowestPriority)
                {
                    lowestPriority = activeSounds[i].priority;
                    worstIndex     = i;
                }
            }

            AudioSource evicted = activeSounds[worstIndex].source;
            activeSounds.RemoveAt(worstIndex);
            ResetSource(evicted);
            return evicted;
        }

        return MakeSource();
    }

    void ReturnSource(AudioSource src)
    {
        if (src == null || pool.Contains(src)) return;
        src.Stop();
        ResetSource(src);
        pool.Enqueue(src);
    }

    void ResetSource(AudioSource src)
    {
        src.Stop();
        src.clip                  = null;
        src.loop                  = false;
        src.pitch                 = 1f;
        src.volume                = 1f;
        src.spatialBlend          = 0f;
        src.minDistance           = 1f;
        src.maxDistance           = 500f;
        src.outputAudioMixerGroup = null;
    }

    // =========================================================================
    // PRIMARY PLAY — AudioEvent  (gameplay path)
    // =========================================================================

    /// <summary>
    /// Plays an AudioEvent ScriptableObject.
    /// Enforces cooldown, no-repeat clip selection, pitch/volume randomisation,
    /// and routes BGM through the crossfade music system automatically.
    /// Returns an AudioHandle for per-instance control.
    /// </summary>
    public AudioHandle Play(AudioEvent audioEvent)
    {
        if (audioEvent == null) return AudioHandle.Invalid;
        if (!audioEvent.CanPlay()) return AudioHandle.Invalid;

        AudioClip clip = audioEvent.GetClip();
        if (clip == null) return AudioHandle.Invalid;

        audioEvent.MarkPlayed();

        if (audioEvent.category == SoundCategory.BGM)
            return new AudioHandle(
                BeginCrossfade(clip, audioEvent.GetVolume(), audioEvent.GetPitch(), audioEvent.loop));

        return PlaySFXCore(
            clip,
            audioEvent.GetVolume(),
            audioEvent.GetPitch(),
            audioEvent.loop,
            audioEvent.spatialBlend,
            audioEvent.minDistance,
            audioEvent.maxDistance,
            (int)audioEvent.priority,
            Vector3.zero,
            usePosition: false
        );
    }

    /// <summary>
    /// Plays an AudioEvent at a world-space position (3D spatial audio).
    /// Sounds whose maxDistance does not reach the AudioListener are culled.
    /// </summary>
    public AudioHandle PlayAtPosition(AudioEvent audioEvent, Vector3 position)
    {
        if (audioEvent == null) return AudioHandle.Invalid;
        if (!audioEvent.CanPlay()) return AudioHandle.Invalid;

        if (cachedListener == null) cachedListener = FindFirstObjectByType<AudioListener>();
        if (cachedListener != null)
        {
            float dist = Vector3.Distance(cachedListener.transform.position, position);
            if (dist > audioEvent.maxDistance) return AudioHandle.Invalid;
        }

        AudioClip clip = audioEvent.GetClip();
        if (clip == null) return AudioHandle.Invalid;

        audioEvent.MarkPlayed();

        if (audioEvent.category == SoundCategory.BGM)
        {
            Debug.LogWarning($"[AudioManager] PlayAtPosition called with BGM event '{audioEvent.name}'. "
                + "BGM does not support positional audio — playing as flat stereo instead.");
            return new AudioHandle(
                BeginCrossfade(clip, audioEvent.GetVolume(), audioEvent.GetPitch(), audioEvent.loop));
        }

        return PlaySFXCore(
            clip,
            audioEvent.GetVolume(),
            audioEvent.GetPitch(),
            audioEvent.loop,
            Mathf.Max(audioEvent.spatialBlend, 1f),
            audioEvent.minDistance,
            audioEvent.maxDistance,
            (int)audioEvent.priority,
            position,
            usePosition: true
        );
    }

    // =========================================================================
    // SECONDARY PLAY — raw AudioClip  (UI / Settings path)
    // =========================================================================

    /// <summary>
    /// Plays a raw AudioClip without an AudioEvent asset.
    /// Intended for UI elements that hold plain AudioClip references.
    /// </summary>
    public AudioHandle Play(
        AudioClip     clip,
        SoundCategory category = SoundCategory.SFX,
        float         volume   = 1f,
        float         pitch    = 1f,
        bool          loop     = false)
    {
        if (clip == null) return AudioHandle.Invalid;

        if (category == SoundCategory.BGM)
            return new AudioHandle(BeginCrossfade(clip, volume, pitch, loop));

        return PlaySFXCore(clip, volume, pitch, loop,
                           spatialBlend: 0f, minDist: 1f, maxDist: 500f,
                           priority: 50,
                           position: Vector3.zero, usePosition: false,
                           category: category);
    }

    /// <summary>
    /// Plays a music clip with an explicit crossfade length and start position.
    /// fadeSeconds &lt; 0 uses crossfadeDuration. startTime resumes a track mid-way.
    /// </summary>
    public AudioHandle PlayMusic(AudioClip clip, float volume = 1f, bool loop = true,
        float fadeSeconds = -1f, float startTime = 0f)
    {
        if (clip == null) return AudioHandle.Invalid;
        return new AudioHandle(BeginCrossfade(clip, volume, 1f, loop, fadeSeconds, startTime));
    }

    /// <summary>
    /// Fades all music to silence over the given seconds, then stops it.
    /// A later PlayMusic/Play(BGM) interrupts the fade cleanly.
    /// </summary>
    public void FadeOutMusic(float seconds)
    {
        if (seconds <= 0f) { StopMusic(); return; }

        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(FadeOutRoutine(seconds));
    }

    // =========================================================================
    // SFX core — all play paths converge here
    // =========================================================================

    /// <summary>
    /// Returns the correct AudioMixerGroup for a given SoundCategory,
    /// sourced from the AudioChannelConfig asset.
    /// Falls back to the SFX group (or null) when no config is assigned.
    /// </summary>
    public AudioMixerGroup GetMixerGroup(SoundCategory category)
    {
        if (channelConfig != null)
        {
            var group = channelConfig.GetGroup(category);
            // If category has no dedicated channel, fall back to SFX
            if (group != null || category == SoundCategory.SFX)
                return group;
            return channelConfig.GetGroup(SoundCategory.SFX);
        }

        // No config assigned — return null so callers can warn
        Debug.LogWarning("[AudioManager] No AudioChannelConfig assigned. Mixer routing will not work.");
        return null;
    }

    int GetCategoryBudget(SoundCategory category) => category switch
    {
        SoundCategory.UI    => maxUIVoices,
        SoundCategory.Voice => maxVoiceVoices,
        SoundCategory.SFX   => maxSFXVoices,
        _                   => 0,
    };

    AudioHandle PlaySFXCore(
        AudioClip     clip,
        float         volume,
        float         pitch,
        bool          loop,
        float         spatialBlend,
        float         minDist,
        float         maxDist,
        int           priority,
        Vector3       position,
        bool          usePosition,
        SoundCategory category = SoundCategory.SFX)
    {
        AudioSource src = GetSource(category);
        if (src == null) return AudioHandle.Invalid;

        src.outputAudioMixerGroup = GetMixerGroup(category);
        src.clip                  = clip;
        src.volume                = volume;
        src.pitch                 = pitch;
        src.loop                  = loop;
        src.spatialBlend          = spatialBlend;
        src.minDistance           = minDist;
        src.maxDistance           = maxDist;

        if (usePosition)
            src.transform.position = position;

        src.Play();

        activeSounds.Add(new ActiveSound { source = src, priority = priority });
        return new AudioHandle(src);
    }

    // =========================================================================
    // Music crossfade
    // =========================================================================

    /// <summary>
    /// Starts the new clip on the inactive source and fades it in.
    /// Returns the source playing the new clip; activeMusicSource only swaps
    /// once the fade completes, so callers must not use it for the handle.
    /// </summary>
    AudioSource BeginCrossfade(AudioClip clip, float targetVolume, float pitch, bool loop,
        float fadeSeconds = -1f, float startTime = 0f)
    {
        if (crossfadeCoroutine != null)
        {
            // Interrupted mid-fade: both sources are audible. Keep the louder one
            // fading out from where it is, and recycle the quieter one for the new
            // clip, so nothing cuts off abruptly or lingers at partial volume.
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;

            if (inactiveMusicSource.volume > activeMusicSource.volume)
                (activeMusicSource, inactiveMusicSource) = (inactiveMusicSource, activeMusicSource);

            inactiveMusicSource.Stop();
        }

        AudioSource fadeIn = inactiveMusicSource;

        fadeIn.clip   = clip;
        fadeIn.pitch  = pitch;
        fadeIn.volume = 0f;
        fadeIn.loop   = loop;
        fadeIn.Play();

        // Seek AFTER Play: Play() rewinds the source. timeSamples is sample-accurate, which
        // matters for compressed/streamed BGM where `time` can land slightly off.
        if (startTime > 0f && clip.frequency > 0)
        {
            int maxSample = Mathf.Max(0, clip.samples - clip.frequency / 20); // keep ~50 ms margin
            fadeIn.timeSamples = Mathf.Clamp(Mathf.RoundToInt(startTime * clip.frequency), 0, maxSample);
        }

        float duration = fadeSeconds >= 0f ? fadeSeconds : crossfadeDuration;
        crossfadeCoroutine = StartCoroutine(
            CrossfadeRoutine(activeMusicSource, fadeIn, targetVolume, duration));

        return fadeIn;
    }

    IEnumerator CrossfadeRoutine(AudioSource fadeOut, AudioSource fadeIn, float targetVolume,
        float duration)
    {
        float startVolume = fadeOut.volume;
        float elapsed     = 0f;

        while (elapsed < duration)
        {
            elapsed        += Time.unscaledDeltaTime;
            float t         = Mathf.Clamp01(elapsed / duration);
            fadeOut.volume  = Mathf.Lerp(startVolume, 0f,           t);
            fadeIn.volume   = Mathf.Lerp(0f,          targetVolume, t);
            yield return null;
        }

        fadeOut.volume = 0f;
        fadeOut.Stop();
        fadeIn.volume  = targetVolume;

        activeMusicSource   = fadeIn;
        inactiveMusicSource = fadeOut;
        crossfadeCoroutine  = null;
    }

    IEnumerator FadeOutRoutine(float duration)
    {
        float startA  = activeMusicSource.volume;
        float startB  = inactiveMusicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k  = 1f - Mathf.Clamp01(elapsed / duration);
            activeMusicSource.volume   = startA * k;
            inactiveMusicSource.volume = startB * k;
            yield return null;
        }

        activeMusicSource.Stop();
        inactiveMusicSource.Stop();
        crossfadeCoroutine = null;
    }

    // =========================================================================
    // Volume control
    // =========================================================================

    /// <summary>
    /// Sets a logical audio bus to a linear 0..1 value.
    /// The audio package deliberately does not persist this value; the host project
    /// decides how settings are stored and reapplies them when appropriate.
    /// </summary>
    public void SetVolume(AudioBus bus, float linearVolume)
    {
        linearVolume = Mathf.Clamp01(linearVolume);
        busVolumes[bus] = linearVolume;

        string param = channelConfig?.GetVolumeParam(bus);
        var mixer = channelConfig?.GetMixer();

        if (mixer != null && !string.IsNullOrEmpty(param))
        {
            float decibels = LinearToDb(linearVolume);
            bool applied = mixer.SetFloat(param, decibels);

            if (!applied)
            {
                Debug.LogError(
                    $"[AudioManager] Mixer rejected parameter '{param}' for bus {bus}. " +
                    "Confirm that it is exposed and that AudioChannelConfig uses the exact exposed name.",
                    this);
                return;
            }

            if (showVolumeDebugLogs)
            {
                Debug.Log(
                    $"[AudioManager] SetVolume bus={bus}, parameter='{param}', " +
                    $"linear={linearVolume:F3}, dB={decibels:F2}",
                    this);
            }

            return;
        }

        // A project can still use master volume without an AudioMixer.
        if (bus == AudioBus.Master)
        {
            AudioListener.volume = linearVolume;

            if (showVolumeDebugLogs)
                Debug.Log($"[AudioManager] SetVolume bus=Master via AudioListener.volume={linearVolume:F3}", this);

            return;
        }

        Debug.LogWarning(
            $"[AudioManager] No mixer parameter is configured for bus {bus}. " +
            "Assign an AudioChannelConfig with an AudioMixer and exposed volume parameter.",
            this);
    }

    /// <summary>Returns the current runtime value for a logical audio bus.</summary>
    public float GetVolume(AudioBus bus, float fallback = 1f)
        => busVolumes.TryGetValue(bus, out float value) ? value : fallback;

    // =========================================================================
    // Stop
    // =========================================================================

    /// <summary>Stops BGM immediately (no fade). Cancels any running crossfade.</summary>
    public void StopMusic()
    {
        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = null;
        activeMusicSource.Stop();
        inactiveMusicSource.Stop();
    }

    /// <summary>Stops and recycles all voices in the given category.</summary>
    public void StopCategory(SoundCategory category)
    {
        if (category == SoundCategory.BGM) { StopMusic(); return; }

        AudioMixerGroup targetGroup = GetMixerGroup(category);
        for (int i = activeSounds.Count - 1; i >= 0; i--)
        {
            if (activeSounds[i].source.outputAudioMixerGroup == targetGroup)
            {
                ReturnSource(activeSounds[i].source);
                activeSounds.RemoveAt(i);
            }
        }
    }

    /// <summary>Stops every sound across all categories.</summary>
    public void StopAll()
    {
        StopMusic();
        StopCategory(SoundCategory.SFX);
        StopCategory(SoundCategory.UI);
        StopCategory(SoundCategory.Voice);
    }

    // =========================================================================
    // Pause / Resume
    // =========================================================================

    /// <summary>Pauses all active sounds across all categories.</summary>
    public void PauseAll()
    {
        foreach (var s in activeSounds) s.source.Pause();
        activeMusicSource.Pause();
        inactiveMusicSource.Pause();
    }

    /// <summary>Resumes all paused sounds across all categories.</summary>
    public void ResumeAll()
    {
        foreach (var s in activeSounds) s.source.UnPause();
        activeMusicSource.UnPause();
        inactiveMusicSource.UnPause();
    }

    /// <summary>Pauses only sounds belonging to the given category.</summary>
    public void PauseCategory(SoundCategory category)
    {
        if (category == SoundCategory.BGM)
        {
            // Both sources are audible during a crossfade.
            activeMusicSource.Pause();
            inactiveMusicSource.Pause();
            return;
        }
        AudioMixerGroup targetGroup = GetMixerGroup(category);
        foreach (var s in activeSounds)
            if (s.source.outputAudioMixerGroup == targetGroup) s.source.Pause();
    }

    /// <summary>Resumes only sounds belonging to the given category.</summary>
    public void ResumeCategory(SoundCategory category)
    {
        if (category == SoundCategory.BGM)
        {
            activeMusicSource.UnPause();
            inactiveMusicSource.UnPause();
            return;
        }
        AudioMixerGroup targetGroup = GetMixerGroup(category);
        foreach (var s in activeSounds)
            if (s.source.outputAudioMixerGroup == targetGroup) s.source.UnPause();
    }

    // =========================================================================
    // Queries
    // =========================================================================

    /// <summary>Returns true if any active SFX source is playing the given clip.</summary>
    public bool IsPlaying(AudioClip clip)
    {
        foreach (var s in activeSounds)
            if (s.source.isPlaying && s.source.clip == clip) return true;
        return false;
    }

    /// <summary>Total active SFX voices at this moment.</summary>
    public int ActiveVoiceCount => activeSounds.Count;

    // =========================================================================
    // Convenience play variants
    // =========================================================================

    /// <summary>Plays the event only if none of its clips are already active.</summary>
    public AudioHandle PlayOnce(AudioEvent audioEvent)
    {
        if (audioEvent == null) return AudioHandle.Invalid;
        foreach (AudioClip clip in audioEvent.clips)
            if (IsPlaying(clip)) return AudioHandle.Invalid;
        return Play(audioEvent);
    }

    /// <summary>Stops the event's category then plays the new event.</summary>
    public AudioHandle PlayOrReplace(AudioEvent audioEvent)
    {
        if (audioEvent == null) return AudioHandle.Invalid;
        StopCategory(audioEvent.category);
        return Play(audioEvent);
    }

    // =========================================================================
    // Mute
    // =========================================================================

    /// <summary>
    /// Globally pauses/unpauses Unity audio. This preserves playback positions.
    /// Use MuteBus when you only want to silence one logical output bus.
    /// </summary>
    public void MuteAll(bool mute) => AudioListener.pause = mute;

    /// <summary>Mutes a logical bus without losing its remembered linear volume.</summary>
    public void MuteBus(AudioBus bus, bool mute)
    {
        string param = channelConfig?.GetVolumeParam(bus);
        var mixer = channelConfig?.GetMixer();

        if (mixer != null && !string.IsNullOrEmpty(param))
        {
            mixer.SetFloat(param, mute ? -80f : LinearToDb(GetVolume(bus)));
            return;
        }

        if (bus == AudioBus.Master)
            AudioListener.volume = mute ? 0f : GetVolume(AudioBus.Master);
    }

    public void MuteSFX(bool mute) => MuteBus(AudioBus.SFX, mute);
    public void MuteBGM(bool mute) => MuteBus(AudioBus.BGM, mute);
    public void MuteUI(bool mute) => MuteBus(AudioBus.UI, mute);
    public void MuteVoice(bool mute) => MuteBus(AudioBus.Voice, mute);

    // =========================================================================
    // dB helpers
    // =========================================================================

    static float LinearToDb(float linear)
        => linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;

    // =========================================================================
    // On-screen debug overlay  (editor + development builds only)
    // =========================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug")]
    [Tooltip("Show the voice/pool overlay in Editor and Development builds. Toggle at runtime too.")]
    public bool showDebugOverlay = true;

    void OnGUI()
    {
        if (!showDebugOverlay) return;
        string bgmClip = activeMusicSource.isPlaying ? activeMusicSource.clip?.name ?? "—" : "—";
        GUI.Label(new Rect(8, 8, 320, 20),
            $"[AudioManager]  Voices {activeSounds.Count}/{maxVoices}  " +
            $"Pool {pool.Count}  BGM: {bgmClip}");
    }
#endif
}