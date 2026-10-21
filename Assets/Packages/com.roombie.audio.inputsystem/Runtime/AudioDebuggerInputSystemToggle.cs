#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// AnimalWood/new-Input-System bridge for the reusable Roombie Audio debugger.
/// Keeps com.roombie.audio independent from Unity.InputSystem.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioDebugger))]
public sealed class AudioDebuggerInputSystemToggle : MonoBehaviour
{
    [SerializeField] private AudioDebugger audioDebugger;
    [SerializeField] private Key toggleKey = Key.F9;

    private void Awake()
    {
        if (audioDebugger == null)
            audioDebugger = GetComponent<AudioDebugger>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || audioDebugger == null)
            return;

        if (keyboard[toggleKey].wasPressedThisFrame)
            audioDebugger.ToggleVisible();
    }
}
#endif
