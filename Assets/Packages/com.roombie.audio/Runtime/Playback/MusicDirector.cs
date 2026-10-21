using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides which music plays. MusicRequest components register while requested; the director
/// plays the one on the highest MusicPriority layer (ties: the most recently requested) through
/// the AudioManager's music channel.
///
/// Each playlist remembers its own track and position (per AudioEvent), so returning from a
/// menu resumes the same song at the same second, and scenes/menus never reference each other.
///
/// Three fade lengths, because one global crossfade can't fit every case:
///   • defaultTransition — switching between requests (a MusicRequest can override it)
///   • trackCrossfade    — one playlist song into the next
///   • silenceFade       — nothing requested (or a silence request) → fade out
///
/// Created automatically the first time a MusicRequest registers and kept across scenes.
/// Place one yourself only to change these settings in the Inspector (first scene is enough).
///
/// Replaces MusicPlaylistPlayer: that component stops for good whenever other code plays BGM
/// and restarts from track 1, so it can't come back from an override. Don't keep both.
/// </summary>
[DisallowMultipleComponent]
public sealed class MusicDirector : MonoBehaviour
{
    [Header("Fades (seconds, real time)")]
    [Tooltip("Crossfade when a different MusicRequest takes over, unless it sets its own.")]
    [Min(0f)] public float defaultTransition = 0.75f;

    [Tooltip("Crossfade between consecutive songs of a playlist. The next song starts this " +
             "long before the current one ends.")]
    [Min(0f)] public float trackCrossfade = 2f;

    [Tooltip("Fade-out when nothing requests music, or a request with no music is active.")]
    [Min(0f)] public float silenceFade = 1f;

    [Tooltip("When no request is active, wait this long before fading out. Covers scene " +
             "loads, where the old scene's request disappears a moment before the new one's " +
             "registers.")]
    [Min(0f)] public float silenceGrace = 0.25f;

    [Header("Debug")]
    [Tooltip("Log every switch, resume position and track change to the Console.")]
    public bool logTransitions = false;

    // A stop within this many seconds of the end counts as the track finishing naturally.
    const float EndTolerance = 0.5f;

    static MusicDirector instance;
    static bool quitting;
    static readonly List<MusicRequest> requests = new List<MusicRequest>();   // oldest → newest

    sealed class PlaylistState
    {
        public int[] order;
        public int   position = -1;
        public float time;
    }

    // Keyed by AudioEvent, not by request, so the gameplay playlist keeps its place even when
    // its MusicRequest is recreated (scene reload) or two requests share the same event.
    readonly Dictionary<AudioEvent, PlaylistState> states = new Dictionary<AudioEvent, PlaylistState>();

    MusicRequest activeRequest;   // compared with ReferenceEquals: it may already be destroyed
    bool         hasActive;       // distinguishes "silence request active" from "nothing active"
    AudioEvent   activeEvent;     // null while silent
    MusicOrder   activeOrder;
    float        activeVolume;
    AudioHandle  current = AudioHandle.Invalid;
    AudioClip    currentClip;
    float        lastPlaybackTime;
    float        emptySince = -1f;
    bool         pendingStart;
    bool         yieldedToExternalMusic;

