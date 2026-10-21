using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central library that maps string keys to AudioEvent assets.
/// Create one per project: Assets → Create → Audio → Audio Database
///
/// ── Why use this? ────────────────────────────────────────────────────────
/// Instead of dragging AudioEvent references into every script that needs
/// one, you store all events here and retrieve them by key from anywhere.
/// This is especially useful for systems like animation state machines,
/// particle systems, or procedural spawners that can't hold serialized
/// references easily.
///
/// ── Usage ────────────────────────────────────────────────────────────────
///
///     // In code:
///     AudioEvent evt = AudioDatabase.Instance.Get("Player_Footstep_Grass");
///     Audio.Play(evt);
///
///     // Or use the static shortcut:
///     AudioDatabase.Play("Explosion_Big");
///
/// ── Best practice ────────────────────────────────────────────────────────
/// For scripts that CAN hold a serialized reference, prefer a direct
/// [SerializeField] AudioEvent field — it's faster and type-safe.
/// Use AudioDatabase for dynamic/runtime lookups only.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Audio Database")]
public class AudioDatabase : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Singleton (set in AudioManager.Awake or manually via Initialize())
    // -------------------------------------------------------------------------

    public static AudioDatabase Instance { get; private set; }

    /// <summary>
    /// Registers this database as the global instance.
    /// Called automatically by AudioManager if a reference is assigned.
    /// </summary>
    public void Initialize() => Instance = this;

    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    [Tooltip("All AudioEvents registered in this project. " +
             "The key is a string used for runtime lookup.")]
    public AudioDatabaseEntry[] entries;

    Dictionary<string, AudioEvent> lookup;

    void OnEnable() => BuildLookup();

    void BuildLookup()
    {
        lookup = new Dictionary<string, AudioEvent>(System.StringComparer.OrdinalIgnoreCase);

        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry.audioEvent == null)
            {
                Debug.LogWarning($"[AudioDatabase] Entry with key '{entry.key}' has no AudioEvent assigned.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.key))
            {
                Debug.LogWarning($"[AudioDatabase] Entry for '{entry.audioEvent.name}' has an empty key.");
                continue;
            }

            if (lookup.ContainsKey(entry.key))
            {
                Debug.LogWarning($"[AudioDatabase] Duplicate key '{entry.key}' — keeping first entry.");
                continue;
            }

            lookup[entry.key] = entry.audioEvent;
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the AudioEvent registered under the given key, or null with a
    /// warning if the key is not found.
    /// </summary>
    public AudioEvent Get(string key)
    {
        if (lookup == null) BuildLookup();

        if (lookup.TryGetValue(key, out AudioEvent evt))
            return evt;

        Debug.LogWarning($"[AudioDatabase] Key not found: '{key}'");
        return null;
    }

    /// <summary>Returns true if the key exists in the database.</summary>
    public bool Contains(string key)
    {
        if (lookup == null) BuildLookup();
        return lookup.ContainsKey(key);
    }

    // -------------------------------------------------------------------------
    // Static convenience — requires Initialize() to have been called
    // -------------------------------------------------------------------------

    /// <summary>
    /// Plays an AudioEvent looked up by key via the global Audio service.
    /// Requires AudioDatabase.Instance to be set (done by AudioManager).
    /// </summary>
    public static AudioHandle Play(string key)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[AudioDatabase] No instance registered. Did you assign a database to AudioManager?");
            return AudioHandle.Invalid;
        }

        AudioEvent evt = Instance.Get(key);
        return evt != null ? Audio.Play(evt) : AudioHandle.Invalid;
    }

    /// <summary>
    /// Plays an AudioEvent at a world position, looked up by key.
    /// </summary>
    public static AudioHandle PlayAt(string key, Vector3 position)
    {
        if (Instance == null) return AudioHandle.Invalid;
        AudioEvent evt = Instance.Get(key);
        return evt != null ? Audio.PlayAtPosition(evt, position) : AudioHandle.Invalid;
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A key→AudioEvent pair stored in the AudioDatabase.</summary>
[System.Serializable]
public class AudioDatabaseEntry
{
    [Tooltip("Unique string identifier used for runtime lookup. " +
             "Convention: 'Category_Name' e.g. 'Player_Jump', 'Enemy_Death', 'UI_Click'.")]
    public string     key;
    public AudioEvent audioEvent;
}