using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Manages the full lifecycle of Addressable audio clips:
///   load → play → auto-release when finished.
///
/// ── Architecture ──────────────────────────────────────────────────────────
///   This sits alongside AudioManager and delegates actual playback to it
///   through the interface-backed Audio facade. The AudioManager
///   handles pooling, voice limits and mixing; this class handles only the
///   async load/release cycle.
///
///   AudioMixer groups are intentionally NOT loaded via Addressables — the
///   existing AudioManager mixer references are reused to avoid duplicate
///   mixer instances (a known Unity Addressables pitfall).
///
/// ── Usage ─────────────────────────────────────────────────────────────────
///   // Fire and forget
///   AddressableAudioLoader.Instance.Play(myAddressableEvent);
///
///   // At a world position
///   AddressableAudioLoader.Instance.PlayAtPosition(myAddressableEvent, transform.position);
///
///   // Yield until clip is loaded and playing
///   yield return AddressableAudioLoader.Instance.Play(myAddressableEvent);
///
///   // Preload a whole Addressables label before a scene (e.g. loading screen)
///   yield return AddressableAudioLoader.Instance.PreloadLabel("BossFightAudio");
///   // ... then play normally — clips already in memory, no async stall
///   AddressableAudioLoader.Instance.Play(myAddressableEvent);
///
///   // Release preloaded label when leaving the scene
///   AddressableAudioLoader.Instance.ReleaseLabel("BossFightAudio");
///
/// ── Memory rules ──────────────────────────────────────────────────────────
///   • One-shot clips  → released automatically after playback ends.
///   • Looping clips   → released when you call Stop() on the AudioHandle,
///                       or when ReleaseAll() / scene unload is triggered.
///   • Preloaded clips → stay in memory until ReleaseLabel() is called.
///     Never release a preloaded clip while it is still playing.
/// </summary>
[DefaultExecutionOrder(-50)]   // initialise before other scripts use it
public class AddressableAudioLoader : MonoBehaviour
{
    // =========================================================================
    // Singleton
    // =========================================================================

    public static AddressableAudioLoader Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================================
    // Internal tracking
    // =========================================================================

    /// <summary>
    /// Tracks a single async clip load + its associated AudioHandle so we can
    /// release the Addressable handle once the clip is done playing.
    /// </summary>
    class ActiveLoad
    {
        public AsyncOperationHandle<AudioClip> opHandle;
        public AudioHandle audioHandle;
        public bool loop;
        public bool released;
    }

    readonly List<ActiveLoad>   activeLoads    = new();
    readonly List<ActiveLoad>   pendingRemoval = new();

    // Preloaded label → list of operation handles for that label's clips
    readonly Dictionary<string, List<AsyncOperationHandle<AudioClip>>> preloadedLabels = new();
    AudioListener cachedListener;

    // =========================================================================
    // Update — poll for finished one-shots and release them
    // =========================================================================

    void Update()
    {
        pendingRemoval.Clear();

        foreach (var load in activeLoads)
        {
            if (load.released) { pendingRemoval.Add(load); continue; }

            // Non-looping sounds: release once AudioSource has stopped
            if (!load.loop && !load.audioHandle.IsPlaying)
            {
                ReleaseLoad(load);
                pendingRemoval.Add(load);
            }
        }

        foreach (var load in pendingRemoval)
            activeLoads.Remove(load);
    }

    void OnDestroy() => ReleaseAll();

    // =========================================================================
    // Public API — Play
    // =========================================================================

    /// <summary>
    /// Loads the clip asynchronously and plays it via AudioManager.
    /// Returns a Coroutine you can yield on to wait until the clip is loaded
    /// and playing. Fire-and-forget callers can ignore the return value.
    /// </summary>
    public Coroutine Play(AudioEventAddressable audioEvent)
        => StartCoroutine(PlayRoutine(audioEvent, Vector3.zero, false));

    /// <summary>
    /// Loads the clip and plays it at a world position (3D spatial audio).
    /// </summary>
    public Coroutine PlayAtPosition(AudioEventAddressable audioEvent, Vector3 position)
        => StartCoroutine(PlayRoutine(audioEvent, position, true));

    // =========================================================================
    // Core coroutine
    // =========================================================================

    IEnumerator PlayRoutine(AudioEventAddressable audioEvent, Vector3 position, bool usePosition)
    {
        if (audioEvent == null)
        {
            Debug.LogWarning("[AddressableAudioLoader] audioEvent is null.");
            yield break;
        }

        if (!audioEvent.CanPlay()) yield break;

        // Avoid loading an Addressable that cannot be played yet.
        if (!Audio.IsReady)
        {
            Debug.LogWarning("[AddressableAudioLoader] No audio service is registered — cannot play.");
            yield break;
        }

        // Pick a random clip reference (no-repeat shuffle)
        var clipRef = audioEvent.GetClipReference();
        if (clipRef == null || !clipRef.RuntimeKeyIsValid())
        {
            Debug.LogWarning($"[AddressableAudioLoader] '{audioEvent.name}' has no valid clip reference.");
            yield break;
        }

        // ── Async load ───────────────────────────────────────────────────────
        var opHandle = Addressables.LoadAssetAsync<AudioClip>(clipRef);

        // Yield until loaded
        yield return opHandle;

        if (opHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[AddressableAudioLoader] Failed to load clip for '{audioEvent.name}': {opHandle.OperationException}");
            Addressables.Release(opHandle);
            yield break;
        }

        AudioClip clip = opHandle.Result;
        audioEvent.MarkPlayed();

        // ── Play via AudioManager's raw-clip overload ─────────────────────────
        AudioHandle handle = usePosition
            ? PlayClipAtPosition(clip, audioEvent, position)
            : PlayClip(clip, audioEvent);

        if (!handle.IsValid)
        {
            // AudioManager rejected the clip (voice limit, etc.) — release immediately
            Addressables.Release(opHandle);
            yield break;
        }

        // ── Track for auto-release ────────────────────────────────────────────
        activeLoads.Add(new ActiveLoad
        {
            opHandle    = opHandle,
            audioHandle = handle,
            loop        = audioEvent.loop,
            released    = false,
        });
    }

