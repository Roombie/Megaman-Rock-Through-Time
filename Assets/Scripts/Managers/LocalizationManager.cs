using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ApplyLanguage(int index)
    {
        StartCoroutine(InitializeLanguageAsync(index));
    }

    private IEnumerator InitializeLanguageAsync(int index)
    {
        yield return LocalizationSettings.InitializationOperation;

        if (index >= 0 && index < LocalizationSettings.AvailableLocales.Locales.Count)
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
        }
        else
        {
            Debug.LogError("Invalid selected language. Index out of range.");
        }
    }
}