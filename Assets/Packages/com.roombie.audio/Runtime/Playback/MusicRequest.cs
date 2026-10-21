using UnityEngine;

public enum MusicOrder
{
    /// <summary>Clips play in the order they appear in the AudioEvent.</summary>
    Sequential,
    /// <summary>Random order, reshuffled every full cycle, never the same track twice in a row.</summary>
    Shuffle
}

/// <summary>
/// Named layers instead of magic numbers. Higher layer wins; within the same layer the most
/// recently requested one wins. Values are spaced so a layer can be inserted later without
/// renumbering (serialized as int: an old priority of 10 reads as Menu).
/// </summary>
public enum MusicPriority
{
    /// <summary>Base music of a scene: main menu theme, gameplay playlist.</summary>
    Scene = 0,
    /// <summary>Menus and shops opened over gameplay.</summary>
    Menu = 10,
    /// <summary>Things that interrupt menus: pause screen, popups with their own theme.</summary>
    Overlay = 20,
    /// <summary>Cutscenes, jingles, anything that must win over everything.</summary>
    Cutscene = 30
}

public enum MusicActivation
{
    /// <summary>Requested while this GameObject is active. Put it on a panel that SetActive toggles.</summary>
    WhileEnabled,
    /// <summary>Requested only between Request() and Release(). For menus shown with a
    /// CanvasGroup/animation instead of SetActive: wire those calls to the open/close events.</summary>
    Manual
}

/// <summary>
/// Declares "this music should play". The MusicDirector plays the highest-priority active
/// request; when it goes away, the previous music comes back at the second it was left.
///
/// ── Typical setup ────────────────────────────────────────────────────────
///   Main menu scene     → music: menu theme,  priority: Scene
///   Gameplay scene      → music: Playlist,    priority: Scene, order: Shuffle
///   Guild panel         → music: guild theme, priority: Menu, fade: 0.3
///   Cutscene            → music: none,        priority: Cutscene  (= fade to silence)
///
/// The AudioEvent is only a track list: its clips are the tracks. Its pitch/volume
/// randomization, cooldown and loop flag are ignored on purpose (a detuned song sounds broken).
/// One clip = that track loops seamlessly. Several clips = playlist. No event = silence.
/// </summary>
[DisallowMultipleComponent]
public sealed class MusicRequest : MonoBehaviour
{
    [Tooltip("BGM AudioEvent. One clip loops; several clips form a playlist. " +
             "Leave empty to request silence (music fades out while this is active).")]
    public AudioEvent music;

    [Tooltip("Only used when the event has several clips.")]
    public MusicOrder order = MusicOrder.Sequential;

    public MusicPriority priority = MusicPriority.Scene;

    public MusicActivation activation = MusicActivation.WhileEnabled;

    [Tooltip("Crossfade length when this music takes over. -1 = MusicDirector's default. " +
             "Menus feel better with a short fade (0.2–0.4 s).")]
    public float fadeSeconds = -1f;

    [Tooltip("When this music becomes active again, continue the same track at the same " +
             "second instead of starting over.")]
    public bool resumeWhereLeftOff = true;

    [Range(0f, 1f)]
    public float volume = 1f;

    bool manualRequested;

    /// <summary>True while this request is competing for the music.</summary>
    public bool IsRequested => isActiveAndEnabled &&
                               (activation == MusicActivation.WhileEnabled || manualRequested);

    /// <summary>Manual activation: start requesting this music (e.g. menu open event).</summary>
    public void Request()
    {
        manualRequested = true;
        MusicDirector.Add(this);   // re-adding moves it to "most recent" within its layer
    }

    /// <summary>Manual activation: stop requesting this music (e.g. menu close event).</summary>
    public void Release()
    {
        manualRequested = false;
        if (activation == MusicActivation.Manual) MusicDirector.Remove(this);
    }

    void OnEnable()
    {
        if (activation == MusicActivation.WhileEnabled) MusicDirector.Add(this);
    }

    void OnDisable()
    {
        manualRequested = false;
        MusicDirector.Remove(this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (music == null) return;

        if (music.category != SoundCategory.BGM)
            Debug.LogWarning($"[MusicRequest] '{music.name}' is not in the BGM category. The " +
                             "director plays it as BGM anyway, but other code using this event " +
                             "won't.", this);

        if (music.clips == null || music.clips.Length == 0)
            Debug.LogWarning($"[MusicRequest] '{music.name}' has no clips; it will play silence.", this);
    }
#endif
}