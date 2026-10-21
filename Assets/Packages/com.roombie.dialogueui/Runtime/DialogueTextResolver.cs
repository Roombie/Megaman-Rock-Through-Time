using System;

public interface IDialogueTextBinding : IDisposable
{
    string Value { get; }
    event Action<string> Changed;
}

public interface IDialogueTextResolver
{
    IDialogueTextBinding Bind(string tableName, string key);
}

/// <summary>A binding whose value depends on external state, such as {name} dialogue variables, and can be re-evaluated on demand.</summary>
public interface IDialogueRefreshableBinding : IDialogueTextBinding
{
    /// <summary>Re-evaluates Value and returns true when it changed. Does not raise Changed, so the caller decides how to present the update.</summary>
    bool Refresh();
}
