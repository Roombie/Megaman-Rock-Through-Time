using UnityEngine;

/// <summary>
/// Simulates sound occlusion by applying a low-pass filter to an AudioSource
/// when a wall or obstacle blocks the direct line between this object and
/// the AudioListener. Sounds behind walls become muffled rather than simply
/// attenuating by distance.
///
/// ── Examples ─────────────────────────────────────────────────────────────
///   Enemy through a wall     → muffled footsteps and voice
///   Music from another room  → low-frequency bleed only
///   Explosion behind cover   → bass thud instead of full blast
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add this component to any world AudioSource (enemy, machine, etc.)
///     that should be muffled when blocked. The AudioSource can be on the
///     same GameObject or a child.
///  2. Assign the occlusionLayers mask to include walls/terrain/obstacles.
///  3. Tune openCutoff (clear LOS) and occludedCutoff (fully blocked).
///
/// ── How it works ─────────────────────────────────────────────────────────
///  Every checkInterval seconds a raycast is fired from this object to the
///  AudioListener. If it hits geometry on the occlusionLayers mask, the
///  AudioLowPassFilter cutoff frequency is smoothly lerped toward the
///  occluded value. When the path is clear it lerps back to the open value.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioOcclusion : MonoBehaviour
{
    [Header("Cutoff Frequencies (Hz)")]
    [Tooltip("Filter cutoff when there is a clear line of sight. " +
             "22000 = fully open / no filtering.")]
    public float openCutoff     = 22000f;

    [Tooltip("Filter cutoff when fully occluded by geometry. " +
             "Lower = more muffled. Typical range: 400–2000.")]
    public float occludedCutoff = 800f;

    [Tooltip("Speed at which the cutoff frequency lerps between open and occluded.")]
    public float smoothSpeed    = 8f;

    [Header("Raycast")]
    [Tooltip("Layers that count as blocking geometry (walls, terrain, etc.).")]
    public LayerMask occlusionLayers = ~0;

    [Tooltip("Seconds between occlusion checks. Lower = more accurate but more CPU.")]
    public float checkInterval = 0.1f;

    // -------------------------------------------------------------------------

    AudioSource      audioSource;
    AudioLowPassFilter lowPassFilter;
    AudioListener    listener;

    float targetCutoff;
    float timer;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // Add or reuse a low-pass filter
        lowPassFilter = GetComponent<AudioLowPassFilter>();
        if (lowPassFilter == null)
            lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();

        lowPassFilter.cutoffFrequency = openCutoff;
        targetCutoff                  = openCutoff;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            CheckOcclusion();
        }

        // Smooth transition between open and occluded cutoff
        lowPassFilter.cutoffFrequency = Mathf.Lerp(
            lowPassFilter.cutoffFrequency,
            targetCutoff,
            Time.deltaTime * smoothSpeed
        );
    }

    void CheckOcclusion()
    {
        if (listener == null)
            listener = FindFirstObjectByType<AudioListener>();

        if (listener == null) return;

        Vector3 origin      = transform.position;
        Vector3 destination = listener.transform.position;
        Vector3 direction   = destination - origin;
        float   distance    = direction.magnitude;

        bool occluded = Physics.Raycast(origin, direction.normalized, distance, occlusionLayers);
        targetCutoff  = occluded ? occludedCutoff : openCutoff;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        AudioListener l = FindFirstObjectByType<AudioListener>();
        if (l == null) return;

        Gizmos.color = targetCutoff < openCutoff * 0.9f
            ? new Color(1f, 0.3f, 0.3f, 0.8f)
            : new Color(0.3f, 1f, 0.3f, 0.8f);

        Gizmos.DrawLine(transform.position, l.transform.position);
    }
#endif
}