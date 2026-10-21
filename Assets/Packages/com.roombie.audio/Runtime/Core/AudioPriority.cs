/// <summary>
/// Named priority levels for AudioEvent voices.
/// When the voice limit is reached, the lowest-priority active sound is
/// evicted to make room for the incoming sound.
///
/// Recommended assignments:
///   Critical  → player death sting, boss roar, UI confirmation
///   High      → player weapon fire, player footstep, key UI sounds
///   Medium    → enemy sounds, world impacts, general SFX
///   Low       → ambient one-shots, distant sounds, background rustles
/// </summary>
public enum AudioPriority
{
    Low      = 10,
    Medium   = 50,
    High     = 80,
    Critical = 100,
}