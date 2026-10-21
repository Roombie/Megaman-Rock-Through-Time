using System.Collections;
using UnityEngine;

/// <summary>
/// Periodically fires random ambient one-shot AudioEvents at randomised
/// positions around the player, making environments feel alive without
/// looping a single obvious audio file.
///
/// ── Examples ─────────────────────────────────────────────────────────────
///   Forest  → bird chirps, leaf rustles, twig snaps, wind gusts
///   Cave    → water drips, distant echo, rock creaks
///   City    → distant traffic, crowd murmur, dog barks, church bell
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add this component to a persistent "Ambience" GameObject in your scene
///     (or on your AudioManager prefab).
///  2. Assign one or more AudioEvents in the sounds array.
///  3. Each AudioEvent should have spatialBlend = 1 and appropriate
///     minDistance / maxDistance set so sounds feel distant.
///  4. Set scatter radius, min/max delay, and optionally point this
///     component at your player transform via listenerTransform.
/// </summary>
public class AmbientAudioPlayer : MonoBehaviour
{
    [Header("Sounds")]
    [Tooltip("Pool of AudioEvents to randomly choose from each interval.")]
    public AudioEvent[] sounds;

    [Header("Timing")]
    [Tooltip("Minimum seconds between ambient sound triggers.")]
    public float minDelay = 5f;

    [Tooltip("Maximum seconds between ambient sound triggers.")]
    public float maxDelay = 20f;

    [Header("Scatter")]
    [Tooltip("Radius around the listener within which sounds are placed.")]
    public float scatterRadius = 20f;

    [Tooltip("If true, the Y position of spawned sounds is locked to this " +
             "transform's Y (good for top-down or flat environments).")]
    public bool lockYAxis = false;

    [Header("Listener Reference")]
    [Tooltip("Transform used as the centre of the scatter sphere. " +
             "Leave null to auto-find the AudioListener each interval.")]
    public Transform listenerTransform;

    // -------------------------------------------------------------------------

    void OnEnable()  => StartCoroutine(AmbientLoop());
    void OnDisable() => StopAllCoroutines();

    IEnumerator AmbientLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            if (sounds == null || sounds.Length == 0) continue;

            AudioEvent evt = sounds[Random.Range(0, sounds.Length)];
            if (evt == null) continue;

            Vector3 centre = GetListenerPosition();
            Vector3 offset = Random.insideUnitSphere * scatterRadius;

            if (lockYAxis) offset.y = 0f;

            Vector3 spawnPos = centre + offset;

            // Use PlayAtPosition so the sound feels directional and distant
            Audio.PlayAtPosition(evt, spawnPos);
        }
    }

    Vector3 GetListenerPosition()
    {
        if (listenerTransform != null) return listenerTransform.position;

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener != null)
        {
            listenerTransform = listener.transform;   // cache for next time
            return listener.transform.position;
        }

        return transform.position;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        Gizmos.DrawSphere(transform.position, scatterRadius);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, scatterRadius);
    }
#endif
}
