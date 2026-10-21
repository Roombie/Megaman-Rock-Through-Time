using UnityEngine;

/// <summary>
/// ScriptableObject that represents a single logical sound event.
/// Create via: Assets → Create → Audio → Audio Event
///
/// Designers configure everything here — clips, randomization, cooldown,
/// priority and spatial settings — without touching any code.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Audio Event")]
public class AudioEvent : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Clips
    // -------------------------------------------------------------------------

    [Header("Clips")]
    [Tooltip("One or more clips. A random one is chosen each play to avoid repetition.")]
    public AudioClip[] clips;

    // -------------------------------------------------------------------------
    // Category
    // -------------------------------------------------------------------------

    [Header("Category")]
    public SoundCategory category = SoundCategory.SFX;

    // -------------------------------------------------------------------------
    // Randomization  (micro-variation prevents robotic repetition)
    // -------------------------------------------------------------------------

    [Header("Randomization")]
    [Tooltip("Subtle pitch shift applied every play. Keep the range small (±0.05) for realism.")]
    public Vector2 pitchRange = new(0.95f, 1.05f);

    [Tooltip("Subtle volume shift applied every play.")]
    public Vector2 volumeRange = new(0.9f, 1f);

    // -------------------------------------------------------------------------
    // Playback settings
    // -------------------------------------------------------------------------

    [Header("Playback")]
    public bool loop = false;

    [Tooltip("Minimum seconds between consecutive plays of this event. " +
             "Prevents audio spam (e.g. rapid-fire hits triggering the same sound 30×/sec).")]
    [Min(0f)]
    public float cooldown = 0f;

    // -------------------------------------------------------------------------
    // Priority  (used by the voice limiter — higher = more important)
    // -------------------------------------------------------------------------

    [Header("Priority")]
    [Tooltip("Higher priority sounds survive when the voice limit is reached. " +
             "Critical always plays. Low is first to be evicted.")]
    public AudioPriority priority = AudioPriority.Medium;

    // -------------------------------------------------------------------------
    // Spatial audio
    // -------------------------------------------------------------------------

    [Header("Spatial Audio")]
    [Tooltip("0 = fully 2D (UI, music). 1 = fully 3D (world sounds). " +
             "Set > 0 when calling PlayAtPosition().")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;

    [Tooltip("3D audio: distance at which the sound is at full volume.")]
    public float minDistance = 1f;

    [Tooltip("3D audio: distance beyond which the sound is silent.")]
    public float maxDistance = 50f;

    // -------------------------------------------------------------------------
    // Runtime state  (not serialized — reset each session)
    // -------------------------------------------------------------------------

    float lastPlayTime = -999f;

    // -------------------------------------------------------------------------
    // Public helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns true when the cooldown has elapsed since the last play.</summary>
    public bool CanPlay()
    {
        // A zero-cooldown event must never be rejected. Besides being the expected meaning of
        // zero, this protects one-shot startup sounds when Enter Play Mode Options keeps the
        // ScriptableObject alive between runs while the clock resets back to zero.
        if (cooldown <= 0f) return true;

        // UNSCALED on purpose. Pause, sleep and hitstop set Time.timeScale to 0, which freezes
        // Time.time; with a scaled clock the first UI highlight in the pause menu played and every
        // later one was rejected as "still cooling down" until the game resumed.
        float now = Time.unscaledTime;

        // With domain reload disabled, lastPlayTime can belong to the previous Play session.
        // Its clock is then ahead of the new session's clock; treat that as fresh state.
        if (now < lastPlayTime)
        {
            lastPlayTime = -999f;
            return true;
        }

        return now - lastPlayTime >= cooldown;
    }

    /// <summary>Stamps the current time so CanPlay() starts the cooldown countdown.</summary>
    public void MarkPlayed() => lastPlayTime = Time.unscaledTime;

    /// <summary>
    /// Returns a random clip, never repeating the same index twice in a row
    /// (no-repeat shuffle) when more than one clip is present.
    /// </summary>
    public AudioClip GetClip()
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        // No-repeat: pick again if we land on the same index as last time
        int index;
        do { index = Random.Range(0, clips.Length); }
        while (index == lastClipIndex && clips.Length > 1);

        lastClipIndex = index;
        return clips[index];
    }

    /// <summary>Returns a randomised pitch within pitchRange.</summary>
    public float GetPitch() => Random.Range(pitchRange.x, pitchRange.y);

    /// <summary>Returns a randomised volume within volumeRange.</summary>
    public float GetVolume() => Random.Range(volumeRange.x, volumeRange.y);

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    int lastClipIndex = -1;
}