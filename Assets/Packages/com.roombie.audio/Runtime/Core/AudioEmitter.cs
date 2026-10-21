using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to any world object that produces sound (enemy, door, machine, etc.)
/// so the object owns and triggers its audio without calling AudioManager
/// directly from gameplay scripts.
///
/// ── Why use this? ────────────────────────────────────────────────────────
///  Keeps gameplay scripts clean. Instead of:
///      Audio.Play(explosionEvent);
///  the object just has:
///      emitter.Play();
///
///  The emitter handles 2D vs 3D automatically based on the event's
///  spatialBlend setting — no configuration needed in the caller.
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add AudioEmitter to any GameObject.
///  2. Assign an AudioEvent to the audioEvent field.
///  3. Call emitter.Play() from your script, or wire it to a UnityEvent
///     in the Inspector (e.g. on a Button onClick).
///
/// ── Multiple sounds per object ───────────────────────────────────────────
///  Add several AudioEmitter components to the same GameObject, one per
///  logical sound (footstep, voice, weapon hit, etc.).
/// </summary>
public class AudioEmitter : MonoBehaviour
{
    [Tooltip("The AudioEvent this emitter will play.")]
    public AudioEvent audioEvent;

    [Tooltip("If true, Play() is called automatically when this component enables.")]
    public bool playOnEnable = false;

    [Tooltip("If true, the current sound is stopped when this component disables.")]
    public bool stopOnDisable = true;

    AudioHandle currentHandle;
    Coroutine pendingPlayOnEnable;

    // -------------------------------------------------------------------------
    // Unity messages
    // -------------------------------------------------------------------------

    void OnEnable()
    {
        if (!playOnEnable) return;

        if (Audio.IsReady)
        {
            Play();
            return;
        }

        // A first-scene emitter can enable before AudioManager has registered the service.
        // Dropping that call made splash/logo sounds fail depending on scene initialization order.
        pendingPlayOnEnable = StartCoroutine(PlayWhenAudioIsReady());
    }

    void OnDisable()
    {
        if (pendingPlayOnEnable != null)
        {
            StopCoroutine(pendingPlayOnEnable);
            pendingPlayOnEnable = null;
        }

        if (stopOnDisable) Stop();
    }

    IEnumerator PlayWhenAudioIsReady()
    {
        while (isActiveAndEnabled && !Audio.IsReady)
            yield return null;

        pendingPlayOnEnable = null;

        if (isActiveAndEnabled && playOnEnable)
            Play();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Plays the assigned AudioEvent.
    /// If the event has spatialBlend > 0, sound is played at this
    /// object's world position for proper 3D attenuation.
    /// Returns an AudioHandle for further per-instance control.
    /// </summary>
    public AudioHandle Play()
    {
        if (audioEvent == null)
            return AudioHandle.Invalid;

        currentHandle = audioEvent.spatialBlend > 0f
            ? Audio.PlayAtPosition(audioEvent, transform.position)
            : Audio.Play(audioEvent);

        return currentHandle;
    }

    /// <summary>Plays a specific AudioEvent (overrides the assigned one for this call).</summary>
    public AudioHandle Play(AudioEvent overrideEvent)
    {
        if (overrideEvent == null)
            return AudioHandle.Invalid;

        currentHandle = overrideEvent.spatialBlend > 0f
            ? Audio.PlayAtPosition(overrideEvent, transform.position)
            : Audio.Play(overrideEvent);

        return currentHandle;
    }

    /// <summary> Plays the assigned AudioEvent. Wire it to UnityEvents </summary>
    /// <remarks> Make sure an AudioEmitter is attached to the same GameObject, or this won't work. </remarks>
    public void PlayAudio()
    {
        if (audioEvent == null)
            return;

        currentHandle = audioEvent.spatialBlend > 0f
            ? Audio.PlayAtPosition(audioEvent, transform.position)
            : Audio.Play(audioEvent);
    }

    /// <summary> Stops the sound that was most recently started by this emitter. </summary>
    /// <remarks> Make sure an AudioEmitter is attached to the same GameObject, or this won't work. </remarks>
    public void Stop() => currentHandle.Stop();

    /// <summary>True if the most recently started sound is still playing.</summary>
    /// <remarks> Make sure an AudioEmitter is attached to the same GameObject, or this won't work. </remarks>
    public bool IsPlaying => currentHandle.IsPlaying;
}