using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressable variant of AudioEvent. Stores AssetReferenceT&lt;AudioClip&gt; instead
/// of direct AudioClip references, so clips are never loaded at startup.
///
/// ── When to use this vs plain AudioEvent ──────────────────────────────────
///   AudioEvent          → short SFX used frequently and already in memory.
///   AudioEventAddressable → large BGM tracks, infrequent VO lines, or any clip
///                           you want to load/unload per scene to save RAM.
///
/// ── Setup ──────────────────────────────────────────────────────────────────
///  1. Right-click → Create → Audio → Addressable Audio Event
///  2. Assign one or more AudioClip entries in the Inspector.
///  3. Mark each referenced AudioClip asset as Addressable in the Inspector
///     (tick the Addressable checkbox or add it to an Addressables group).
///  4. Call AddressableAudioLoader.Instance.Play(myEvent) — loading is handled
///     automatically; you get back a Coroutine you can yield on if needed.
///
/// ── Memory lifecycle ───────────────────────────────────────────────────────
///   Each clip is loaded just before playback and released automatically once
///   the AudioSource stops playing. You never need to call Release() yourself
///   unless you cancel playback early via the returned AudioHandle.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Addressable Audio Event")]
public class AudioEventAddressable : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Clips  (Addressable references — no memory cost until loaded)
    // -------------------------------------------------------------------------

    [Header("Clips")]
    [Tooltip("One or more Addressable AudioClip references. A random one is chosen each play.")]
    public AssetReferenceT<AudioClip>[] clips;

    // -------------------------------------------------------------------------
    // Shared settings — identical fields to AudioEvent for consistency
    // -------------------------------------------------------------------------

    [Header("Category")]
    public SoundCategory category = SoundCategory.SFX;

    [Header("Randomization")]
    [Tooltip("Subtle pitch shift each play. Keep range small (±0.05) for realism.")]
    public Vector2 pitchRange = new(0.95f, 1.05f);

    [Tooltip("Subtle volume shift each play.")]
    public Vector2 volumeRange = new(0.9f, 1f);

    [Header("Playback")]
    public bool loop = false;

    [Tooltip("Minimum seconds between consecutive plays of this event.")]
    [Min(0f)]
    public float cooldown = 0f;

    [Header("Priority")]
    public AudioPriority priority = AudioPriority.Medium;

    [Header("Spatial Audio")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;
    public float minDistance  = 1f;
    public float maxDistance  = 50f;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    float lastPlayTime = -999f;
    int   lastClipIndex = -1;

    // -------------------------------------------------------------------------
    // Public helpers
    // -------------------------------------------------------------------------

    public bool CanPlay()
    {
        if (cooldown <= 0f) return true;

        float now = Time.time;
        if (now < lastPlayTime)
        {
            lastPlayTime = -999f;
            return true;
        }

        return now - lastPlayTime >= cooldown;
    }
    public void MarkPlayed()   => lastPlayTime = Time.time;
    public float GetPitch()    => Random.Range(pitchRange.x,  pitchRange.y);
    public float GetVolume()   => Random.Range(volumeRange.x, volumeRange.y);

    /// <summary>
    /// Returns a random AudioClip; using no-repeat shuffle.
    /// Returns null if the clips array is empty.
    /// </summary>
    public AssetReferenceT<AudioClip> GetClipReference()
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        int index;
        do { index = Random.Range(0, clips.Length); }
        while (index == lastClipIndex && clips.Length > 1);

        lastClipIndex = index;
        return clips[index];
    }
}