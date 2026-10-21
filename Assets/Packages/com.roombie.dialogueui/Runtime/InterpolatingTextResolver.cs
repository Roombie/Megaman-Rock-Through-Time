using System;

/// <summary>
/// Wraps another IDialogueTextResolver and runs DialogueTextInterpolator over every binding, so
/// {name} placeholders resolve after localization and again whenever the localized text changes.
/// When no inner resolver is assigned the key itself is treated as literal text.
/// </summary>
public sealed class InterpolatingTextResolver : IDialogueTextResolver
{
    private readonly IDialogueTextResolver inner;
    private readonly IDialogueVariableSource variables;

    public InterpolatingTextResolver(IDialogueTextResolver inner, IDialogueVariableSource variables)
    {
        this.inner = inner;
        this.variables = variables;
    }

    public IDialogueTextBinding Bind(string tableName, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        IDialogueTextBinding source = inner?.Bind(tableName, key);
        return new Binding(source, key, variables);
    }

    private sealed class Binding : IDialogueRefreshableBinding
    {
        private readonly IDialogueVariableSource variables;
        private readonly string literal;
        private IDialogueTextBinding source;

        public string Value { get; private set; }
        public event Action<string> Changed;

        public Binding(IDialogueTextBinding source, string literal, IDialogueVariableSource variables)
        {
            this.source = source;
            this.literal = literal ?? string.Empty;
            this.variables = variables;
            Value = DialogueTextInterpolator.Format(source != null ? source.Value : this.literal, variables);
            if (source != null) source.Changed += HandleChanged;
        }

        public bool Refresh()
        {
            string next = DialogueTextInterpolator.Format(source != null ? source.Value : literal, variables);
            if (string.Equals(next, Value, StringComparison.Ordinal)) return false;
            Value = next;
            return true;
        }

        private void HandleChanged(string value)
        {
            Value = DialogueTextInterpolator.Format(value, variables);
            Changed?.Invoke(Value);
        }

        public void Dispose()
        {
            if (source != null)
            {
                source.Changed -= HandleChanged;
                source.Dispose();
            }
            source = null;
            Changed = null;
        }
    }
}
