using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Sets a TextMeshPro text from the GameText table and keeps it in sync with the selected language.
/// With "Pixel Centre" the text is centred on `centreX` but snapped to a whole game pixel
/// (TMP's own centring puts odd-width texts on half pixels, which blurs pixel fonts).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    public string key;
    [Tooltip("Shown when the key is missing.")]
    public string fallback;

    [Header("Pixel centring (keep the text's alignment Top Left)")]
    public bool pixelCentre;
    public float centreX;

    private TMP_Text text;

    private void OnEnable()
    {
        text = GetComponent<TMP_Text>();
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale locale) => Refresh();

    public void Refresh()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (!string.IsNullOrEmpty(key))
        {
            string value = LocalizationSettings.StringDatabase.GetLocalizedString("GameText", key);
            text.text = string.IsNullOrEmpty(value) ? fallback : value;
        }

        if (pixelCentre)
        {
            var rect = (RectTransform)transform;
            float width = text.GetPreferredValues(text.text).x;
            rect.anchoredPosition = new Vector2(Mathf.Round(centreX - width / 2f), rect.anchoredPosition.y);
        }
    }
}
