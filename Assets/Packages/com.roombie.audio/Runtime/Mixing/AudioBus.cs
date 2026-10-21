/// <summary>
/// Logical output buses controlled by the reusable audio service.
/// These are intentionally independent from any game's settings model.
/// </summary>
public enum AudioBus
{
    Master = 0,
    SFX = 1,
    BGM = 2,
    UI = 3,
    Voice = 4,
}