    // =========================================================================
    // Preloading by Addressables label
    // =========================================================================

    /// <summary>
    /// Loads all AudioClip assets that share the given Addressables label into
    /// memory. After this coroutine completes, any AudioEventAddressable that
    /// references one of those clips will play instantly with no async delay.
    ///
    /// Call this during a loading screen, then call ReleaseLabel() when leaving
    /// the scene that needed those clips.
    ///
    /// Example:
    ///   yield return AddressableAudioLoader.Instance.PreloadLabel("Level1Audio");
    /// </summary>
    public Coroutine PreloadLabel(string label)
        => StartCoroutine(PreloadLabelRoutine(label));

    IEnumerator PreloadLabelRoutine(string label)
    {
        if (preloadedLabels.ContainsKey(label))
        {
            Debug.Log($"[AddressableAudioLoader] Label '{label}' is already preloaded.");
            yield break;
        }

        // Load all AudioClips under this label
        var locHandle = Addressables.LoadResourceLocationsAsync(label, typeof(AudioClip));
        yield return locHandle;

        if (locHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[AddressableAudioLoader] Could not locate assets for label '{label}'.");
            Addressables.Release(locHandle);
            yield break;
        }

        var locations = locHandle.Result;
        var handles   = new List<AsyncOperationHandle<AudioClip>>(locations.Count);

        foreach (var loc in locations)
        {
            var h = Addressables.LoadAssetAsync<AudioClip>(loc);
            handles.Add(h);
            yield return h;

            if (h.Status != AsyncOperationStatus.Succeeded)
                Debug.LogWarning($"[AddressableAudioLoader] Failed to preload: {loc.PrimaryKey}");
        }

        Addressables.Release(locHandle);
        preloadedLabels[label] = handles;
        Debug.Log($"[AddressableAudioLoader] Preloaded label '{label}': {handles.Count} clip(s).");
    }

    /// <summary>
    /// Releases all clips previously loaded by PreloadLabel().
    /// Do NOT call this while any of those clips are still playing.
    /// </summary>
    public void ReleaseLabel(string label)
    {
        if (!preloadedLabels.TryGetValue(label, out var handles))
        {
            Debug.LogWarning($"[AddressableAudioLoader] Label '{label}' was not preloaded.");
            return;
        }

        foreach (var h in handles)
            if (h.IsValid()) Addressables.Release(h);

        preloadedLabels.Remove(label);
        Debug.Log($"[AddressableAudioLoader] Released label '{label}'.");
    }

    /// <summary>
    /// Releases all active one-shot loads and all preloaded labels.
    /// Called automatically when this MonoBehaviour is destroyed.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var load in activeLoads)
            ReleaseLoad(load);
        activeLoads.Clear();

        foreach (var label in new List<string>(preloadedLabels.Keys))
            ReleaseLabel(label);
    }

    // =========================================================================
    // Stop helpers (for looping sounds)
    // =========================================================================

    /// <summary>
    /// Stops a looping sound started via this loader and releases its clip.
    /// </summary>
    public void Stop(AudioHandle handle)
    {
        foreach (var load in activeLoads)
        {
            if (load.audioHandle.Equals(handle))
            {
                load.audioHandle.Stop();
                ReleaseLoad(load);
                return;
            }
        }
        // Handle not tracked here — just stop it directly
        handle.Stop();
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    AudioHandle PlayClip(AudioClip clip, AudioEventAddressable evt)
    {
        // Use AudioManager's raw-clip Play overload so pooling and voice
        // limiting still apply, and the mixer group is routed correctly.
        return Audio.Play(
            clip,
            evt.category,
            evt.GetVolume(),
            evt.GetPitch(),
            evt.loop);
    }

    AudioHandle PlayClipAtPosition(AudioClip clip, AudioEventAddressable evt, Vector3 position)
    {
        // Distance-cull check mirrors AudioManager.PlayAtPosition
        if (cachedListener == null)
            cachedListener = FindFirstObjectByType<AudioListener>();

        if (cachedListener != null)
        {
            float dist = Vector3.Distance(cachedListener.transform.position, position);
            if (dist > evt.maxDistance) return AudioHandle.Invalid;
        }

        // Play via AudioManager's raw-clip overload (handles pooling + mixing)
        AudioHandle handle = Audio.Play(
            clip,
            evt.category,
            evt.GetVolume(),
            evt.GetPitch(),
            evt.loop);

        // Patch spatial settings onto the live source via AudioHandle.SetSpatial
        if (handle.IsValid)
            handle.SetSpatial(evt.spatialBlend, evt.minDistance, evt.maxDistance, position);

        return handle;
    }

    void ReleaseLoad(ActiveLoad load)
    {
        if (load.released) return;
        load.released = true;

        if (load.opHandle.IsValid())
            Addressables.Release(load.opHandle);
    }

    // =========================================================================
    // Query
    // =========================================================================

    /// <summary>Total number of clips currently loaded by this loader.</summary>
    public int ActiveLoadCount => activeLoads.Count;

    /// <summary>Returns true if a label has been preloaded and not yet released.</summary>
    public bool IsLabelPreloaded(string label) => preloadedLabels.ContainsKey(label);
}
