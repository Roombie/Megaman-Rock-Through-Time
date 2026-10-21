#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// Audio Collection — LumberCat
/// Browse, search, preview and manage all AudioEvent assets in the project.
/// Open via:  Tools → Audio → Audio Collection
/// </summary>
public class AudioCollectionWindow : EditorWindow
{
    // ── Editor icon ─────────────────────────────────────────────────────────
    // Resolved relative to THIS script's own asset path, so the AudioSystem
    // folder can be moved, renamed, or dropped into another project and the
    // icon still loads. Cached: OnGUI runs many times per second.
    Texture2D _headerIcon;
    Texture2D HeaderIcon => _headerIcon ??= AssetDatabase.LoadAssetAtPath<Texture2D>(
        Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)))
            .Replace('\\', '/') + "/Icons/RoombieEditorIcon.png");

    // =========================================================================
    // Constants & Colours
    // =========================================================================

    static readonly Color COL_HEADER_BG    = new Color(0.13f, 0.13f, 0.16f, 1f);
    static readonly Color COL_PANEL_BG     = new Color(0.17f, 0.17f, 0.20f, 1f);
    static readonly Color COL_ROW_EVEN     = new Color(0.20f, 0.20f, 0.23f, 1f);
    static readonly Color COL_ROW_ODD      = new Color(0.18f, 0.18f, 0.21f, 1f);
    static readonly Color COL_ACCENT_SFX   = new Color(0.25f, 0.65f, 0.35f, 1f);
    static readonly Color COL_ACCENT_BGM   = new Color(0.30f, 0.55f, 0.90f, 1f);
    static readonly Color COL_ACCENT_UI    = new Color(0.90f, 0.75f, 0.20f, 1f);
    static readonly Color COL_ACCENT_VOICE = new Color(0.85f, 0.40f, 0.25f, 1f);
    static readonly Color COL_BTN_RED      = new Color(0.70f, 0.20f, 0.20f, 1f);
    static readonly Color COL_BTN_GREEN    = new Color(0.20f, 0.55f, 0.28f, 1f);
    static readonly Color COL_BTN_BLUE     = new Color(0.20f, 0.40f, 0.75f, 1f);
    static readonly Color COL_TEXT_DIM     = new Color(0.55f, 0.55f, 0.60f, 1f);
    static readonly Color COL_TEXT_BRIGHT  = new Color(0.92f, 0.92f, 0.95f, 1f);
    static readonly Color COL_TAG_BG       = new Color(0.22f, 0.22f, 0.26f, 1f);

    const float HEADER_HEIGHT = 72f;
    const float ROW_HEIGHT    = 24f;
    const float TAG_HEIGHT    = 28f;
    const float BTN_TINY      = 22f;

    // =========================================================================
    // State
    // =========================================================================

    string  search_tag    = "";
    string  search_clip   = "";
    int     collection_tab = 0;
    Vector2 col_scroll    = Vector2.zero;

    List<AudioEvent> allEvents = new();

    // Tab arrays — rebuilt from AudioChannelConfig if available
    string[]        tabLabels      = { "Sounds", "Music", "UI", "Voice" };
    SoundCategory[] tabCategories  = { SoundCategory.SFX, SoundCategory.BGM, SoundCategory.UI, SoundCategory.Voice };
    bool            tabsFromConfig = false;
    double lastRefreshTime = -1;
    const double REFRESH_INTERVAL = 2.0;

    Dictionary<AudioEvent, AudioSource> previewSources = new();

    // =========================================================================
    // Window lifecycle
    // =========================================================================

    [MenuItem("Tools/Audio/Audio Collection")]
    public static void OpenWindow()
    {
        var w = GetWindow<AudioCollectionWindow>("Audio Collection");
        w.minSize = new Vector2(420, 480);
        w.Show();
    }

    void OnEnable()
    {
        RefreshEventCache();
        RebuildTabs();
        // Clean up preview GameObjects before scripts reload so they don't leak
        AssemblyReloadEvents.beforeAssemblyReload += CleanupAllPreviewSources;
    }
    void OnDisable()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= CleanupAllPreviewSources;
        CleanupAllPreviewSources();
    }
    void OnFocus()   => RefreshEventCache();
    void OnInspectorUpdate() => Repaint();

    // =========================================================================
    // OnGUI
    // =========================================================================

    void OnGUI()
    {
        if (EditorApplication.timeSinceStartup - lastRefreshTime > REFRESH_INTERVAL)
            RefreshEventCache();

        float w = position.width;
        float h = position.height;

        EditorGUI.DrawRect(new Rect(0, 0, w, h), COL_PANEL_BG);

        GUILayout.BeginArea(new Rect(0, 0, w, h));
        DrawCollectionPanel(w, h);
        GUILayout.EndArea();
    }

    // =========================================================================
    // Collection Panel
    // =========================================================================

    void DrawCollectionPanel(float w, float h)
    {
        DrawPanelHeader("AUDIO COLLECTION", "Manage and modify your audio events", w, HEADER_HEIGHT);

        float bodyY = HEADER_HEIGHT + 4;
        float bodyH = h - bodyY - 4;

        GUILayout.BeginArea(new Rect(0, bodyY, w, bodyH));
        GUILayout.BeginVertical();

        float innerW = w - 24;

        // ── Category tabs ─────────────────────────────────────────────────
        GUILayout.Space(6);
        Rect tabRect = GUILayoutUtility.GetRect(innerW, 28);
        tabRect.x += 12;
        tabRect.width = innerW;
        collection_tab = Mathf.Clamp(collection_tab, 0, tabLabels.Length - 1);
        collection_tab = DrawCategoryTabs(tabRect, collection_tab, tabLabels, tabCategories);
        if (!tabsFromConfig)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            var oc = GUI.color; GUI.color = COL_TEXT_DIM;
            GUILayout.Label("⚠ No AudioManager in scene — showing default tabs", DimLabelStyle());
            GUI.color = oc;
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(8);

        // ── Search row ────────────────────────────────────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        GUILayout.Label("Tag:", DimLabelStyle(), GUILayout.Width(28));
        search_tag  = EditorGUILayout.TextField(search_tag,  GUILayout.Width(120));
        GUILayout.Space(8);
        GUILayout.Label("Clip:", DimLabelStyle(), GUILayout.Width(28));
        search_clip = EditorGUILayout.TextField(search_clip, GUILayout.Width(120));
        GUILayout.Space(8);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.25f, 0.30f);
        if (GUILayout.Button("Clear", GUILayout.Width(46), GUILayout.Height(18)))
        { search_tag = ""; search_clip = ""; }
        GUI.backgroundColor = old;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↺", GUILayout.Width(24), GUILayout.Height(18)))
            RefreshEventCache();
        GUILayout.Space(12);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // ── Scrollable list ───────────────────────────────────────────────
        col_scroll = GUILayout.BeginScrollView(col_scroll, GUIStyle.none, GUI.skin.verticalScrollbar);

        var filtered = GetFilteredEvents(
            collection_tab < tabCategories.Length ? tabCategories[collection_tab] : SoundCategory.SFX);
        var grouped  = GroupByTag(filtered);
        int rowIndex = 0;

        foreach (var group in grouped)
            DrawTagGroup(group.Key, group.Value, innerW, ref rowIndex);

        if (grouped.Count == 0)
        {
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("No audio events found.", DimLabelStyle());
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void DrawTagGroup(string tag, List<AudioEvent> events, float w, ref int rowIndex)
    {
        Rect headerRect = GUILayoutUtility.GetRect(w, TAG_HEIGHT + 2);
        headerRect.x += 12; headerRect.width = w - 12;
        EditorGUI.DrawRect(headerRect, COL_TAG_BG);

        // Coloured left border
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3, headerRect.height),
            collection_tab < tabCategories.Length
                ? CategoryColor(tabCategories[collection_tab]) : Color.gray);

        // Tag label
        GUI.Label(new Rect(headerRect.x + 10, headerRect.y + 6, 200, 18), tag, TagHeaderStyle());

        // Count badge
        string badge = events.Count.ToString();
        float badgeW = 22 + badge.Length * 5;
        Rect badgeRect = new Rect(headerRect.x + 10 + 200, headerRect.y + 7, badgeW, 16);
        EditorGUI.DrawRect(badgeRect, new Color(0.30f, 0.30f, 0.35f));
        GUI.Label(badgeRect, badge, BadgeStyle());

        // Add (+) button
        float addBtnX = headerRect.x + headerRect.width - BTN_TINY - 8;
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = COL_BTN_GREEN;
        if (GUI.Button(new Rect(addBtnX, headerRect.y + 3, BTN_TINY, BTN_TINY), "+", SmallBtnStyle()))
            AddClipToTag(tag,
                collection_tab < tabCategories.Length ? tabCategories[collection_tab] : SoundCategory.SFX);
        GUI.backgroundColor = oldBg;

        GUILayout.Space(2);

        foreach (var evt in events)
        {
            if (evt == null) continue;
            DrawEventRow(evt, w, rowIndex);
            rowIndex++;
        }

        // Footer info + delete
        Rect infoRect = GUILayoutUtility.GetRect(w, 20);
        infoRect.x += 12; infoRect.width = w - 12;
        EditorGUI.DrawRect(infoRect, new Color(0.15f, 0.15f, 0.18f));

        oldBg = GUI.backgroundColor;
        GUI.backgroundColor = COL_BTN_RED;
        if (GUI.Button(new Rect(infoRect.x + infoRect.width - 64, infoRect.y + 1, 60, 18),
                "Delete", SmallBtnStyle()))
        {
            if (EditorUtility.DisplayDialog("Delete Tag Group",
                $"Delete ALL {events.Count} events tagged '{tag}'?", "Delete", "Cancel"))
                DeleteTagGroup(events);
        }
        GUI.backgroundColor = oldBg;

        GUILayout.Space(6);
    }

    void DrawEventRow(AudioEvent evt, float w, int index)
    {
        Rect row = GUILayoutUtility.GetRect(w, ROW_HEIGHT);
        row.x += 12; row.width = w - 12;

        EditorGUI.DrawRect(row, index % 2 == 0 ? COL_ROW_EVEN : COL_ROW_ODD);

        GUI.Label(new Rect(row.x + 6,  row.y + 4, 16,  16), "♪", NoteStyle());
        GUI.Label(new Rect(row.x + 22, row.y + 4, 200, 16), evt.name, RowLabelStyle());

        int clipCount = evt.clips != null ? evt.clips.Length : 0;
        GUI.Label(new Rect(row.x + 225, row.y + 5, 40, 14), $"({clipCount})", DimLabelStyle());

        float btnX = row.x + row.width - (BTN_TINY * 3 + 6);

        Color oldBg = GUI.backgroundColor;

        // Ping ◎
        GUI.backgroundColor = COL_BTN_BLUE;
        if (GUI.Button(new Rect(btnX, row.y + 1, BTN_TINY, BTN_TINY - 2), "◎", SmallBtnStyle()))
            EditorGUIUtility.PingObject(evt);
        GUI.backgroundColor = oldBg;
        btnX += BTN_TINY + 2;

        // Play ▶ / Stop ■
        bool isPlaying = IsPreviewPlaying(evt);
        GUI.backgroundColor = isPlaying ? new Color(0.6f, 0.6f, 0.1f) : COL_BTN_GREEN;
        if (GUI.Button(new Rect(btnX, row.y + 1, BTN_TINY, BTN_TINY - 2),
            isPlaying ? "■" : "▶", SmallBtnStyle()))
        {
            if (isPlaying) StopPreview(evt); else PlayPreview(evt);
        }
        GUI.backgroundColor = oldBg;
        btnX += BTN_TINY + 2;

        // Remove ×
        GUI.backgroundColor = COL_BTN_RED;
        if (GUI.Button(new Rect(btnX, row.y + 1, BTN_TINY, BTN_TINY - 2), "×", SmallBtnStyle()))
        {
            if (EditorUtility.DisplayDialog("Remove Event",
                $"Remove '{evt.name}' from the database?\n(Asset file will NOT be deleted.)",
                "Remove", "Cancel"))
                RemoveFromDatabase(evt);
        }
        GUI.backgroundColor = oldBg;
    }

    // =========================================================================
    // Shared UI helpers
    // =========================================================================

    void DrawPanelHeader(string title, string subtitle, float w, float h)
    {
        EditorGUI.DrawRect(new Rect(0, 0, w, h), COL_HEADER_BG);
        EditorGUI.DrawRect(new Rect(0, h - 2, w, 2), new Color(0.25f, 0.50f, 0.85f));

        float iconSize = 50f;
        float cx = 12 + iconSize * 0.5f;
        float cy = h * 0.5f;
        var headerIcon = HeaderIcon;
        if (headerIcon != null)
        {
            GUI.DrawTexture(new Rect(cx - iconSize * 0.55f, cy - iconSize * 0.5f, iconSize, iconSize), headerIcon, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(new Rect(cx - 10, cy - 10, 20, 20), new Color(0.25f, 0.50f, 0.85f, 0.25f));
            GUI.Label(new Rect(cx - 8, cy - 9, 18, 18), "♫", IconStyle());
        }

        float textX = cx + iconSize * 0.5f + 10f;
        GUI.Label(new Rect(textX, 10, w - textX - 8, 26), title, TitleStyle());
        GUI.Label(new Rect(textX, 34, w - textX - 8, 18), subtitle, SubtitleStyle());
    }

    // Rebuilds tab arrays from AudioChannelConfig (via AudioManager) if available.
    void RebuildTabs()
    {
        var mgr    = FindFirstObjectByType<AudioManager>();
        var config = mgr != null ? mgr.channelConfig : null;

        if (config != null && config.channels != null && config.channels.Count > 0)
        {
            var labels = new List<string>();
            var cats   = new List<SoundCategory>();
            foreach (var ch in config.channels)
            {
                if (ch.isMaster || ch.category == ChannelCategory.None) continue;
                labels.Add(ch.label);
                cats.Add(ChanCatToSoundCat(ch.category));
            }
            if (labels.Count > 0)
            {
                tabLabels      = labels.ToArray();
                tabCategories  = cats.ToArray();
                tabsFromConfig = true;
                return;
            }
        }
        tabLabels      = new[] { "Sounds", "Music", "UI", "Voice" };
        tabCategories  = new[] { SoundCategory.SFX, SoundCategory.BGM, SoundCategory.UI, SoundCategory.Voice };
        tabsFromConfig = false;
    }

    static SoundCategory ChanCatToSoundCat(ChannelCategory cc) => cc switch
    {
        ChannelCategory.BGM   => SoundCategory.BGM,
        ChannelCategory.UI    => SoundCategory.UI,
        ChannelCategory.Voice => SoundCategory.Voice,
        _                     => SoundCategory.SFX,
    };

    int DrawCategoryTabs(Rect r, int selected, string[] labels, SoundCategory[] cats)
    {
        float tabW = r.width / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            Rect t = new Rect(r.x + i * tabW, r.y, tabW - 1, r.height);
            bool active = selected == i;
            EditorGUI.DrawRect(t, active ? CategoryColor(cats[i]) : new Color(0.22f, 0.22f, 0.25f));
            GUI.Label(t, labels[i], active ? ActiveTabStyle() : InactiveTabStyle());
            if (Event.current.type == EventType.MouseDown && t.Contains(Event.current.mousePosition))
            { selected = i; Event.current.Use(); Repaint(); }
        }
        return selected;
    }

    // =========================================================================
    // Preview audio
    // =========================================================================

    void PlayPreview(AudioEvent evt)
    {
        StopPreview(evt);
        AudioClip clip = evt.GetClip();
        if (clip == null) return;
        var go = new GameObject($"__AudioPreview_{evt.name}") { hideFlags = HideFlags.HideAndDontSave };
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.loop = evt.loop;
        src.volume = evt.GetVolume(); src.pitch = evt.GetPitch();
        src.Play();
        previewSources[evt] = src;
    }

    void StopPreview(AudioEvent evt)
    {
        if (previewSources.TryGetValue(evt, out var src) && src != null)
        { src.Stop(); DestroyImmediate(src.gameObject); }
        previewSources.Remove(evt);
    }

    bool IsPreviewPlaying(AudioEvent evt) =>
        previewSources.TryGetValue(evt, out var src) && src != null && src.isPlaying;

    void CleanupAllPreviewSources()
    {
        foreach (var src in previewSources.Values)
            if (src != null) DestroyImmediate(src.gameObject);
        previewSources.Clear();
    }

    // =========================================================================
    // Data helpers
    // =========================================================================

    void RefreshEventCache()
    {
        allEvents.Clear();
        foreach (var g in AssetDatabase.FindAssets("t:AudioEvent"))
        {
            var e = AssetDatabase.LoadAssetAtPath<AudioEvent>(AssetDatabase.GUIDToAssetPath(g));
            if (e != null) allEvents.Add(e);
        }
        lastRefreshTime = EditorApplication.timeSinceStartup;
        Repaint();
    }

    List<AudioEvent> GetFilteredEvents(SoundCategory category)
    {
        return allEvents
            .Where(e => e != null && e.category == category)
            .Where(e => string.IsNullOrEmpty(search_tag)  || e.name.IndexOf(search_tag,  System.StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(e => string.IsNullOrEmpty(search_clip) ||
                (e.clips != null && e.clips.Any(c => c != null && c.name.IndexOf(search_clip, System.StringComparison.OrdinalIgnoreCase) >= 0)))
            .OrderBy(e => e.name)
            .ToList();
    }

    Dictionary<string, List<AudioEvent>> GroupByTag(List<AudioEvent> events)
    {
        var result = new Dictionary<string, List<AudioEvent>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var e in events)
        {
            string key = ExtractTag(e.name);
            if (!result.ContainsKey(key)) result[key] = new List<AudioEvent>();
            result[key].Add(e);
        }
        return result;
    }

    string ExtractTag(string name)
    {
        int idx = name.IndexOf('_');
        if (idx > 0) return name.Substring(0, idx);
        idx = name.IndexOf(' ');
        if (idx > 0) return name.Substring(0, idx);
        return name;
    }

    AudioDatabase FindAudioDatabase()
    {
        var guids = AssetDatabase.FindAssets("t:AudioDatabase");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<AudioDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    void AddClipToTag(string tag, SoundCategory category)
    {
        int existing = allEvents.Count(e => e != null &&
            ExtractTag(e.name).Equals(tag, System.StringComparison.OrdinalIgnoreCase));
        string folder = "Assets/Audio/Events";
        if (!AssetDatabase.IsValidFolder(folder))
        { System.IO.Directory.CreateDirectory(folder); AssetDatabase.Refresh(); }
        string assetName = existing == 0 ? tag : $"{tag} ({existing + 1})";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
        var evt = CreateInstance<AudioEvent>();
        evt.category = category;
        AssetDatabase.CreateAsset(evt, path);
        AssetDatabase.SaveAssets();
        var db = FindAudioDatabase();
        if (db != null)
        {
            ArrayUtility.Add(ref db.entries, new AudioDatabaseEntry { key = assetName, audioEvent = evt });
            EditorUtility.SetDirty(db); AssetDatabase.SaveAssets();
        }
        EditorGUIUtility.PingObject(evt);
        RefreshEventCache();
    }

    void RemoveFromDatabase(AudioEvent evt)
    {
        var db = FindAudioDatabase();
        if (db == null || db.entries == null) return;
        int idx = System.Array.FindIndex(db.entries, e => e.audioEvent == evt);
        if (idx >= 0) { ArrayUtility.RemoveAt(ref db.entries, idx); EditorUtility.SetDirty(db); AssetDatabase.SaveAssets(); }
        RefreshEventCache();
    }

    void DeleteTagGroup(List<AudioEvent> events)
    {
        var db = FindAudioDatabase();
        foreach (var evt in events)
        {
            if (evt == null) continue;
            StopPreview(evt);
            if (db != null && db.entries != null)
            {
                int idx = System.Array.FindIndex(db.entries, e => e.audioEvent == evt);
                if (idx >= 0) ArrayUtility.RemoveAt(ref db.entries, idx);
            }
            string path = AssetDatabase.GetAssetPath(evt);
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        }
        if (db != null) { EditorUtility.SetDirty(db); AssetDatabase.SaveAssets(); }
        RefreshEventCache();
    }

    // =========================================================================
    // GUIStyles
    // =========================================================================

    GUIStyle _titleStyle, _subtitleStyle, _tagHeaderStyle;
    GUIStyle _dimLabelStyle, _rowLabelStyle, _noteStyle, _iconStyle, _badgeStyle;
    GUIStyle _activeTabStyle, _inactiveTabStyle, _smallBtnStyle;

    GUIStyle TitleStyle()
    {
        if (_titleStyle != null) return _titleStyle;
        return _titleStyle = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 16, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 0.75f, 0.25f) } };
    }
    GUIStyle SubtitleStyle()
    {
        if (_subtitleStyle != null) return _subtitleStyle;
        return _subtitleStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 10, alignment = TextAnchor.MiddleLeft, normal = { textColor = COL_TEXT_DIM } };
    }
    GUIStyle TagHeaderStyle()
    {
        if (_tagHeaderStyle != null) return _tagHeaderStyle;
        return _tagHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 11, normal = { textColor = COL_TEXT_BRIGHT } };
    }
    GUIStyle DimLabelStyle()
    {
        if (_dimLabelStyle != null) return _dimLabelStyle;
        return _dimLabelStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 10, normal = { textColor = COL_TEXT_DIM } };
    }
    GUIStyle RowLabelStyle()
    {
        if (_rowLabelStyle != null) return _rowLabelStyle;
        return _rowLabelStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 11, normal = { textColor = COL_TEXT_BRIGHT } };
    }
    GUIStyle NoteStyle()
    {
        if (_noteStyle != null) return _noteStyle;
        return _noteStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.55f, 0.80f, 0.55f) } };
    }
    GUIStyle IconStyle()
    {
        if (_iconStyle != null) return _iconStyle;
        return _iconStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.35f, 0.65f, 1f) } };
    }
    GUIStyle BadgeStyle()
    {
        if (_badgeStyle != null) return _badgeStyle;
        return _badgeStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = COL_TEXT_DIM } };
    }
    GUIStyle ActiveTabStyle()
    {
        if (_activeTabStyle != null) return _activeTabStyle;
        return _activeTabStyle = new GUIStyle(EditorStyles.miniButtonMid)
        { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = Color.white }, hover = { textColor = Color.white }, active = { textColor = Color.white } };
    }
    GUIStyle InactiveTabStyle()
    {
        if (_inactiveTabStyle != null) return _inactiveTabStyle;
        return _inactiveTabStyle = new GUIStyle(EditorStyles.miniButtonMid)
        { fontSize = 11, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = COL_TEXT_DIM }, hover = { textColor = COL_TEXT_BRIGHT } };
    }
    GUIStyle SmallBtnStyle()
    {
        if (_smallBtnStyle != null) return _smallBtnStyle;
        return _smallBtnStyle = new GUIStyle(EditorStyles.miniButton)
        { fontSize = 11, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0,0,0,0),
          normal = { textColor = Color.white }, hover = { textColor = Color.white }, active = { textColor = Color.white } };
    }

    static Color CategoryColor(SoundCategory cat) => cat switch
    {
        SoundCategory.SFX   => COL_ACCENT_SFX,
        SoundCategory.BGM   => COL_ACCENT_BGM,
        SoundCategory.UI    => COL_ACCENT_UI,
        SoundCategory.Voice => COL_ACCENT_VOICE,
        _ => Color.gray
    };
}
#endif