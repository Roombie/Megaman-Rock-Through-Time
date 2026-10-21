using System.Collections;
using UnityEngine;

/// <summary>
/// Periodically fires distant, directional sounds at the edge of the
/// player's audio range, making the world feel much larger than it is.
///
/// Unlike AmbientAudioPlayer (which scatters sounds in a tight radius),
/// this component places sounds at a configurable distance — far enough
/// that the player hears them as background world events.
///
/// ── Examples ─────────────────────────────────────────────────────────────
///   Mountains   → distant thunder, avalanche rumble, eagle cry
///   Battlefield → far artillery, distant gunfire, explosion echo
///   Village     → church bell, dog barking, crowd murmur
///   Forest      → wolf howl, owl hoot, distant waterfall
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Place this on any GameObject (e.g. the AudioManager prefab or a
///     dedicated "World Ambience" object).
///  2. Assign AudioEvents in the sounds array. Each event should have:
///       spatialBlend  = 1  (fully 3D)
///       maxDistance   ≥ distanceFromListener  (so the sound is audible)
///       volume        low-ish (0.3–0.6) for distant feel
///  3. Tune distanceFromListener, minDelay / maxDelay to taste.
/// </summary>
public class DistantSoundPlayer : MonoBehaviour
{
    [Header("Sounds")]
    public AudioEvent[] sounds;

    [Header("Timing")]
    public float minDelay = 15f;
    public float maxDelay = 45f;

    [Header("Distance")]
    [Tooltip("How far from the AudioListener the sound is placed (metres).")]
    public float distanceFromListener = 60f;

    [Tooltip("If true, the spawned position is kept at the same Y as the listener " +
             "(suitable for flat worlds / 2.5D games).")]
    public bool lockYAxis = true;

    [Header("Listener Reference")]
    [Tooltip("Leave null to auto-find the AudioListener.")]
    public Transform listenerTransform;

    // -------------------------------------------------------------------------

    void OnEnable()  => StartCoroutine(DistantLoop());
    void OnDisable() => StopAllCoroutines();

    IEnumerator DistantLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            if (sounds == null || sounds.Length == 0) continue;

            AudioEvent evt = sounds[Random.Range(0, sounds.Length)];
            if (evt == null) continue;

            Vector3 centre    = GetListenerPosition();
            Vector3 direction = Random.onUnitSphere;

            if (lockYAxis) { direction.y = 0f; direction.Normalize(); }

            Vector3 spawnPos = centre + direction * distanceFromListener;

            Audio.PlayAtPosition(evt, spawnPos);
        }
    }

    Vector3 GetListenerPosition()
    {
        if (listenerTransform != null) return listenerTransform.position;

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener != null)
        {
            listenerTransform = listener.transform;
            return listener.transform.position;
        }

        return transform.position;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.15f);
        Gizmos.DrawSphere(transform.position, distanceFromListener);
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, distanceFromListener);
    }
#endif
}
