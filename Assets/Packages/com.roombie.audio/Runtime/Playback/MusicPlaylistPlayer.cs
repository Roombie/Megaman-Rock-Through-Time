using UnityEngine;

/// <summary>
/// Plays a list of BGM tracks one after another and loops back to the first
/// track after the last one finishes. Tracks go through the AudioManager's
/// music crossfade, so each song blends into the next.
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add this component to a GameObject in your scene
///     (or on your AudioManager prefab to keep it across scenes).
///  2. Drag your music clips into the tracks array, in the order to play.
///  3. Set startNextEarly to match AudioManager.crossfadeDuration for a
///     smooth blend, or 0 to start each song right after the previous ends.
///  4. Import BGM clips with Load Type = Streaming to keep memory low.
///
/// ── Runtime control ──────────────────────────────────────────────────────
///     playlist.Play();   // start from the first track
///     playlist.Next();   // skip to the next track
///     playlist.Stop();   // stop the music and the playlist
///
/// If other code plays different BGM or calls Audio.StopMusic(), the playlist
/// stops advancing instead of fighting over the music channel. Call Play()
/// again to resume it.
/// </summary>
public class MusicPlaylistPlayer : MonoBehaviour
{
    [Header("Tracks")]
    [Tooltip("Music clips played in order. After the last one, playback restarts from the first.")]
    public AudioClip[] tracks;

    [Tooltip("Playback volume for every track.")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Order")]
    [Tooltip("If true, tracks play in a random order that is reshuffled every full cycle.")]
    public bool shuffle = false;

    [Tooltip("Start the playlist automatically when this component is enabled.")]
    public bool playOnEnable = true;

    [Header("Transitions")]
    [Tooltip("Seconds before a track ends to start the next one. " +
             "Match AudioManager.crossfadeDuration for a smooth blend; 0 = no overlap.")]
    [Min(0f)]
    public float startNextEarly = 1f;

    // Seconds from the end within which a stop counts as the track finishing
    // naturally rather than being stopped by other code.
    const float EndTolerance = 0.5f;

    AudioHandle current;
    AudioClip   currentClip;
    int[]       order;
    int         position = -1;
    bool        isRunning;
    float       lastPlaybackTime;

    /// <summary>True while the playlist is active and advancing through tracks.</summary>
    public bool IsRunning => isRunning;

    /// <summary>The clip currently playing, or null.</summary>
    public AudioClip CurrentTrack => isRunning ? current.Clip : null;

    // -------------------------------------------------------------------------

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    // The current track keeps playing to its end; the playlist just stops advancing.
    void OnDisable() => isRunning = false;

    // -------------------------------------------------------------------------
    // Public control
    // -------------------------------------------------------------------------

    /// <summary>Starts the playlist from the first track (or a new shuffle).</summary>
    public void Play()
    {
        if (tracks == null || tracks.Length == 0) return;

        BuildOrder();
        position  = -1;
        current   = AudioHandle.Invalid;
        isRunning = true;

        // AudioManager registers itself in Awake; if it isn't ready yet,
        // Update starts the first track once it is.
        if (Audio.IsReady) PlayNext();
    }

    /// <summary>Skips to the next track, crossfading into it.</summary>
    public void Next()
    {
        if (isRunning) PlayNext();
    }

    /// <summary>Stops the music and the playlist.</summary>
    public void Stop()
    {
        isRunning = false;
        if (current.IsPlaying) Audio.StopMusic();
        current = AudioHandle.Invalid;
    }

    // -------------------------------------------------------------------------

    void Update()
    {
        if (!isRunning) return;

        if (!current.IsValid)
        {
            if (Audio.IsReady) PlayNext();
            return;
        }

        AudioClip clip = current.Clip;

        // Another caller reused this music source for its own BGM.
        if (clip != currentClip)
        {
            isRunning = false;
            return;
        }

        if (current.IsPlaying && clip != null)
        {
            lastPlaybackTime = current.PlaybackTime;

            // Cap the overlap so very short clips can't retrigger every frame.
            float early     = Mathf.Min(startNextEarly, clip.length * 0.5f);
            float remaining = (clip.length - lastPlaybackTime) / Mathf.Max(0.01f, Mathf.Abs(current.Pitch));

            if (remaining <= early) PlayNext();
            return;
        }

        // Paused: Unity keeps the playback position, so just wait.
        if (current.PlaybackTime > 0f) return;

        // Stopped and rewound. If it was near the end, the track finished
        // naturally; otherwise other code took over the music channel.
        if (clip != null && lastPlaybackTime >= clip.length - EndTolerance)
            PlayNext();
        else
            isRunning = false;
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    void PlayNext()
    {
        // Skip empty slots, but give up after one full pass so an array of
        // nulls can't loop forever.
        for (int attempts = 0; attempts < order.Length; attempts++)
        {
            position++;
            if (position >= order.Length)
            {
                position = 0;
                if (shuffle) Shuffle();
            }

            AudioClip clip = tracks[order[position]];
            if (clip == null) continue;

            current          = Audio.Play(clip, SoundCategory.BGM, volume, 1f, loop: false);
            currentClip      = clip;
            lastPlaybackTime = 0f;
            return;
        }

        Debug.LogWarning("[MusicPlaylistPlayer] No valid tracks assigned.", this);
        isRunning = false;
    }

    void BuildOrder()
    {
        order = new int[tracks.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        if (shuffle) Shuffle();
    }

    void Shuffle()
    {
        int previousLast = order.Length > 0 ? order[^1] : -1;

        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        // Avoid playing the same track twice in a row across the cycle boundary.
        if (order.Length > 1 && order[0] == previousLast)
            (order[0], order[1]) = (order[1], order[0]);
    }
}
