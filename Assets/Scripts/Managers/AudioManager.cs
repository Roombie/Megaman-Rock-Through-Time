using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioMixer mixer;
    public AudioMixerGroup sfxMixerGroup;
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup voiceMixerGroup;

    public int poolSize = 10;
    private Queue<AudioSource> sfxPool;
    private AudioSource musicSource;
    private readonly List<AudioSource> pausedSources = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePools();
        ApplySavedVolumes();
    }

    private void InitializePools()
    {
        sfxPool = new Queue<AudioSource>();
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxPool.Enqueue(source);
        }
    }

    private void ApplySavedVolumes()
    {
        var audioTypes = new SettingType[]
        {
            SettingType.MasterVolumeKey,
            SettingType.MusicVolumeKey,
            SettingType.SFXVolumeKey,
            SettingType.VoiceVolumeKey
        };

        foreach (SettingType type in audioTypes)
        {
            float volume = GetVolume(type, 1f);
            SetVolume(type, volume);
        }
    }

    public void SetVolume(SettingType type, float volume)
    {
        string param = type switch
        {   
            //  SettingType    =>    AudioMixers Exposed parameters
            SettingType.MasterVolumeKey => "MasterVolume",
            SettingType.MusicVolumeKey => "MusicVolume",
            SettingType.SFXVolumeKey => "SFXVolume",
            SettingType.VoiceVolumeKey => "VoiceVolume",
            _ => null
        };

        if (string.IsNullOrEmpty(param))
        {
            Debug.LogWarning($"[AudioManager] Mixer param not found for {type}");
            return;
        }

        // Vout = voltage, Vin = input voltage
        // Standard formula dB = 20 dB × log10(Vout / Vin)
        float dB = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
        mixer.SetFloat(param, dB);

        PlayerPrefs.SetFloat(SettingsKeys.Get(type), volume);
        PlayerPrefs.Save();
    }

    public float GetVolume(SettingType type, float fallback = 1f)
        => PlayerPrefs.GetFloat(SettingsKeys.Get(type), fallback);

    public void Play(AudioClip clip, SoundCategory category = SoundCategory.SFX, float volume = 1f, float pitch = 1f, bool loop = false)
    {
        if (clip == null) return;

        AudioSource source = GetAudioSource(category);
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = loop;
        source.Play();

        if (category == SoundCategory.SFX)
            StartCoroutine(ReturnToPoolAfterPlayback(source, clip.length / Mathf.Abs(pitch)));
    }

    private AudioSource GetAudioSource(SoundCategory category)
    {
        AudioSource source = (category == SoundCategory.SFX && sfxPool.Count > 0)
            ? sfxPool.Dequeue()
            : gameObject.AddComponent<AudioSource>();

        source.outputAudioMixerGroup = category switch
        {
            SoundCategory.Music => musicMixerGroup,
            SoundCategory.Voice => voiceMixerGroup,
            _ => sfxMixerGroup
        };

        if (category == SoundCategory.Music)
            musicSource = source;

        return source;
    }

    public void Stop(AudioClip clip)
    {
        foreach (var source in GetComponents<AudioSource>())
        {
            if (source.clip == clip)
            {
                source.Stop();
                if (!sfxPool.Contains(source) && source != musicSource)
                    sfxPool.Enqueue(source);
            }
        }
    }

    public bool IsPlaying(AudioClip clip)
        => System.Array.Exists(GetComponents<AudioSource>(), src => src.clip == clip && src.isPlaying);

    public void PauseAll() => PauseSources(GetComponents<AudioSource>());
    public void ResumeAll() => ResumeSources(pausedSources);

    public void PauseCategory(SoundCategory category)
    {
        foreach (var source in GetComponents<AudioSource>())
        {
            if (source.isPlaying && GetCategory(source) == category)
            {
                source.Pause();
                if (!pausedSources.Contains(source))
                    pausedSources.Add(source);
            }
        }
    }

    public void ResumeCategory(SoundCategory category)
    {
        foreach (var source in pausedSources)
        {
            if (source != null && GetCategory(source) == category)
                source.UnPause();
        }
    }

    public void StopAll()
    {
        foreach (var source in GetComponents<AudioSource>())
        {
            source.Stop();
            if (!sfxPool.Contains(source) && source != musicSource)
                sfxPool.Enqueue(source);
        }
    }

    private IEnumerator ReturnToPoolAfterPlayback(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.Stop();
        sfxPool.Enqueue(source);
    }

    public void PlayBackgroundMusic(AudioClip clip, float volume = 1f, float pitch = 1f, bool loop = true)
    {
        if (musicSource != null && musicSource.isPlaying)
            StartCoroutine(FadeOutMusic(musicSource, 1f));

        Play(clip, SoundCategory.Music, volume, pitch, loop);
    }

    private IEnumerator FadeOutMusic(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(startVolume, 0, t / duration);
            yield return null;
        }
        source.volume = 0;
        source.Stop();
    }

    private void PauseSources(AudioSource[] sources)
    {
        foreach (var source in sources)
        {
            if (source.isPlaying)
            {
                source.Pause();
                if (!pausedSources.Contains(source))
                    pausedSources.Add(source);
            }
        }
    }

    private void ResumeSources(List<AudioSource> sources)
    {
        foreach (var source in sources)
        {
            if (source != null)
                source.UnPause();
        }
        sources.Clear();
    }

    private SoundCategory GetCategory(AudioSource source)
    {
        if (source.outputAudioMixerGroup == musicMixerGroup) return SoundCategory.Music;
        if (source.outputAudioMixerGroup == voiceMixerGroup) return SoundCategory.Voice;
        return SoundCategory.SFX;
    }
}

public enum SoundCategory
{
    SFX,
    Music,
    Voice
}