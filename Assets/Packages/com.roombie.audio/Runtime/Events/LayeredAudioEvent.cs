using UnityEngine;

/// <summary>
/// Plays several AudioEvents simultaneously as a single logical sound,
/// producing richer, more cinematic results than any one clip can achieve.
///
/// ── Examples ─────────────────────────────────────────────────────────────
///   Explosion  = BassBoom + Debris + AirPressure + DistantEcho
///   Gunshot    = MuzzleBlast + MechanicalClick + ShellEject
///   Magic Spell = WooshBase + Sparkle + ImpactThud
///   Boss Roar  = LowGrowl + HighShriek + RoomReverb
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Create a new ScriptableObject: Assets → Create → Audio → Layered Event
///  2. Add 2–5 AudioEvent references to the layers array.
///  3. Call Play() on this asset from code, or
///     drag the asset onto an AudioEmitter component.
///  4. Optional: set a per-layer volume multiplier to balance the mix.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Layered Audio Event")]
public class LayeredAudioEvent : ScriptableObject
{
    [Tooltip("Each layer is an AudioEvent played simultaneously. " +
             "Aim for 2–5 layers — more rarely improves the result.")]
    public AudioEventLayer[] layers;

    /// <summary>
    /// Plays all layers via the AudioManager. Each layer becomes an
    /// independent voice in the pool and benefits from that event's
    /// own randomisation (clip shuffle, pitch/volume variation).
    /// </summary>
    public void Play()
    {
        foreach (var layer in layers)
        {
            if (layer.audioEvent == null) continue;

            // Temporarily scale the event's volume range by the layer multiplier.
            // We restore it immediately after Play() so the asset stays clean.
            Vector2 original = layer.audioEvent.volumeRange;
            layer.audioEvent.volumeRange = original * layer.volumeMultiplier;

            Audio.Play(layer.audioEvent);

            layer.audioEvent.volumeRange = original;
        }
    }

    /// <summary>
    /// Plays all layers at a world position (3D spatial audio).
    /// All layers share the same origin point so they blend correctly.
    /// </summary>
    public void PlayAtPosition(Vector3 position)
    {
        foreach (var layer in layers)
        {
            if (layer.audioEvent == null) continue;

            Vector2 original = layer.audioEvent.volumeRange;
            layer.audioEvent.volumeRange = original * layer.volumeMultiplier;

            Audio.PlayAtPosition(layer.audioEvent, position);

            layer.audioEvent.volumeRange = original;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>One layer inside a LayeredAudioEvent.</summary>
[System.Serializable]
public class AudioEventLayer
{
    [Tooltip("The AudioEvent to play for this layer.")]
    public AudioEvent audioEvent;

    [Tooltip("Volume scale applied on top of the AudioEvent's own volume range. " +
             "Use this to balance layers relative to each other (e.g. 0.6 for a subtle layer).")]
    [Range(0f, 2f)]
    public float volumeMultiplier = 1f;
}
