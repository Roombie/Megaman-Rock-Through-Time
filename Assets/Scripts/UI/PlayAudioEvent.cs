using UnityEngine;

/// <summary>
/// Plays an AudioEvent through the audio system. Hook Play() to a UnityEvent (a button's On Click, an
/// animation event, a trigger...) or tick "Play On Enable" to play it when the object spawns,
/// instead of putting an AudioSource on the object.
/// </summary>
public class PlayAudioEvent : MonoBehaviour
{
    [SerializeField] private AudioEvent audioEvent;
    [Tooltip("Play when the object is enabled (e.g. an effect prefab when it's spawned).")]
    [SerializeField] private bool playOnEnable;

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    public void Play() => Audio.Play(audioEvent);
}
