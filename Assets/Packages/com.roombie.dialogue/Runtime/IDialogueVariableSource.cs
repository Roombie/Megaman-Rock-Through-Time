using System;
using System.Globalization;

/// <summary>
/// Supplies values for {name} placeholders in dialogue text. Implement it on a MonoBehaviour and
/// assign it to DialogueWindow to expose game state (player name, currency, quest counters) without
/// the dialogue packages referencing the game.
/// </summary>
public interface IDialogueVariableSource
{
    bool TryGetVariable(string name, out string value);
}

/// <summary>
/// Optional companion to IDialogueVariableSource. Raised with the variable name after a value changes
/// so an open DialogueWindow can refresh the text it is showing.
/// </summary>
public interface IDialogueVariableNotifier
{
    event Action<string> Changed;
}

/// <summary>
/// Asks each source in order and returns the first value found. Sources are resolved lazily so scene
/// singletons and the active DialogueContext can be looked up at bind time.
/// </summary>
public sealed class CompositeDialogueVariableSource : IDialogueVariableSource
{
    private readonly Func<IDialogueVariableSource>[] sources;

    public CompositeDialogueVariableSource(params Func<IDialogueVariableSource>[] sources)
    {
        this.sources = sources ?? Array.Empty<Func<IDialogueVariableSource>>();
    }

    public bool TryGetVariable(string name, out string value)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            IDialogueVariableSource source = sources[i]?.Invoke();
            if (source != null && !ReferenceEquals(source, this) && source.TryGetVariable(name, out value))
                return true;
        }

        value = null;
        return false;
    }
}

/// <summary>Formats arbitrary values for display in dialogue text using the invariant culture.</summary>
public static class DialogueVariableText
{
    public static string From(object value) => value switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
