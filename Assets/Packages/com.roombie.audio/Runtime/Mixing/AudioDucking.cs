using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Automatically lowers a target AudioMixer group's volume (ducking) when a
/// specific AudioEvent plays, then restores it afterward.
///
/// ── Classic use cases ────────────────────────────────────────────────────
///   Dialogue starts   → Music volume drops by -10 dB
///   Boss ability fires → All SFX duck briefly to highlight the sound
///   Cinematic plays   → BGM fades under narration
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add this component anywhere in the scene (e.g. on AudioManager).
///  2. Set targetGroup to the mixer group to duck (e.g. BGM or SFX).
///  3. Set targetParam to the exposed parameter name on that group.
///  4. Configure duckAmount (negative dB), fadeInTime, holdTime, fadeOutTime.
///  5. Call Duck() from code when the trigger sound starts, or wire it to
///     an AudioEmitter / UnityEvent in the Inspector.
///
/// ── AudioMixer setup ─────────────────────────────────────────────────────
///  In your AudioMixer, right-click the volume of the group you want to duck
///  and choose "Expose to script". Name the parameter (e.g. "BGMDuck").
///  Use that exact name in targetParam below.
/// </summary>
public class AudioDucking : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The AudioMixer to apply ducking to.")]
    public AudioMixer targetMixer;

    [Tooltip("The exposed parameter name on the mixer (e.g. 'BGMDuck').")]
    public string targetParam = "BGMDuck";

    [Header("Duck Settings")]
    [Tooltip("How many dB to reduce the target group. Negative value (e.g. -10).")]
    public float duckAmount   = -10f;

    [Tooltip("Seconds to fade the volume down.")]
    public float fadeInTime   = 0.1f;

    [Tooltip("Seconds to hold the ducked volume before restoring.")]
    public float holdTime     = 0.5f;

    [Tooltip("Seconds to fade the volume back to normal.")]
    public float fadeOutTime  = 0.5f;

    // -------------------------------------------------------------------------

    float    baseVolume = 0f;   // 0 dB = normal
    Coroutine duckRoutine;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (targetMixer == null || string.IsNullOrWhiteSpace(targetParam)) return;

        if (!targetMixer.GetFloat(targetParam, out _))
        {
            Debug.LogWarning(
                $"AudioDucking: mixer '{targetMixer.name}' has no exposed parameter " +
                $"named '{targetParam}'.", this);
        }
    }
#endif

    void Awake()
    {
        if (targetMixer != null)
            targetMixer.GetFloat(targetParam, out baseVolume);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Triggers a duck cycle: fade down → hold → fade back up.
    /// Safe to call while a duck is already in progress — it restarts.
    /// </summary>
    public void Duck()
    {
        if (targetMixer == null)
        {
            Debug.LogWarning("[AudioDucking] targetMixer is not assigned.", this);
            return;
        }

        if (duckRoutine != null)
            StopCoroutine(duckRoutine);

        duckRoutine = StartCoroutine(DuckRoutine());
    }

    /// <summary>Immediately restores the volume without waiting for holdTime.</summary>
    public void Release()
    {
        if (duckRoutine != null) StopCoroutine(duckRoutine);
        duckRoutine = StartCoroutine(FadeRoutine(GetCurrentDb(), baseVolume, fadeOutTime));
    }

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    IEnumerator DuckRoutine()
    {
        // Fade down
        yield return FadeRoutine(GetCurrentDb(), baseVolume + duckAmount, fadeInTime);

        // Hold
        yield return new WaitForSeconds(holdTime);

        // Fade back up
        yield return FadeRoutine(GetCurrentDb(), baseVolume, fadeOutTime);

        duckRoutine = null;
    }

    IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float db = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            targetMixer.SetFloat(targetParam, db);
            yield return null;
        }

        targetMixer.SetFloat(targetParam, to);
    }

    float GetCurrentDb()
    {
        targetMixer.GetFloat(targetParam, out float db);
        return db;
    }
}