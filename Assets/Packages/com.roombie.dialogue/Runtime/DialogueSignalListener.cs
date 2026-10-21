using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class DialogueStringEvent : UnityEvent<string> { }

public sealed class DialogueSignalListener : MonoBehaviour
{
    [Serializable]
    private sealed class Entry
    {
        public string signalId;
        public DialogueStringEvent onRaised = new();
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    private void OnEnable() => DialogueSignalBus.Raised += HandleSignal;
    private void OnDisable() => DialogueSignalBus.Raised -= HandleSignal;

    private void HandleSignal(DialogueSignalContext context)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (entry == null ||
                !string.Equals(entry.signalId, context.SignalId, StringComparison.OrdinalIgnoreCase))
                continue;

            entry.onRaised?.Invoke(context.Payload);
        }
    }
}
