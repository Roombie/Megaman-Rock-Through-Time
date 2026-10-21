using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Removes Roombie Text Animator tags while preserving ordinary TMP rich-text tags.
/// Parsed ranges use the same logical character indexing TMP uses for normal text.
/// </summary>
public static class TextAnimationTagParser
{
    private sealed class OpenTag
    {
        public string Name;
        public int StartIndex;
        public Dictionary<string, string> Parameters;
    }

    private static readonly HashSet<string> ControlSpanTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "speed", "instant"
    };

    private static readonly HashSet<string> MarkerTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "pause", "wait", "event"
    };

    public static TextAnimationDocument Parse(string source, IEnumerable<string> effectTags)
    {
        source ??= string.Empty;

        var customTags = new HashSet<string>(ControlSpanTags, StringComparer.OrdinalIgnoreCase);
        if (effectTags != null)
        {
            foreach (string tag in effectTags)
                if (!string.IsNullOrWhiteSpace(tag))
                    customTags.Add(tag.Trim());
        }

        var output = new StringBuilder(source.Length);
        var spans = new List<TextAnimationSpan>();
        var markers = new List<TextAnimationMarker>();
        var openTags = new List<OpenTag>();

        int logicalIndex = 0;

        for (int i = 0; i < source.Length;)
        {
            char c = source[i];
            if (c == '<')
            {
                int close = source.IndexOf('>', i + 1);
                if (close >= 0)
                {
                    string raw = source.Substring(i + 1, close - i - 1).Trim();
                    if (TryParseTag(raw, out bool closing, out bool selfClosing, out string name, out Dictionary<string, string> values))
                    {
                        if (customTags.Contains(name))
                        {
                            if (closing)
                                CloseTag(openTags, spans, name, logicalIndex);
                            else
                            {
                                openTags.Add(new OpenTag
                                {
                                    Name = name,
                                    StartIndex = logicalIndex,
                                    Parameters = values
                                });
                                if (selfClosing)
                                    CloseTag(openTags, spans, name, logicalIndex);
                            }
                            i = close + 1;
                            continue;
                        }

                        if (MarkerTags.Contains(name) && !closing)
                        {
                            AddMarker(markers, name, logicalIndex, values);
                            i = close + 1;
                            continue;
                        }

                        // Preserve TMP rich-text tags verbatim.
                        output.Append(source, i, close - i + 1);
                        if (!closing && TmpTagProducesCharacter(name))
                            logicalIndex++;
                        i = close + 1;
                        continue;
                    }
                }
            }

            if (char.IsHighSurrogate(c) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
            {
                output.Append(c);
                output.Append(source[i + 1]);
                logicalIndex++;
                i += 2;
            }
            else
            {
                output.Append(c);
                logicalIndex++;
                i++;
            }
        }

        // Be forgiving while authoring: an unclosed custom tag applies to end-of-line.
        for (int i = openTags.Count - 1; i >= 0; i--)
        {
            OpenTag open = openTags[i];
            if (logicalIndex > open.StartIndex)
                spans.Add(new TextAnimationSpan(open.Name, open.StartIndex, logicalIndex, open.Parameters));
        }

        spans.Sort((a, b) =>
        {
            int start = a.StartIndex.CompareTo(b.StartIndex);
            return start != 0 ? start : b.Length.CompareTo(a.Length);
        });
        markers.Sort((a, b) => a.CharacterIndex.CompareTo(b.CharacterIndex));

        return new TextAnimationDocument(source, output.ToString(), logicalIndex, spans, markers);
    }

    private static void CloseTag(List<OpenTag> openTags, List<TextAnimationSpan> spans, string name, int endIndex)
    {
        for (int i = openTags.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(openTags[i].Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            OpenTag open = openTags[i];
            openTags.RemoveAt(i);
            if (endIndex > open.StartIndex)
                spans.Add(new TextAnimationSpan(open.Name, open.StartIndex, endIndex, open.Parameters));
            return;
        }
    }

    private static void AddMarker(
        List<TextAnimationMarker> markers,
        string name,
        int characterIndex,
        Dictionary<string, string> values)
    {
        if (name.Equals("event", StringComparison.OrdinalIgnoreCase))
        {
            string eventName = GetFirst(values, "value", "name", "id");
            if (!string.IsNullOrWhiteSpace(eventName))
                markers.Add(new TextAnimationMarker(TextAnimationMarkerType.Event, characterIndex, 0f, eventName));
            return;
        }

        string raw = GetFirst(values, "value", "duration", "time");
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
            markers.Add(new TextAnimationMarker(TextAnimationMarkerType.Pause, characterIndex, Math.Max(0f, duration), string.Empty));
    }

    private static string GetFirst(Dictionary<string, string> values, params string[] keys)
    {
        if (values == null) return string.Empty;
        foreach (string key in keys)
            if (values.TryGetValue(key, out string value))
                return value;
        return string.Empty;
    }

    private static bool TmpTagProducesCharacter(string name) =>
        name.Equals("br", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("sprite", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseTag(
        string raw,
        out bool closing,
        out bool selfClosing,
        out string name,
        out Dictionary<string, string> values)
    {
        closing = false;
        selfClosing = false;
        name = string.Empty;
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(raw)) return false;

        string text = raw.Trim();
        if (text[0] == '/')
        {
            closing = true;
            text = text.Substring(1).TrimStart();
        }

        if (text.EndsWith("/", StringComparison.Ordinal))
        {
            selfClosing = true;
            text = text.Substring(0, text.Length - 1).TrimEnd();
        }

        int pos = 0;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '=')
            pos++;

        if (pos == 0) return false;
        name = text.Substring(0, pos).Trim().ToLowerInvariant();

        if (closing) return true;

        if (pos < text.Length && text[pos] == '=')
        {
            pos++;
            values["value"] = ReadValue(text, ref pos);
        }

        while (pos < text.Length)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            if (pos >= text.Length) break;

            int keyStart = pos;
            while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '=') pos++;
            string key = text.Substring(keyStart, pos - keyStart).Trim();
            if (string.IsNullOrEmpty(key)) break;

            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            if (pos < text.Length && text[pos] == '=')
            {
                pos++;
                while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
                values[key] = ReadValue(text, ref pos);
            }
            else
            {
                values[key] = "true";
            }
        }

        return true;
    }

    private static string ReadValue(string text, ref int pos)
    {
        if (pos >= text.Length) return string.Empty;

        if (text[pos] == '"' || text[pos] == '\'')
        {
            char quote = text[pos++];
            int start = pos;
            while (pos < text.Length && text[pos] != quote) pos++;
            string value = text.Substring(start, pos - start);
            if (pos < text.Length) pos++;
            return value;
        }

        int valueStart = pos;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos])) pos++;
        return text.Substring(valueStart, pos - valueStart);
    }
}