    /// <summary>The request currently in control of the music, or null.</summary>
    public static MusicRequest Active => instance != null ? instance.activeRequest : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        quitting = false;
        requests.Clear();
    }

    /// <summary>Registers (or re-registers as most recent) a request.</summary>
    public static void Add(MusicRequest request)
    {
        if (request == null) return;
        requests.Remove(request);
        requests.Add(request);
        EnsureInstance();
    }

    public static void Remove(MusicRequest request) => requests.Remove(request);

    /// <summary>Skips to the next track of the active playlist.</summary>
    public static void Next()
    {
        if (instance != null && instance.activeEvent != null && Audio.IsReady)
            instance.Advance(instance.trackCrossfade);
    }

    static void EnsureInstance()
    {
        if (instance != null || quitting) return;

        instance = FindFirstObjectByType<MusicDirector>();
        if (instance == null)
            instance = new GameObject("MusicDirector").AddComponent<MusicDirector>();
    }

    // -------------------------------------------------------------------------

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Application.quitting += OnQuitting;
    }

    void OnDestroy()
    {
        Application.quitting -= OnQuitting;
        if (instance == this) instance = null;
    }

    static void OnQuitting() => quitting = true;

    void Update()
    {
        requests.RemoveAll(r => r == null);
        MusicRequest top = FindTop();

        if (!ReferenceEquals(top, activeRequest) || (top == null && hasActive))
        {
            if (top == null)
            {
                // Nothing wants music. Wait a moment (scene load), then fade out.
                if (emptySince < 0f) emptySince = Time.unscaledTime;
                if (Time.unscaledTime - emptySince < silenceGrace) return;

                SaveState();
                Log("No music requested; fading out.");
                activeRequest = null;
                hasActive     = false;
                activeEvent   = null;
                current       = AudioHandle.Invalid;
                Audio.FadeOutMusic(silenceFade);
                return;
            }

            emptySince = -1f;
            SwitchTo(top);
        }
        else
        {
            emptySince = -1f;
        }

        if (pendingStart && Audio.IsReady)
        {
            pendingStart = false;
            StartActive(activeRequest != null && activeRequest.resumeWhereLeftOff, TransitionFor(activeRequest));
        }

        if (activeEvent != null && !pendingStart) Tick();
    }

    MusicRequest FindTop()
    {
        MusicRequest best = null;
        foreach (MusicRequest request in requests)
            if (request.IsRequested && (best == null || request.priority >= best.priority))
                best = request;   // >= with oldest→newest order: newest wins ties
        return best;
    }

    float TransitionFor(MusicRequest request) =>
        request != null && request.fadeSeconds >= 0f ? request.fadeSeconds : defaultTransition;

    // -------------------------------------------------------------------------

    void SwitchTo(MusicRequest request)
    {
        SaveState();

        activeRequest          = request;
        hasActive              = true;
        activeEvent            = request.music;
        activeOrder            = request.order;
        activeVolume           = request.volume;
        yieldedToExternalMusic = false;
        current                = AudioHandle.Invalid;
        currentClip            = null;
        pendingStart           = false;

        if (activeEvent == null || !HasAnyClip(activeEvent))
        {
            // Deliberate silence (no event), or an event with nothing in it.
            activeEvent = null;
            Log($"'{request.name}' ({request.priority}) requests silence.");
            Audio.FadeOutMusic(request.fadeSeconds >= 0f ? request.fadeSeconds : silenceFade);
            return;
        }

        Log($"Switching to '{request.name}' ({request.priority}, '{activeEvent.name}').");

        if (Audio.IsReady) StartActive(request.resumeWhereLeftOff, TransitionFor(request));
        else pendingStart = true;   // AudioManager not registered yet (first frame).
    }

    void StartActive(bool resume, float fade)
    {
        PlaylistState state = GetState();

        if (resume && state.position >= 0)
        {
            Log($"Resuming track {state.position + 1}/{state.order.Length} at {state.time:0.0}s.");
            PlayTrack(state, state.position, state.time, fade);
        }
        else
        {
            state.position = -1;
            Advance(fade);
        }
    }

    PlaylistState GetState()
    {
        if (!states.TryGetValue(activeEvent, out PlaylistState state))
        {
            state = new PlaylistState();
            states[activeEvent] = state;
        }

        // Rebuild if the clip list was edited since last time.
        if (state.order == null || state.order.Length != activeEvent.clips.Length)
        {
            state.order    = BuildOrder(activeEvent.clips.Length);
            state.position = -1;
            state.time     = 0f;
        }

        return state;
    }

    void SaveState()
    {
        if (activeEvent == null || !states.TryGetValue(activeEvent, out PlaylistState state)) return;

        state.time = current.IsValid && current.Clip == currentClip && !yieldedToExternalMusic
            ? current.PlaybackTime
            : 0f;
    }

    void Advance(float fade)
    {
        PlaylistState state = GetState();
        AudioClip[] clips = activeEvent.clips;

        // Skip empty slots, but give up after one full pass.
        for (int attempts = 0; attempts < state.order.Length; attempts++)
        {
            state.position++;
            if (state.position >= state.order.Length)
            {
                state.position = 0;
                if (activeOrder == MusicOrder.Shuffle) Shuffle(state.order);
            }

            if (clips[state.order[state.position]] == null) continue;

            PlayTrack(state, state.position, 0f, fade);
            return;
        }

        Debug.LogWarning($"[MusicDirector] '{activeEvent.name}' has no valid clips.", activeEvent);
        activeEvent = null;
    }

    void PlayTrack(PlaylistState state, int position, float time, float fade)
    {
        AudioClip clip = activeEvent.clips[state.order[position]];
        if (clip == null) { Advance(fade); return; }

        // Resuming right at the end would just trigger the next track; start it instead.
        if (time > 0f && clip.length - time <= trackCrossfade + EndTolerance) time = 0f;

        current          = Audio.PlayMusic(clip, activeVolume, loop: IsSingleTrack(), fade, time);
        currentClip      = clip;
        state.time       = time;
        lastPlaybackTime = time;
        Log($"Playing '{clip.name}' from {time:0.0}s (fade {fade:0.##}s).");
    }

    static bool HasAnyClip(AudioEvent audioEvent)
    {
        if (audioEvent.clips == null) return false;
        foreach (AudioClip clip in audioEvent.clips)
            if (clip != null) return true;
        return false;
    }

    bool IsSingleTrack()
    {
        int valid = 0;
        foreach (AudioClip clip in activeEvent.clips)
            if (clip != null && ++valid > 1) return false;
        return true;
    }

    void Tick()
    {
        if (yieldedToExternalMusic) return;
        if (!current.IsValid) return;

        AudioClip clip = current.Clip;

        // Some other script played its own BGM. Don't fight it; take over again on the next
        // request change. (Prefer a MusicRequest over calling Audio.Play with BGM directly.)
        if (clip != currentClip)
        {
            yieldedToExternalMusic = true;
            Debug.Log($"[MusicDirector] Other code started BGM '{(clip != null ? clip.name : "none")}'; " +
                      "pausing playlist control until the next MusicRequest change.", this);
            return;
        }

        if (clip != null && current.IsPlaying)
        {
            lastPlaybackTime = current.PlaybackTime;
            if (IsSingleTrack()) return;      // Looping; nothing to advance.

            float early     = Mathf.Min(trackCrossfade, clip.length * 0.5f);
            float remaining = (clip.length - lastPlaybackTime) / Mathf.Max(0.01f, Mathf.Abs(current.Pitch));
            if (remaining <= early) Advance(trackCrossfade);
            return;
        }

        // Paused (PauseCategory/PauseAll): Unity keeps the position, so just wait.
        if (current.PlaybackTime > 0f) return;

        // Stopped and rewound: either the track ended, or someone called StopMusic/FadeOutMusic.
        if (clip != null && lastPlaybackTime >= clip.length - EndTolerance)
            Advance(trackCrossfade);
        else
            yieldedToExternalMusic = true;
    }

    void Log(string message)
    {
        if (logTransitions) Debug.Log("[MusicDirector] " + message, this);
    }

    // -------------------------------------------------------------------------

    int[] BuildOrder(int count)
    {
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        if (activeOrder == MusicOrder.Shuffle) Shuffle(order);
        return order;
    }

    static void Shuffle(int[] order)
    {
        int previousLast = order.Length > 0 ? order[order.Length - 1] : -1;

        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        // No repeat across the cycle boundary.
        if (order.Length > 1 && order[0] == previousLast)
            (order[0], order[1]) = (order[1], order[0]);
    }
}