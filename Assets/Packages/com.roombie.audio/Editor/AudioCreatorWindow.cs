#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// Audio Creator — LumberCat
/// Creates and configures AudioEvent ScriptableObject assets.
/// Open via:  Tools → Audio → Audio Creator
/// </summary>
public class AudioCreatorWindow : EditorWindow
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
    static readonly Color COL_ACCENT_SFX   = new Color(0.25f, 0.65f, 0.35f, 1f);
    static readonly Color COL_ACCENT_BGM   = new Color(0.30f, 0.55f, 0.90f, 1f);
    static readonly Color COL_ACCENT_UI    = new Color(0.90f, 0.75f, 0.20f, 1f);
    static readonly Color COL_ACCENT_VOICE = new Color(0.85f, 0.40f, 0.25f, 1f);
    static readonly Color COL_BTN_GREEN    = new Color(0.20f, 0.55f, 0.28f, 1f);
    static readonly Color COL_TEXT_DIM     = new Color(0.55f, 0.55f, 0.60f, 1f);
    static readonly Color COL_TEXT_BRIGHT  = new Color(0.92f, 0.92f, 0.95f, 1f);

    const float HEADER_HEIGHT = 72f;

    static readonly string[] PRESET_NAMES = {
        "Frequent Sound",
        "Ambient / Music",
        "Dialogue",
        "UI Click",
    };
    static readonly string[] PRESET_DESCRIPTIONS = {
        "Short SFX played frequently (footsteps, hits). Vorbis, Decompress on Load.",
        "Long ambient loops or music. Vorbis, Streaming. Stereo.",
        "Voice lines. Vorbis, Load on Demand. Mono recommended.",
        "Very short UI sounds. PCM or ADPCM, Decompress on Load.",
    };

    // =========================================================================
    // State
    // =========================================================================

    string      creator_tag      = "";
    int         creator_category = 0;
    int         creator_preset   = 0;
    bool        creator_forceMono = true;
    List<AudioClip> creator_clips = new List<AudioClip>();

    ReorderableList creator_clipsList;

    // Tab arrays — rebuilt from AudioChannelConfig if available
    string[]        tabLabels       = { "Sounds", "Music", "UI", "Voice" };
    SoundCategory[] tabCategories   = { SoundCategory.SFX, SoundCategory.BGM, SoundCategory.UI, SoundCategory.Voice };
    bool            tabsFromConfig  = false;
    List<AudioEvent> allEvents = new();
    double lastRefreshTime = -1;
    const double REFRESH_INTERVAL = 2.0;

    // =========================================================================
    // Window lifecycle
    // =========================================================================

    [MenuItem("Tools/Audio/Audio Creator")]
    public static void OpenWindow()
    {
        var w = GetWindow<AudioCreatorWindow>("Audio Creator");
        w.minSize = new Vector2(400, 480);
        w.Show();
    }

    void OnEnable()
    {
        RefreshEventCache();
        BuildClipsList();
        RebuildTabs();
    }

    void OnFocus() => RefreshEventCache();
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
        DrawCreatorPanel(w, h);
        GUILayout.EndArea();
    }

    // =========================================================================
    // Creator Panel
    // =========================================================================

    void DrawCreatorPanel(float w, float h)
    {
        DrawPanelHeader("AUDIO CREATOR", "Create new sound", w, HEADER_HEIGHT);

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
        creator_category = Mathf.Clamp(creator_category, 0, tabLabels.Length - 1);
        creator_category = DrawCategoryTabs(tabRect, creator_category, tabLabels, tabCategories);
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

        // ── Tag field ─────────────────────────────────────────────────────
        SectionLabel("Tag");
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        creator_tag = EditorGUILayout.TextField(creator_tag, GUILayout.Width(innerW - 12));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        // ── Clips list ────────────────────────────────────────────────────
        SectionLabel("Audio Clips");
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        GUILayout.BeginVertical(GUILayout.Width(innerW - 12));
        if (creator_clipsList != null)
            creator_clipsList.DoLayoutList();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        // ── Compression Preset ────────────────────────────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        bool foldOpen = EditorPrefs.GetBool("AudioCreator_PresetFold", true);
        bool newOpen  = EditorGUILayout.Foldout(foldOpen, "  Compression Preset", true, FoldoutStyle());
        if (newOpen != foldOpen) EditorPrefs.SetBool("AudioCreator_PresetFold", newOpen);
        GUILayout.EndHorizontal();

        if (newOpen)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.BeginVertical(GUILayout.Width(innerW - 20));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset:", DimLabelStyle(), GUILayout.Width(54));
            creator_preset = EditorGUILayout.Popup(creator_preset, PRESET_NAMES);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            Rect descRect = GUILayoutUtility.GetRect(innerW - 20, 34);
            EditorGUI.DrawRect(descRect, new Color(0.13f, 0.13f, 0.16f, 1f));
            GUI.Label(new Rect(descRect.x + 6, descRect.y + 4, descRect.width - 12, descRect.height - 8),
                PRESET_DESCRIPTIONS[creator_preset], MiniDescStyle());

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Force to Mono:", DimLabelStyle(), GUILayout.Width(90));
            creator_forceMono = EditorGUILayout.Toggle(creator_forceMono, GUILayout.Width(18));
            GUILayout.EndHorizontal();

            Rect noteRect = GUILayoutUtility.GetRect(innerW - 20, 26);
            EditorGUI.DrawRect(noteRect, new Color(0.13f, 0.13f, 0.16f, 1f));
            GUI.Label(new Rect(noteRect.x + 6, noteRect.y + 4, noteRect.width - 12, noteRect.height - 8),
                "Reduces memory footprint. Not recommended for stereo BGM.", MiniDescStyle());

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        // ── Create button ─────────────────────────────────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = creator_category < tabCategories.Length
            ? CategoryColor(tabCategories[creator_category]) : Color.gray;
        if (GUILayout.Button(
            $"CREATE {(creator_category < tabCategories.Length ? tabCategories[creator_category] : SoundCategory.SFX).ToString().ToUpper()} EVENT",
            CreateBtnStyle(), GUILayout.Width(innerW - 12), GUILayout.Height(34)))
        {
            DoCreateEvent();
        }
        GUI.backgroundColor = old;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void DoCreateEvent()
    {
        if (string.IsNullOrWhiteSpace(creator_tag))
        {
            EditorUtility.DisplayDialog("Audio Creator", "Please enter a Tag before creating.", "OK");
            return;
        }

        string folder = "Assets/Audio/Events";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        int existing = allEvents.Count(e => e != null &&
            e.name.StartsWith(creator_tag + " (", System.StringComparison.OrdinalIgnoreCase));
        string assetName = existing == 0 ? creator_tag : $"{creator_tag} ({existing + 1})";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");

        var evt = CreateInstance<AudioEvent>();
        evt.category = creator_category < tabCategories.Length
            ? tabCategories[creator_category] : SoundCategory.SFX;

        if (creator_clips != null && creator_clips.Count > 0)
            evt.clips = creator_clips.Where(c => c != null).ToArray();

        AssetDatabase.CreateAsset(evt, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Auto-register into AudioDatabase
        var db = FindAudioDatabase();
        if (db != null)
        {
            ArrayUtility.Add(ref db.entries, new AudioDatabaseEntry { key = assetName, audioEvent = evt });
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        EditorGUIUtility.PingObject(evt);
        RefreshEventCache();
        Debug.Log($"[Audio Creator] Created AudioEvent: {path}");

        // Reset form so the next clip doesn't accidentally inherit the same clips
        creator_tag = "";
        creator_clips.Clear();
        GUI.FocusControl(null);
        Repaint();
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
            GUI.DrawTexture(new Rect(cx - iconSize * 0.5f, cy - iconSize * 0.55f, iconSize, iconSize), headerIcon, ScaleMode.ScaleToFit);
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
                tabLabels     = labels.ToArray();
                tabCategories = cats.ToArray();
                tabsFromConfig = true;
                return;
            }
        }
        // Fallback to hardcoded defaults
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

    void SectionLabel(string text)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        GUILayout.Label(text, SectionLabelStyle());
        GUILayout.EndHorizontal();
        GUILayout.Space(2);
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

    AudioDatabase FindAudioDatabase()
    {
        var guids = AssetDatabase.FindAssets("t:AudioDatabase");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<AudioDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    void BuildClipsList()
    {
        // Single List<AudioClip> is the one backing store — no array/List desync.
        creator_clipsList = new ReorderableList(
            creator_clips, typeof(AudioClip), true, true, true, true);

        creator_clipsList.drawHeaderCallback = rect =>
            GUI.Label(rect, "Audio Clips  (drag to reorder)", SectionLabelStyle());

        creator_clipsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            rect.y += 2; rect.height -= 4;
            if (index < creator_clips.Count)
                creator_clips[index] = (AudioClip)EditorGUI.ObjectField(
                    rect, creator_clips[index], typeof(AudioClip), false);
        };

        creator_clipsList.onAddCallback    = list => creator_clips.Add(null);

        creator_clipsList.onRemoveCallback = list =>
        {
            if (list.index >= 0 && list.index < creator_clips.Count)
                creator_clips.RemoveAt(list.index);
        };
    }

    // =========================================================================
    // GUIStyles
    // =========================================================================

    GUIStyle _titleStyle, _subtitleStyle, _sectionLabelStyle;
    GUIStyle _dimLabelStyle, _iconStyle, _activeTabStyle, _inactiveTabStyle;
    GUIStyle _createBtnStyle, _miniDescStyle, _foldoutStyle;

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
    GUIStyle SectionLabelStyle()
    {
        if (_sectionLabelStyle != null) return _sectionLabelStyle;
        return _sectionLabelStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = COL_TEXT_DIM } };
    }
    GUIStyle DimLabelStyle()
    {
        if (_dimLabelStyle != null) return _dimLabelStyle;
        return _dimLabelStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 10, normal = { textColor = COL_TEXT_DIM } };
    }
    GUIStyle IconStyle()
    {
        if (_iconStyle != null) return _iconStyle;
        return _iconStyle = new GUIStyle(EditorStyles.label)
        { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.35f, 0.65f, 1f) } };
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
    GUIStyle CreateBtnStyle()
    {
        if (_createBtnStyle != null) return _createBtnStyle;
        return _createBtnStyle = new GUIStyle(EditorStyles.miniButton)
        { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = Color.white }, hover = { textColor = Color.white }, active = { textColor = Color.white } };
    }
    GUIStyle MiniDescStyle()
    {
        if (_miniDescStyle != null) return _miniDescStyle;
        return _miniDescStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        { fontSize = 9, normal = { textColor = COL_TEXT_DIM } };
    }
    GUIStyle FoldoutStyle()
    {
        if (_foldoutStyle != null) return _foldoutStyle;
        return _foldoutStyle = new GUIStyle(EditorStyles.foldout)
        { fontStyle = FontStyle.Bold,
          normal = { textColor = COL_TEXT_BRIGHT }, onNormal = { textColor = COL_TEXT_BRIGHT } };
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