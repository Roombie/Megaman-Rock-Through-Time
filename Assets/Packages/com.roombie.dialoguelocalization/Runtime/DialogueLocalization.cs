using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public static class DialogueLocalization
{
    public const string TableName = "Dialogues";

    public static LocalizedString CreateLocalizedString(string id) => CreateLocalizedString(TableName, id);
    public static LocalizedString CreateLocalizedString(string tableName, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return new LocalizedString
        {
            TableReference = string.IsNullOrWhiteSpace(tableName) ? TableName : tableName.Trim(),
            TableEntryReference = id.Trim()
        };
    }

    public static string GetText(string id, string languageCode = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        try
        {
            string value = CreateLocalizedString(id).GetLocalizedString();
            return string.IsNullOrEmpty(value) ? id.Trim() : value;
        }
        catch { return id.Trim(); }
    }

    public static string GetEditorLanguageCode()
    {
        Locale locale = LocalizationSettings.SelectedLocale;
        return locale != null ? locale.Identifier.Code.ToUpperInvariant() : string.Empty;
    }

    public static void ReloadCSV() { }
    public static List<string> GetAllIds() => null;
    public static void RemoveIds(List<string> ids) { }
}
