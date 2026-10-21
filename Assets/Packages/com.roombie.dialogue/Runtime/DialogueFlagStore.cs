using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight runtime flag source for Condition nodes. Keep one under SYSTEMS for simple dialogue
/// flags. Long-term story/quest flags can replace this through DialogueRunner.OnCheckCondition.
/// </summary>
public sealed class DialogueFlagStore : MonoBehaviour
{
    [Serializable]
    private sealed class Entry
    {
        public string key;
        public bool value;
    }

    public static DialogueFlagStore Instance { get; private set; }

    [SerializeField] private List<Entry> initialFlags = new();

    private readonly Dictionary<string, bool> values =
        new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[DialogueFlagStore] More than one DialogueFlagStore exists in the scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        values.Clear();
        for (int i = 0; i < initialFlags.Count; i++)
        {
            Entry entry = initialFlags[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            values[entry.key.Trim()] = entry.value;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool GetFlag(string key)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               values.TryGetValue(key.Trim(), out bool value) &&
               value;
    }

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        values[key.Trim()] = value;
    }

    public void SetTrue(string key) => SetFlag(key, true);
    public void SetFalse(string key) => SetFlag(key, false);

    public void Toggle(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        string normalized = key.Trim();
        SetFlag(normalized, !GetFlag(normalized));
    }
}
