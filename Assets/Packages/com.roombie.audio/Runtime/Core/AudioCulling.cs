using UnityEngine;

/// <summary>
/// Disables an AudioSource when the AudioListener is beyond a set distance,
/// and re-enables it when the listener moves back within range.
///
/// Unity still processes every enabled AudioSource every frame even if the
/// player can't hear it. This component eliminates that overhead for
/// persistent world sources (machines, waterfalls, ambient loops) that
/// exist throughout the entire scene lifetime.
///
/// ── Difference from PlayAtPosition distance culling ───────────────────────
///   PlayAtPosition culling skips one-shot sounds at fire time.
///   AudioCulling continuously manages persistent/looping sources on
///   objects that are always in the scene.
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add to any GameObject that has an AudioSource set to loop.
///  2. Set cullDistance to a reasonable range (should be ≥ the AudioSource's
///     maxDistance so the transition is inaudible).
///  3. AudioSource.enabled is toggled automatically in Update.
///
/// ── Performance note ─────────────────────────────────────────────────────
///  The distance check runs every checkInterval seconds (not every frame)
///  to keep the overhead negligible even with many culled sources.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioCulling : MonoBehaviour
{
    [Tooltip("Sources beyond this distance from the AudioListener are disabled.")]
    public float cullDistance = 50f;

    [Tooltip("Seconds between distance checks. Lower = more responsive but more CPU.")]
    public float checkInterval = 0.2f;

    // -------------------------------------------------------------------------

    AudioSource   source;
    AudioListener listener;
    float         timer;

    void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < checkInterval) return;
        timer = 0f;

        if (listener == null)
        {
            listener = FindFirstObjectByType<AudioListener>();
            if (listener == null) return;
        }

        float dist          = Vector3.Distance(transform.position, listener.transform.position);
        bool  shouldEnable  = dist <= cullDistance;

        if (source.enabled != shouldEnable)
        {
            source.enabled = shouldEnable;

            // Resume loop from where it was paused rather than restarting
            if (shouldEnable && source.loop && !source.isPlaying)
                source.Play();
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.2f);
        Gizmos.DrawSphere(transform.position, cullDistance);
        Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, cullDistance);
    }
#endif
}