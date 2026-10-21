using UnityEngine;

/// <summary>
/// Returned by AudioManager.Play() and AudioManager.PlayAtPosition().
/// Lets callers control a specific sound instance (stop it, check if it's
/// still playing, adjust volume at runtime) without needing a clip reference.
///
/// Usage example:
///
///     AudioHandle engineHandle = Audio.Play(engineEvent);
///     engineHandle.SetVolume(speed / maxSpeed); // update every frame
///     engineHandle.Stop(); // stop when car explodes
/// </summary>
public struct AudioHandle
{
    // Null-safe sentinel
    public static readonly AudioHandle Invalid = new(null);

    readonly AudioSource source;

    internal AudioHandle(AudioSource src) => source = src;

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>True while the sound is still playing.</summary>
    public bool IsPlaying => source != null && source.isPlaying;

    /// <summary>True if this handle points at a valid AudioSource.</summary>
    public bool IsValid => source != null;

    /// <summary>
    /// Playback position in seconds. Unity rewinds this to 0 when a non-looping
    /// clip ends or is stopped, and holds it while paused.
    /// </summary>
    public float PlaybackTime => source != null ? source.time : 0f;

    /// <summary>The clip currently assigned to this instance.</summary>
    public AudioClip Clip => source != null ? source.clip : null;

    /// <summary>Current volume of this instance (the event's randomised volume until overridden).</summary>
    public float Volume => source != null ? source.volume : 0f;

    /// <summary>Current pitch; converts clip seconds into real seconds.</summary>
    public float Pitch => source != null ? source.pitch : 1f;

    // -------------------------------------------------------------------------
    // Control
    // -------------------------------------------------------------------------

    /// <summary>Stops the sound immediately.</summary>
    public void Stop() { if (source != null) source.Stop(); }

    /// <summary>Pauses the sound.</summary>
    public void Pause() { if (source != null) source.Pause(); }

    /// <summary>Resumes a paused sound.</summary>
    public void Resume() { if (source != null) source.UnPause(); }

    /// <summary>
    /// Overrides the volume of this specific instance at runtime.
    /// Useful for dynamic sounds like engines or heartbeats driven by gameplay values.
    /// </summary>
    public void SetVolume(float volume)
    {
        if (source != null)
            source.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Jumps to a playback position in seconds, clamped to the clip. Lets music resume at the
    /// second it was interrupted instead of starting the track over.
    /// </summary>
    public void Seek(float seconds)
    {
        if (source == null || source.clip == null) return;
        source.time = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, source.clip.length - 0.05f));
    }

    /// <summary>Overrides the pitch of this instance at runtime.</summary>
    public void SetPitch(float pitch)
    {
        if (source != null)
            source.pitch = pitch;
    }

    /// <summary>
    /// Sets 3D spatial parameters on this instance after it has started playing.
    /// Allows optional playback adapters to apply spatial settings after playback begins.
    /// </summary>
    public void SetSpatial(float spatialBlend, float minDistance, float maxDistance, Vector3 position)
    {
        if (source == null) return;
        source.spatialBlend          = spatialBlend;
        source.minDistance           = minDistance;
        source.maxDistance           = maxDistance;
        source.transform.position    = position;
    }
}