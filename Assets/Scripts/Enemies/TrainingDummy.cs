using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Weapon Lab target: takes hits like an enemy (weakness table included), shows each hit's damage as a floating
/// number and keeps a running total / damage per second. Immortal by default.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TrainingDummy : MonoBehaviour, IDamageable, IDamagePreview
{
    public int maxHealth = 28;
    [Tooltip("Never dies (health refills instead).")]
    public bool immortal = true;
    public WeaponDamage[] weaknesses;
    public AudioEvent hitSound;
    public AudioEvent immuneSound;
    public Color hitFlash = Color.white;
    [Tooltip("Pixel font for the labels (megaman_2).")]
    public TMP_FontAsset font;

    // megaman_2 at size 9 is 1 font pixel = 1 game pixel on a 1-unit-per-pixel canvas. World text: size 1 = 0.1 units
    // and 1 game pixel = 1/16 unit, so 9 * (1/16) / 0.1 = 5.625 keeps it crisp at 256x240.
    private const float PixelFontSize = 5.625f;
    private const float PixelsPerUnit = 16f;

    public int Health { get; private set; }
    public int TotalDamage { get; private set; }
    public int Hits { get; private set; }

    private SpriteRenderer sprite;
    private Color baseColor;
    private TextMeshPro statsLabel;
    private float firstHitTime = -1f;
    private float lastHitTime;
    private Coroutine flash;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        baseColor = sprite.color;
        Health = maxHealth;
        statsLabel = MakeText("Stats");
        statsLabel.alignment = TextAlignmentOptions.Bottom;
        UpdateLabel();
    }

    public int PreviewDamage(in DamageInfo info) => WeaknessTable.Resolve(weaknesses, info);

    public bool TakeDamage(in DamageInfo info)
    {
        int amount = PreviewDamage(info);
        Popup(amount <= 0 ? "IMMUNE" : $"-{amount}", amount <= 0 ? new Color(0.7f, 0.7f, 0.75f) : new Color(1f, 0.85f, 0.3f));
        if (amount <= 0)
        {
            Audio.Play(immuneSound);
            return false;
        }

        Audio.Play(hitSound);
        if (firstHitTime < 0f) firstHitTime = Time.time;
        lastHitTime = Time.time;
        Hits++;
        TotalDamage += amount;
        Health -= amount;
        if (Health <= 0)
        {
            Popup("DEFEATED", new Color(1f, 0.4f, 0.3f));
            Health = maxHealth;
            if (!immortal) gameObject.SetActive(false);
        }
        if (flash != null) StopCoroutine(flash);
        flash = StartCoroutine(Flash());
        UpdateLabel();
        return true;
    }

    public void ResetStats()
    {
        Health = maxHealth;
        TotalDamage = 0;
        Hits = 0;
        firstHitTime = -1f;
        UpdateLabel();
    }

    private void Update()
    {
        // Damage per second over the current burst (resets after 3 s without hits)
        if (firstHitTime >= 0f && Time.time - lastHitTime > 3f) firstHitTime = -1f;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (statsLabel == null) return;
        // Short lines: the screen is only 256 pixels wide (DPS is in the Weapon Lab panel)
        statsLabel.text = $"HP {Health}\nDMG {TotalDamage}";
        // Just above the dummy, on whole game pixels
        Vector3 top = new(transform.position.x, sprite.bounds.max.y + 4f / PixelsPerUnit, 0f);
        statsLabel.transform.position = Snap(top);
    }

    private IEnumerator Flash()
    {
        sprite.color = hitFlash;
        yield return new WaitForSeconds(0.06f);
        sprite.color = baseColor;
        flash = null;
    }

    private void Popup(string text, Color color)
    {
        var label = MakeText("Hit");
        label.transform.SetParent(null, true);
        label.transform.position = Snap(new Vector3(transform.position.x + Random.Range(-4, 5) / PixelsPerUnit, sprite.bounds.center.y, 0f));
        label.text = text;
        label.color = color;
        StartCoroutine(Rise(label));
    }

    private static IEnumerator Rise(TextMeshPro label)
    {
        Vector3 start = label.transform.position;
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            if (label == null) yield break;
            label.transform.position = Snap(start + Vector3.up * (t * 2f));   // rises a whole pixel at a time
            label.alpha = 1f - t / 0.7f;
            yield return null;
        }
        if (label != null) Destroy(label.gameObject);
    }

    public float DamagePerSecond
    {
        get
        {
            if (firstHitTime < 0f) return 0f;
            return TotalDamage / Mathf.Max(0.5f, lastHitTime - firstHitTime);
        }
    }

    private static Vector3 Snap(Vector3 p) =>
        new(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, p.z);

    private TextMeshPro MakeText(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        // Undo the dummy's scale so the text keeps its pixel size
        Vector3 scale = transform.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f);
        var text = go.AddComponent<TextMeshPro>();
        if (font != null) text.font = font;
        text.fontSize = PixelFontSize;
        text.enableAutoSizing = false;
        text.extraPadding = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.sortingOrder = 100;
        return text;
    }

#if UNITY_EDITOR
    // Picks the project's pixel font automatically (also for dummies made before this field existed)
    private void OnValidate()
    {
        if (font != null) return;
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath("f47decd5411deaf46a0af54ba445a67e");   // megaman_2
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }
#endif
}
