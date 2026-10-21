using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class TextTagParameters
{
    private static readonly IReadOnlyDictionary<string, string> EmptyValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, string> values;

    public static TextTagParameters Empty { get; } = new TextTagParameters(null);

    public TextTagParameters(IDictionary<string, string> values)
    {
        if (values == null || values.Count == 0)
        {
            this.values = EmptyValues;
            return;
        }

        this.values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public bool Contains(string key) =>
        !string.IsNullOrWhiteSpace(key) && values.ContainsKey(key);

    public bool TryGet(string key, out string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = null;
            return false;
        }

        return values.TryGetValue(key, out value);
    }

    public string Get(string key, string fallback = "") => GetString(key, fallback);

    public string GetString(string key, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(key))
            return fallback;

        return values.TryGetValue(key, out string value) ? value : fallback;
    }

    public float GetFloat(string key, float fallback)
    {
        string raw = GetString(key, null);
        return raw != null && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    public float GetValueFloat(float fallback) => GetFloat("value", fallback);

    public int GetInt(string key, int fallback)
    {
        string raw = GetString(key, null);
        return raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    public bool GetBool(string key, bool fallback)
    {
        string raw = GetString(key, null);
        if (raw == null)
            return fallback;

        if (bool.TryParse(raw, out bool value))
            return value;

        if (raw == "1") return true;
        if (raw == "0") return false;
        return fallback;
    }
}
