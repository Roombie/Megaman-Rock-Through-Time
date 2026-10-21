using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Attach to any trigger collider to change the AudioMixer reverb snapshot
/// when the player (or any tagged object) enters or exits the zone.
///
/// ── Typical use cases ────────────────────────────────────────────────────
///   Cave entrance   → long reverb snapshot
///   Underwater zone → muffled low-pass snapshot
///   Cathedral       → large hall reverb snapshot
///   Open field      → dry / no-reverb snapshot
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add a Collider to this GameObject and tick "Is Trigger".
///  2. Create AudioMixer Snapshots for each environment in your mixer.
///  3. Assign the "inside" snapshot to enterSnapshot and the "outside"
///     snapshot to exitSnapshot (can be null if you only need enter).
///  4. Set triggerTag to the tag on your player GameObject (default "Player").
///
/// ── Optional ambient event ───────────────────────────────────────────────
///  Assign ambientLoop to play a looping AudioEvent when inside the zone
///  (e.g. dripping water in a cave). It is stopped on exit.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AudioZone : MonoBehaviour
{
    [Header("Snapshots")]
    [Tooltip("AudioMixer snapshot to transition to when the trigger is entered.")]
    public AudioMixerSnapshot enterSnapshot;

    [Tooltip("AudioMixer snapshot to revert to when the trigger is exited. " +
             "Leave null to not change anything on exit.")]
    public AudioMixerSnapshot exitSnapshot;

    [Tooltip("Seconds to blend into each snapshot.")]
    public float transitionTime = 1f;

    [Header("Trigger")]
    [Tooltip("Only objects with this tag will activate the zone.")]
    public string triggerTag = "Player";

    [Header("Optional Ambient Loop")]
    [Tooltip("AudioEvent played (looping) while the player is inside the zone. " +
             "Stopped automatically on exit.")]
    public AudioEvent ambientLoop;

    AudioHandle ambientHandle;

#if UNITY_EDITOR
    void OnValidate()
    {
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        for (int i = 0; i < tags.Length; i++)
            if (tags[i] == triggerTag) return;

        Debug.LogWarning($"AudioZone: tag '{triggerTag}' does not exist.", this);
    }
#endif

    // -------------------------------------------------------------------------

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(triggerTag)) return;

        enterSnapshot?.TransitionTo(transitionTime);

        if (ambientLoop != null)
            ambientHandle = Audio.Play(ambientLoop);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(triggerTag)) return;

        exitSnapshot?.TransitionTo(transitionTime);

        ambientHandle.Stop();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        Collider col = GetComponent<Collider>();

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.position, sphere.radius * transform.lossyScale.x);
        }
    }
#endif
}