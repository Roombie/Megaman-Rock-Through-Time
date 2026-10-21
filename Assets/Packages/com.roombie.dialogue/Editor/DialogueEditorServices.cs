using System;
using System.Collections.Generic;

/// <summary>
/// Optional project hooks used only by the reusable graph editor.
/// A project integration can register localization preview, CSV sync and unused-key maintenance.
/// </summary>
public static class DialogueEditorServices
{
    public static Func<string, string, string> ResolveText;
    public static Func<string> GetLanguageCode;

    public static Func<bool> SyncLocalization;
    public static Func<List<string>> GetAllTextIds;
    public static Action<List<string>> RemoveTextIds;

    public static string Text(string key, string languageCode = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (ResolveText != null)
            return ResolveText(key.Trim(), languageCode);

        return key.Trim();
    }

    public static string LanguageCode() =>
        GetLanguageCode != null ? GetLanguageCode() ?? string.Empty : string.Empty;
}
