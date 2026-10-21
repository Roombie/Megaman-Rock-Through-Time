using System.Text;

/// <summary>
/// Replaces {name} placeholders in dialogue text with values from an IDialogueVariableSource.
///
/// Syntax:
///   {playerName}           value, or left verbatim when the variable is unknown
///   {playerName|Stranger}  value, or "Stranger" when the variable is unknown
///   {{ and }}              literal braces
///
/// Runs after localization and before Text Animator, so TMP and animator tags in the surrounding
/// text are untouched. Substituted values are inserted raw; a source that returns user-typed text
/// containing '&lt;' should wrap it in &lt;noparse&gt; itself.
/// </summary>
public static class DialogueTextInterpolator
{
    public static bool ContainsPlaceholder(string text) =>
        !string.IsNullOrEmpty(text) && text.IndexOf('{') >= 0;

    public static string Format(string text, IDialogueVariableSource source)
    {
        if (!ContainsPlaceholder(text))
            return text ?? string.Empty;

        var output = new StringBuilder(text.Length + 16);
        for (int i = 0; i < text.Length;)
        {
            char c = text[i];
            if (c == '{')
            {
                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    output.Append('{');
                    i += 2;
                    continue;
                }

                int close = text.IndexOf('}', i + 1);
                if (close < 0)
                {
                    output.Append(text, i, text.Length - i);
                    break;
                }

                AppendPlaceholder(output, text, i, close, source);
                i = close + 1;
                continue;
            }

            if (c == '}' && i + 1 < text.Length && text[i + 1] == '}')
            {
                output.Append('}');
                i += 2;
                continue;
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }

    private static void AppendPlaceholder(StringBuilder output, string text, int open, int close, IDialogueVariableSource source)
    {
        string body = text.Substring(open + 1, close - open - 1);
        int pipe = body.IndexOf('|');
        string name = (pipe >= 0 ? body.Substring(0, pipe) : body).Trim();

        if (name.Length > 0 && source != null && source.TryGetVariable(name, out string value))
        {
            output.Append(value ?? string.Empty);
            return;
        }

        if (pipe >= 0)
        {
            output.Append(body, pipe + 1, body.Length - pipe - 1);
            return;
        }

        // Unknown variable with no fallback: keep it visible so authoring mistakes are obvious.
        output.Append(text, open, close - open + 1);
    }
}
