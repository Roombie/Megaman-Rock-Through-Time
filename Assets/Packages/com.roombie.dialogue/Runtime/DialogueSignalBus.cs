using System;

public readonly struct DialogueSignalContext
{
    public readonly string SignalId;
    public readonly string Payload;
    public readonly DialogueContext DialogueContext;

    public DialogueSignalContext(string signalId, string payload, DialogueContext dialogueContext)
    {
        SignalId = signalId ?? string.Empty;
        Payload = payload ?? string.Empty;
        DialogueContext = dialogueContext;
    }
}

public static class DialogueSignalBus
{
    public static event Action<DialogueSignalContext> Raised;

    public static void Raise(string signalId, string payload, DialogueContext context)
    {
        if (string.IsNullOrWhiteSpace(signalId))
            return;

        Raised?.Invoke(new DialogueSignalContext(signalId.Trim(), payload, context));
    }
}
