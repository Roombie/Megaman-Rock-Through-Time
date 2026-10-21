/// <summary>Helpers for AudioHandles returned by Audio.Play(AudioEvent).</summary>
public static class AudioEventExtensions
{
    /// <summary>
    /// True while the handle is still playing one of the event's clips. Pooled sources are reused, so an old
    /// handle can point at a source that now plays something else: check this before stopping it.
    /// </summary>
    public static bool IsPlayingEvent(this AudioHandle handle, AudioEvent audioEvent)
    {
        if (!handle.IsPlaying || audioEvent == null || audioEvent.clips == null) return false;
        return System.Array.IndexOf(audioEvent.clips, handle.Clip) >= 0;
    }
}
