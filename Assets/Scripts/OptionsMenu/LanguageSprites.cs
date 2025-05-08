using UnityEngine;
using UnityEngine.Localization;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LanguageSprites", menuName = "Localization/LanguageSprites")]
public class LanguageSprites : ScriptableObject
{
    [System.Serializable]
    public struct LanguageSpriteSet
    {
        public Locale locale;
        public Sprite onSprite;
        public Sprite offSprite;
    }

    public LanguageSpriteSet[] languageSpriteSets;

    private Dictionary<string, LanguageSpriteSet> spriteSetDict;

    private void OnEnable()
    {
        InitializeDictionary();
    }

   private void InitializeDictionary()
    {
        spriteSetDict = new Dictionary<string, LanguageSpriteSet>();
        foreach (var set in languageSpriteSets)
        {
            string key = set.locale.Identifier.Code; // Ex: "en", "es"
            if (!spriteSetDict.ContainsKey(key))
            {
                spriteSetDict.Add(key, set);
            }
        }
    }

    public Sprite GetSprite(Locale locale, bool isOn)
    {
        if (locale == null) return null;

        string key = locale.Identifier.Code;

        if (spriteSetDict.TryGetValue(key, out var spriteSet))
        {
            return isOn ? spriteSet.onSprite : spriteSet.offSprite;
        }

        Debug.LogWarning($"Sprite for locale '{key}' not found.");
        return null;
    }
}