using System;
using UnityEngine;
using UnityEngine.Localization;

[DisallowMultipleComponent]
public sealed class UnityDialogueTextResolver : MonoBehaviour, IDialogueTextResolver
{
    [SerializeField] private string defaultTable = DialogueLocalization.TableName;

    public IDialogueTextBinding Bind(string tableName, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        string table = string.IsNullOrWhiteSpace(tableName) ? defaultTable : tableName;
        return new Binding(DialogueLocalization.CreateLocalizedString(table, key));
    }

    private sealed class Binding : IDialogueTextBinding
    {
        private LocalizedString localized;
        public string Value { get; private set; }
        public event Action<string> Changed;

        public Binding(LocalizedString value)
        {
            localized = value;
            Value = localized != null ? localized.GetLocalizedString() : string.Empty;
            if (localized != null) localized.StringChanged += HandleChanged;
        }

        private void HandleChanged(string value)
        {
            Value = value ?? string.Empty;
            Changed?.Invoke(Value);
        }

        public void Dispose()
        {
            if (localized != null) localized.StringChanged -= HandleChanged;
            localized = null;
            Changed = null;
        }
    }
}
