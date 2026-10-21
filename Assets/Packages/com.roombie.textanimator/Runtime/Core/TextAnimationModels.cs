using System;
using System.Collections.Generic;
using System.Globalization;

public enum TextAnimationMarkerType
{
    Pause,
    Event
}

/// <summary>A parsed inline-tag range expressed in TMP character indices.</summary>
public sealed class TextAnimationSpan
{
    private readonly Dictionary<string, string> parameters;

    public string Tag { get; }
    public int StartIndex { get; }
    public int EndIndexExclusive { get; }
    public int Length => Math.Max(0, EndIndexExclusive - StartIndex);
    public IReadOnlyDictionary<string, string> Parameters => parameters;

    public TextAnimationSpan(string tag, int startIndex, int endIndexExclusive, Dictionary<string, string> values)
    {
        Tag = (tag ?? string.Empty).Trim().ToLowerInvariant();
        StartIndex = Math.Max(0, startIndex);
        EndIndexExclusive = Math.Max(StartIndex, endIndexExclusive);
        parameters = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool Contains(int characterIndex) =>
        characterIndex >= StartIndex && characterIndex < EndIndexExclusive;

    public bool TryGet(string key, out string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = null;
            return false;
        }
        return parameters.TryGetValue(key, out value);
    }

    public string GetString(string key, string fallback = "") =>
        TryGet(key, out string value) ? value : fallback;

    public float GetFloat(string key, float fallback)
    {
        if (!TryGet(key, out string value)) return fallback;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
            ? result
            : fallback;
    }

    public float GetFloatAny(float fallback, params string[] keys)
    {
        if (keys != null)
        {
            foreach (string key in keys)
                if (TryGet(key, out string value) &&
                    float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                    return result;
        }

        if (TryGet("value", out string shorthand) &&
            float.TryParse(shorthand, NumberStyles.Float, CultureInfo.InvariantCulture, out float shortResult))
            return shortResult;

        return fallback;
    }
}

public sealed class TextAnimationMarker
{
    public TextAnimationMarkerType Type { get; }
    public int CharacterIndex { get; }
    public float NumberValue { get; }
    public string StringValue { get; }

    public TextAnimationMarker(TextAnimationMarkerType type, int characterIndex, float numberValue, string stringValue)
    {
        Type = type;
        CharacterIndex = Math.Max(0, characterIndex);
        NumberValue = numberValue;
        StringValue = stringValue ?? string.Empty;
    }
}

public sealed class TextAnimationDocument
{
    public string SourceText { get; }
    public string RenderText { get; }
    public int CharacterCount { get; }
    public IReadOnlyList<TextAnimationSpan> Spans { get; }
    public IReadOnlyList<TextAnimationMarker> Markers { get; }

    public TextAnimationDocument(
        string sourceText,
        string renderText,
        int characterCount,
        List<TextAnimationSpan> spans,
        List<TextAnimationMarker> markers)
    {
        SourceText = sourceText ?? string.Empty;
        RenderText = renderText ?? string.Empty;
        CharacterCount = Math.Max(0, characterCount);
        Spans = spans ?? new List<TextAnimationSpan>();
        Markers = markers ?? new List<TextAnimationMarker>();
    }
}
