using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight runtime variable source for {name} placeholders in dialogue text. Keep one under
/// SYSTEMS for simple values such as the player name. Systems that already own their data can
/// implement IDialogueVariableSource directly and assign it to DialogueWindow instead.
/// </summary>
public sealed class DialogueVariableStore : MonoBehaviour, IDialogueVariableSource, IDialogueVariableNotifier
{
    [Serializable]
    private sealed class Entry
    {
        public string key;
        public string value;
    }

    public static DialogueVariableStore Instance { get; private set; }

    [SerializeField] private List<Entry> initialVariables = new();

    private readonly Dictionary<string, string> values =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised with the variable name after SetVariable or Remove changes a value.</summary>
    public event Action<string> Changed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[DialogueVariableStore] More than one DialogueVariableStore exists in the scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        values.Clear();
        for (int i = 0; i < initialVariables.Count; i++)
        {
            Entry entry = initialVariables[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            values[entry.key.Trim()] = entry.value ?? string.Empty;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryGetVariable(string name, out string value)
    {
        value = null;
        return !string.IsNullOrWhiteSpace(name) && values.TryGetValue(name.Trim(), out value);
    }

    public string GetVariable(string name, string fallback = "") =>
        TryGetVariable(name, out string value) ? value : fallback;

    public void SetVariable(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        string normalized = name.Trim();
        values[normalized] = value ?? string.Empty;
        Changed?.Invoke(normalized);
    }

    /// <summary>Stores any value formatted with the invariant culture, for example ints and floats.</summary>
    public void SetVariable(string name, object value) => SetVariable(name, DialogueVariableText.From(value));

    public bool Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        string normalized = name.Trim();
        if (!values.Remove(normalized))
            return false;

        Changed?.Invoke(normalized);
        return true;
    }
}
