#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// Audio Output Manager — LumberCat
///
/// Visual dashboard for AudioMixer routing, volume levels, mute/solo,
/// live voice counts, exposed parameter mapping, and snapshot management.
///
/// Fully data-driven from an AudioChannelConfig ScriptableObject asset.
/// Add, remove, or reorder channels in the config and this window updates
/// immediately — no code changes required.
///
/// Open via: Tools → Audio → Output Manager
/// </summary>
public class AudioOutputManagerWindow : EditorWindow
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
    static readonly Color COL_CARD_BG      = new Color(0.20f, 0.20f, 0.24f, 1f);
    static readonly Color COL_CARD_BORDER  = new Color(0.28f, 0.28f, 0.33f, 1f);
    static readonly Color COL_ROW_BG       = new Color(0.19f, 0.19f, 0.22f, 1f);
    static readonly Color COL_SECTION_BG   = new Color(0.15f, 0.15f, 0.18f, 1f);

    static readonly Color COL_MUTE_ON      = new Color(0.80f, 0.30f, 0.20f, 1f);
    static readonly Color COL_SOLO_ON      = new Color(0.85f, 0.75f, 0.10f, 1f);
    static readonly Color COL_BTN_DARK     = new Color(0.24f, 0.24f, 0.28f, 1f);
    static readonly Color COL_BTN_GREEN    = new Color(0.20f, 0.55f, 0.28f, 1f);
    static readonly Color COL_BTN_RED      = new Color(0.65f, 0.20f, 0.20f, 1f);
    static readonly Color COL_ACCENT_BAR   = new Color(0.25f, 0.50f, 0.85f, 1f);

    static readonly Color COL_TEXT_DIM     = new Color(0.55f, 0.55f, 0.60f, 1f);
    static readonly Color COL_TEXT_BRIGHT  = new Color(0.92f, 0.92f, 0.95f, 1f);
    static readonly Color COL_TEXT_WARN    = new Color(0.95f, 0.75f, 0.25f, 1f);

    const float HEADER_HEIGHT   = 72f;
    const float CARD_HEIGHT     = 210f;
    const float CARD_GAP        = 8f;
    const float CARD_MIN_W      = 175f;
    const float SECTION_LABEL_H = 22f;
    const float ROW_H           = 24f;

    // =========================================================================
    // Per-channel editor state (resized whenever channel count changes)
    // =========================================================================

    bool[]  muteState        = new bool[0];
    bool[]  soloState        = new bool[0];
    float[] volSlider        = new float[0];
    int     lastChannelCount = -1;

    // =========================================================================
    // UI state
    // =========================================================================

    bool    showSnapshots    = true;
    bool    showBulkReassign = false;
    bool    showParamMapping = true;
    string  newSnapshotName  = "";
    Vector2 mainScroll       = Vector2.zero;
    Vector2 scrollSnapshots  = Vector2.zero;

    // =========================================================================
    // Cached references
    // =========================================================================

    AudioManager         cachedManager;
    AudioChannelConfig   cachedConfig;
    double               lastManagerScan = -1;
    double               lastRepaint     = -1;

    // =========================================================================
    // Window lifecycle
    // =========================================================================

    [MenuItem("Tools/Audio/Output Manager")]
    public static void OpenWindow()
    {
        var w = GetWindow<AudioOutputManagerWindow>("Output Manager");
        w.minSize = new Vector2(560, 600);
        w.Show();
    }

    void OnEnable()
    {
        ScanForManager();
        SyncConfig();
    }

    void OnFocus() => ScanForManager();

    void OnInspectorUpdate()
    {
        if (EditorApplication.timeSinceStartup - lastRepaint > 0.1)
        {
            Repaint();
            lastRepaint = EditorApplication.timeSinceStartup;
        }
    }

    // =========================================================================
    // OnGUI entry
    // =========================================================================

    void OnGUI()
    {
        if (EditorApplication.timeSinceStartup - lastManagerScan > 3.0)
            ScanForManager();

        SyncConfig();

        float w = position.width;
        float h = position.height;

        EditorGUI.DrawRect(new Rect(0, 0, w, h), COL_PANEL_BG);
        DrawHeader(w, HEADER_HEIGHT);

        GUILayout.BeginArea(new Rect(0, HEADER_HEIGHT, w, h - HEADER_HEIGHT));
        mainScroll = GUILayout.BeginScrollView(mainScroll,
            GUIStyle.none, GUI.skin.verticalScrollbar);

        GUILayout.Space(10);

        if (cachedConfig == null)
            DrawNoConfigWarning(w);
        else
        {
            DrawChannelCards(w);
            GUILayout.Space(10);
            DrawParamMappingSection(w);
            GUILayout.Space(10);
            DrawSnapshotSection(w);
            GUILayout.Space(10);
            DrawBulkReassignSection(w);
        }

        GUILayout.Space(16);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // Header
    // =========================================================================

    void DrawHeader(float w, float h)
    {
        EditorGUI.DrawRect(new Rect(0, 0, w, h), COL_HEADER_BG);
        EditorGUI.DrawRect(new Rect(0, h - 2, w, 2), COL_ACCENT_BAR);

        float iconSize = 50f, cx = 16f;
        float cy = h * 0.5f - iconSize * 0.55f;
        var icon = HeaderIcon;
        if (icon != null)
            GUI.DrawTexture(new Rect(cx, cy, iconSize, iconSize), icon, ScaleMode.ScaleToFit);
        else
        {
            EditorGUI.DrawRect(new Rect(cx, cy, iconSize, iconSize), new Color(0.22f, 0.22f, 0.28f));
            GUI.Label(new Rect(cx, cy, iconSize, iconSize), "♫", HeaderIconFallbackStyle());
        }

        float textX = cx + iconSize + 10f;
        GUI.Label(new Rect(textX, cy + 2,  w - textX - 110, 26), "OUTPUT MANAGER",                    TitleStyle());
        GUI.Label(new Rect(textX, cy + 28, w - textX - 110, 18), "Mixer routing · volumes · snapshots", SubtitleStyle());

        if (Application.isPlaying)
        {
            Rect badge = new Rect(w - 84, h * 0.5f - 11, 76, 22);
            EditorGUI.DrawRect(badge, new Color(0.20f, 0.50f, 0.22f));
            GUI.Label(badge, "● LIVE", LiveBadgeStyle());
        }
        else if (cachedManager == null)
        {
            Rect badge = new Rect(w - 108, h * 0.5f - 11, 100, 22);
            EditorGUI.DrawRect(badge, new Color(0.45f, 0.28f, 0.08f));
            GUI.Label(badge, "⚠ No Manager", WarningBadgeStyle());
        }
        else if (cachedConfig == null)
        {
            Rect badge = new Rect(w - 104, h * 0.5f - 11, 96, 22);
            EditorGUI.DrawRect(badge, new Color(0.45f, 0.28f, 0.08f));
            GUI.Label(badge, "⚠ No Config", WarningBadgeStyle());
        }
    }

    // =========================================================================
    // No-config warning + Create button
    // =========================================================================

    void DrawNoConfigWarning(float w)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        GUILayout.BeginVertical();
        EditorGUILayout.HelpBox(
            "No AudioChannelConfig assigned to AudioManager.\n\n" +
            "1. Create an asset: Assets → Create → Audio → Channel Config\n" +
            "2. Assign it to AudioManager.channelConfig in the Inspector\n" +
            "3. Come back here — all your channels will appear.",
            MessageType.Warning);

        GUILayout.Space(8);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = COL_BTN_GREEN;
        if (GUILayout.Button("Create Channel Config Asset", GUILayout.Height(28)))
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create AudioChannelConfig", "AudioChannelConfig", "asset",
                "Choose where to save the Channel Config asset");
            if (!string.IsNullOrEmpty(path))
            {
                var asset = ScriptableObject.CreateInstance<AudioChannelConfig>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(asset);
                if (cachedManager != null)
                {
                    Undo.RecordObject(cachedManager, "Assign AudioChannelConfig");
                    cachedManager.channelConfig = asset;
                    EditorUtility.SetDirty(cachedManager);
                    cachedConfig = asset;
                    SyncConfig(force: true);
                }
            }
        }
        GUI.backgroundColor = old;
        GUILayout.EndVertical();
        GUILayout.Space(12);
        GUILayout.EndHorizontal();
    }

    // =========================================================================
    // Channel cards
    // =========================================================================

    void DrawChannelCards(float w)
    {
        // Section header row with + Add Channel button
        Rect sr = GUILayoutUtility.GetRect(w - 20, SECTION_LABEL_H);
        sr.x += 10; sr.width = w - 20;
        EditorGUI.DrawRect(sr, COL_SECTION_BG);
        EditorGUI.DrawRect(new Rect(sr.x, sr.y, 3, sr.height), COL_ACCENT_BAR);
        GUI.Label(new Rect(sr.x + 10, sr.y + 3, sr.width - 120, sr.height), "OUTPUT CHANNELS", SectionTitleStyle());

        Color oldbg = GUI.backgroundColor;
        GUI.backgroundColor = COL_BTN_GREEN;
        if (GUI.Button(new Rect(sr.x + sr.width - 114, sr.y + 2, 110, 18), "+ Add Channel", SmallBtnStyle()))
            AddChannel();
        GUI.backgroundColor = oldbg;
        GUILayout.Space(8);

        var channels = cachedConfig.channels;
        if (channels == null || channels.Count == 0)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("No channels defined. Press + Add Channel to create one.", DimLabelStyle());
            GUILayout.EndHorizontal();
            return;
        }

        // Responsive wrapping grid
        float innerW = w - 20;
        int   total  = channels.Count;
        int   cols   = total;
        while (cols > 1 && (innerW - CARD_GAP * (cols - 1)) / cols < CARD_MIN_W)
            cols--;
        float cardW = (innerW - CARD_GAP * (cols - 1)) / cols;
        int   rows  = Mathf.CeilToInt((float)total / cols);

        for (int row = 0; row < rows; row++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            for (int col = 0; col < cols; col++)
            {
                int idx = row * cols + col;
                if (idx < total)
                    DrawChannelCard(idx, cardW, CARD_HEIGHT);
                else
                    GUILayout.Space(cardW);  // empty slot — keeps last row left-aligned

                if (col < cols - 1) GUILayout.Space(CARD_GAP);
            }
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            if (row < rows - 1) GUILayout.Space(CARD_GAP);
        }
        GUILayout.Space(CARD_GAP);
    }

    void DrawChannelCard(int idx, float cardW, float cardH)
    {
        var channels = cachedConfig.channels;
        if (idx >= channels.Count) return;
        var ch = channels[idx];

        Rect card = GUILayoutUtility.GetRect(cardW, cardH);
        float ox = card.x, oy = card.y, iw = cardW;

        EditorGUI.DrawRect(card, COL_CARD_BG);
        DrawBorder(card, COL_CARD_BORDER);
        // Accent stripe
        EditorGUI.DrawRect(new Rect(ox, oy, iw, 3), ch.accentColor);

        // ── Row 1: icon + label + delete button ────────────────────────────
        float ry = oy + 10;
        GUI.Label(new Rect(ox + 8,  ry, 22, 22), ch.icon, CardIconStyle(ch.accentColor));
        GUI.Label(new Rect(ox + 32, ry + 2, iw - 72, 18), ch.label, CardLabelStyle());

        // Voice count (play mode, non-master)
        if (Application.isPlaying && !ch.isMaster)
        {
            var sc = ChannelCategoryToSoundCategory(ch.category);
            if (sc.HasValue)
            {
                int voices = CountLiveVoices(sc.Value);
                var oc = GUI.color;
                GUI.color = voices > 0 ? ch.accentColor : COL_TEXT_DIM;
                GUI.Label(new Rect(ox + iw - 38, ry + 2, 34, 18), $"{voices}v", VoiceCountStyle());
                GUI.color = oc;
            }
        }

        // × delete button (top-right corner)
        Color ob = GUI.backgroundColor;
        GUI.backgroundColor = COL_BTN_RED;
        if (GUI.Button(new Rect(ox + iw - 22, oy + 4, 18, 14), "×", DeleteBtnStyle()))
        {
            bool confirmed = !ch.isMaster || EditorUtility.DisplayDialog(
                "Remove Master Channel",
                $"'{ch.label}' is the Master channel. Remove it anyway?",
                "Remove", "Cancel");
            GUI.backgroundColor = ob;
            if (confirmed) { RemoveChannel(idx); return; }
        }
        GUI.backgroundColor = ob;

        // ── Row 2: Group / Mixer field ──────────────────────────────────────
        ry += 32;
        float labelW = 46f;
        float fieldX = ox + 8 + labelW + 4;
        float fieldW = iw - 8 - labelW - 4 - 8;

        GUI.Label(new Rect(ox + 8, ry + 2, labelW, 16), ch.isMaster ? "Mixer" : "Group", DimLabelStyle());

        if (ch.isMaster)
        {
            EditorGUI.ObjectField(new Rect(fieldX, ry, fieldW, 18),
                GetMixerFromConfig(), typeof(AudioMixer), false);
        }
        else
        {
            var newGroup = (AudioMixerGroup)EditorGUI.ObjectField(
                new Rect(fieldX, ry, fieldW, 18), ch.mixerGroup, typeof(AudioMixerGroup), false);
            if (newGroup != ch.mixerGroup)
            {
                Undo.RecordObject(cachedConfig, $"Set {ch.label} Group");
                ch.mixerGroup = newGroup;
                EditorUtility.SetDirty(cachedConfig);
            }
        }

        // ── Row 3: Channel label text field ────────────────────────────────
        ry += 26;
        GUI.Label(new Rect(ox + 8, ry + 2, labelW, 16), "Name", DimLabelStyle());
        string newLabel = EditorGUI.TextField(new Rect(fieldX, ry, fieldW, 17), ch.label, ParamFieldStyle());
        if (newLabel != ch.label)
        {
            Undo.RecordObject(cachedConfig, "Rename Channel");
            ch.label = newLabel;
            EditorUtility.SetDirty(cachedConfig);
        }

        // ── Row 4: Volume slider ────────────────────────────────────────────
        ry += 26;
        if (idx >= volSlider.Length) return;

        float pctW    = 34f;
        float sliderX = ox + 8 + labelW + 4;
        float sliderW = iw - 8 - labelW - 4 - pctW - 4 - 8;

        GUI.Label(new Rect(ox + 8, ry + 2, labelW, 16), "Volume", DimLabelStyle());

        float newVol = GUI.HorizontalSlider(new Rect(sliderX, ry + 5, sliderW, 14), volSlider[idx], 0f, 1f);
        if (!Mathf.Approximately(newVol, volSlider[idx]))
        {
            volSlider[idx] = newVol;
            ApplyVolume(idx, newVol);
        }
        GUI.Label(new Rect(sliderX + sliderW + 4, ry + 2, pctW, 16),
            $"{Mathf.RoundToInt(volSlider[idx] * 100)}%", DimLabelStyle());

        // ── Row 5: fill bar ─────────────────────────────────────────────────
        ry += 26;
        float barX = ox + 8 + labelW + 4;
        float barW = iw - 8 - labelW - 4 - 8;
        EditorGUI.DrawRect(new Rect(barX, ry, barW, 8), new Color(0.11f, 0.11f, 0.13f));
        EditorGUI.DrawRect(new Rect(barX, ry, barW * volSlider[idx], 8),
            Color.Lerp(ch.accentColor, Color.white, 0.15f));

        // ── Row 6: MUTE · SOLO · ↺ ─────────────────────────────────────────
        ry += 18;
        float btnW = 46f, btnH = 22f, btnGap = 6f;
        ob = GUI.backgroundColor;

        GUI.backgroundColor = muteState[idx] ? COL_MUTE_ON : COL_BTN_DARK;
        if (GUI.Button(new Rect(ox + 8, ry, btnW, btnH), "MUTE", MuteSoloBtnStyle()))
        { muteState[idx] = !muteState[idx]; ApplyMute(idx); }

        GUI.backgroundColor = soloState[idx] ? COL_SOLO_ON : COL_BTN_DARK;
        if (GUI.Button(new Rect(ox + 8 + btnW + btnGap, ry, btnW, btnH), "SOLO", MuteSoloBtnStyle()))
        { soloState[idx] = !soloState[idx]; ApplySolo(idx); }

        GUI.backgroundColor = COL_BTN_DARK;
        if (GUI.Button(new Rect(ox + iw - 8 - 30, ry, 30, btnH), "↺", MuteSoloBtnStyle()))
            ResetChannel(idx);

        GUI.backgroundColor = ob;
    }

    // =========================================================================
    // Param mapping section
    // =========================================================================

    void DrawParamMappingSection(float w)
    {
        showParamMapping = DrawFoldoutHeader("EXPOSED PARAMETER MAPPING", showParamMapping, w);
        if (!showParamMapping) return;

        var channels = cachedConfig.channels;
        float innerW = w - 20;
        float labelW = 80, fieldW = innerW - labelW - 120, hintW = 110;

        EditorGUI.DrawRect(new Rect(10, GUILayoutUtility.GetRect(innerW, 0).y, innerW, 1), COL_CARD_BORDER);
        GUILayout.Space(4);

        var mixer = GetMixerFromConfig();

        for (int i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            Rect row = GUILayoutUtility.GetRect(innerW, ROW_H);
            row.x += 10; row.width = innerW;
            EditorGUI.DrawRect(row, i % 2 == 0 ? COL_ROW_BG : COL_CARD_BG);

            EditorGUI.DrawRect(new Rect(row.x + 4, row.y + 7, 6, 10), ch.accentColor);
            GUI.Label(new Rect(row.x + 16, row.y + 4, labelW, 16), ch.label, DimLabelStyle());

            string current = ch.volumeParam ?? "";
            string newVal  = EditorGUI.TextField(
                new Rect(row.x + labelW + 20, row.y + 3, fieldW, 17), current, ParamFieldStyle());
            if (newVal != current)
            {
                Undo.RecordObject(cachedConfig, $"Edit {ch.label} Param");
                ch.volumeParam = newVal;
                EditorUtility.SetDirty(cachedConfig);
            }

            bool   found     = mixer != null && !string.IsNullOrEmpty(current) && mixer.GetFloat(current, out _);
            Color  statusCol = string.IsNullOrEmpty(current) ? COL_TEXT_DIM
                             : found ? new Color(0.25f, 0.65f, 0.35f) : COL_TEXT_WARN;
            string statusTxt = string.IsNullOrEmpty(current) ? "not set"
                             : found ? "✓ found" : "✗ not exposed";
            var oc = GUI.color;
            GUI.color = statusCol;
            GUI.Label(new Rect(row.x + labelW + fieldW + 28, row.y + 4, hintW, 16), statusTxt, DimLabelStyle());
            GUI.color = oc;
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Each parameter must be exposed in your AudioMixer asset.\n" +
            "Right-click a volume knob → Expose Parameter, then match the name here.",
            MessageType.Info);
        GUILayout.Space(12);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    // =========================================================================
    // Snapshot section
    // =========================================================================

    void DrawSnapshotSection(float w)
    {
        showSnapshots = DrawFoldoutHeader("MIXER SNAPSHOTS", showSnapshots, w);
        if (!showSnapshots) return;

        float innerW = w - 20;
        var mixer = GetMixerFromConfig();
        if (mixer == null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            EditorGUILayout.HelpBox("No AudioMixer found. Assign mixer groups to your channels first.", MessageType.Warning);
            GUILayout.Space(12);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            return;
        }

        var snapshots = GetMixerSnapshots(mixer);
        if (snapshots.Count > 0)
        {
            scrollSnapshots = GUILayout.BeginScrollView(scrollSnapshots,
                GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Height(Mathf.Min(snapshots.Count * ROW_H + 4, 160)));

            for (int i = 0; i < snapshots.Count; i++)
            {
                var snap = snapshots[i];
                Rect row = GUILayoutUtility.GetRect(innerW, ROW_H);
                row.x += 10; row.width = innerW;
                EditorGUI.DrawRect(row, i % 2 == 0 ? COL_ROW_BG : COL_CARD_BG);
                GUI.Label(new Rect(row.x + 10, row.y + 4, innerW - 160, 16), snap.name, RowLabelStyle());

                Color ob = GUI.backgroundColor;
                GUI.backgroundColor = Application.isPlaying ? COL_BTN_GREEN : COL_BTN_DARK;
                if (GUI.Button(new Rect(row.x + innerW - 150, row.y + 2, 70, 19), "▶ Apply", SmallBtnStyle()))
                {
                    if (Application.isPlaying) snap.TransitionTo(0.5f);
                    else Debug.Log($"[Output Manager] Snapshot '{snap.name}' — enter Play mode to apply.");
                }
                GUI.backgroundColor = COL_BTN_DARK;
                if (GUI.Button(new Rect(row.x + innerW - 74, row.y + 2, 60, 19), "Select", SmallBtnStyle()))
                    EditorGUIUtility.PingObject(snap);
                GUI.backgroundColor = ob;
            }
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("No snapshots found in mixer.", DimLabelStyle());
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        GUILayout.Label("New snapshot:", DimLabelStyle(), GUILayout.Width(90));
        newSnapshotName = EditorGUILayout.TextField(newSnapshotName, GUILayout.Width(180));
        GUILayout.Space(6);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = string.IsNullOrWhiteSpace(newSnapshotName) ? COL_BTN_DARK : COL_BTN_GREEN;
        if (GUILayout.Button("Create", GUILayout.Width(64), GUILayout.Height(19)))
        {
            if (!string.IsNullOrWhiteSpace(newSnapshotName))
            {
                CreateSnapshot(mixer, newSnapshotName.Trim());
                newSnapshotName = "";
                GUI.FocusControl(null);
            }
        }
        GUI.backgroundColor = old;
        GUILayout.FlexibleSpace();
        GUILayout.Space(12);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Snapshots capture all mixer parameter values at a point in time.\n" +
            "Use snap.TransitionTo(seconds) at runtime to blend between states.",
            MessageType.Info);
        GUILayout.Space(12);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    // =========================================================================
    // Add / Remove channel
    // =========================================================================

    void AddChannel()
    {
        if (cachedConfig == null) return;
        Undo.RecordObject(cachedConfig, "Add Audio Channel");

        Color[] palette =
        {
            new Color(0.30f, 0.55f, 0.90f), new Color(0.25f, 0.65f, 0.35f),
            new Color(0.90f, 0.75f, 0.20f), new Color(0.85f, 0.40f, 0.25f),
            new Color(0.70f, 0.50f, 0.90f), new Color(0.30f, 0.75f, 0.80f),
            new Color(0.80f, 0.35f, 0.65f), new Color(0.60f, 0.80f, 0.30f),
        };

        cachedConfig.channels.Add(new AudioChannelConfig.ChannelEntry
        {
            label       = "New Channel",
            isMaster    = false,
            mixerGroup  = null,
            volumeParam = "NewVolume",
            category    = ChannelCategory.SFX,
            accentColor = palette[cachedConfig.channels.Count % palette.Length],
            icon        = "♬",
        });

        EditorUtility.SetDirty(cachedConfig);
        SyncConfig(force: true);
    }

    void RemoveChannel(int idx)
    {
        if (cachedConfig == null || idx < 0 || idx >= cachedConfig.channels.Count) return;
        Undo.RecordObject(cachedConfig, $"Remove Channel '{cachedConfig.channels[idx].label}'");
        cachedConfig.channels.RemoveAt(idx);
        EditorUtility.SetDirty(cachedConfig);
        SyncConfig(force: true);
    }

    // =========================================================================
    // Config sync — resize per-channel arrays when channel count changes
    // =========================================================================

    void SyncConfig(bool force = false)
    {
        if (cachedManager != null && cachedConfig != cachedManager.channelConfig)
            cachedConfig = cachedManager.channelConfig;

        if (cachedConfig == null) return;

        int count = cachedConfig.channels?.Count ?? 0;
        if (!force && count == lastChannelCount) return;

        lastChannelCount = count;

        bool[]  newMute = new bool[count];
        bool[]  newSolo = new bool[count];
        float[] newVol  = new float[count];

        int keep = Mathf.Min(count, muteState.Length);
        for (int i = 0; i < keep; i++)
        {
            newMute[i] = muteState[i];
            newSolo[i] = soloState[i];
            newVol[i]  = volSlider[i];
        }

        var mixer = GetMixerFromConfig();
        for (int i = keep; i < count; i++)
        {
            newVol[i] = 1f;
            var ch = cachedConfig.channels[i];
            if (mixer != null && !string.IsNullOrEmpty(ch.volumeParam) && mixer.GetFloat(ch.volumeParam, out float db))
                newVol[i] = Mathf.Clamp01(DbToLinear(db));
        }

        muteState = newMute;
        soloState = newSolo;
        volSlider = newVol;
    }

    // =========================================================================
    // Bulk mixer group reassignment
    // =========================================================================

    AudioMixerGroup _bulkTargetGroup;
    ChannelCategory _bulkTargetCategory = ChannelCategory.SFX;

    void DrawBulkReassignSection(float w)
    {
        showBulkReassign = DrawFoldoutHeader("BULK MIXER GROUP REASSIGN", showBulkReassign, w);
        if (!showBulkReassign) return;

        float innerW = w - 20;

        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Reassigns all AudioEvents of a given category to a new AudioMixerGroup in one click.\n" +
            "Use this after renaming or restructuring your AudioMixer.",
            MessageType.Info);
        GUILayout.Space(12);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        // Category picker
        Rect row1 = GUILayoutUtility.GetRect(innerW, ROW_H);
        row1.x += 10; row1.width = innerW;
        EditorGUI.DrawRect(row1, COL_ROW_BG);
        GUI.Label(new Rect(row1.x + 8, row1.y + 4, 70, 16), "Category", DimLabelStyle());
        _bulkTargetCategory = (ChannelCategory)EditorGUI.EnumPopup(
            new Rect(row1.x + 84, row1.y + 3, 120, 17), _bulkTargetCategory);

        // New group picker
        Rect row2 = GUILayoutUtility.GetRect(innerW, ROW_H);
        row2.x += 10; row2.width = innerW;
        EditorGUI.DrawRect(row2, COL_CARD_BG);
        GUI.Label(new Rect(row2.x + 8, row2.y + 4, 70, 16), "New Group", DimLabelStyle());
        _bulkTargetGroup = (AudioMixerGroup)EditorGUI.ObjectField(
            new Rect(row2.x + 84, row2.y + 3, innerW - 96, 17),
            _bulkTargetGroup, typeof(AudioMixerGroup), false);

        GUILayout.Space(6);

        // Apply button
        bool canApply = _bulkTargetGroup != null && _bulkTargetCategory != ChannelCategory.None;
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        Color ob = GUI.backgroundColor;
        GUI.backgroundColor = canApply ? COL_BTN_GREEN : COL_BTN_DARK;
        if (GUILayout.Button("Reassign All AudioEvents in Category", GUILayout.Height(26)) && canApply)
            DoBulkReassign(_bulkTargetCategory, _bulkTargetGroup);
        GUI.backgroundColor = ob;
        GUILayout.Space(12);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    void DoBulkReassign(ChannelCategory category, AudioMixerGroup newGroup)
    {
        var sc = ChannelCategoryToSoundCategory(category);
        if (!sc.HasValue) return;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioEvent");
        int count = 0;
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var evt = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioEvent>(path);
            if (evt == null || evt.category != sc.Value) continue;

            // AudioEvent stores outputAudioMixerGroup if it has one
            var so = new UnityEditor.SerializedObject(evt);
            var prop = so.FindProperty("outputAudioMixerGroup");
            if (prop != null)
            {
                so.Update();
                prop.objectReferenceValue = newGroup;
                so.ApplyModifiedProperties();
                count++;
            }
        }

        // Also update the channel config entry
        if (cachedConfig != null)
        {
            Undo.RecordObject(cachedConfig, "Bulk Reassign Mixer Group");
            foreach (var ch in cachedConfig.channels)
                if (!ch.isMaster && ch.category == category)
                    ch.mixerGroup = newGroup;
            EditorUtility.SetDirty(cachedConfig);
        }

        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[Output Manager] Bulk reassign: {count} AudioEvent(s) in {category} → {newGroup?.name ?? "null"}");
        EditorUtility.DisplayDialog("Bulk Reassign Complete",
            $"{count} AudioEvent(s) updated to use '{newGroup?.name}'.", "OK");
    }

    // =========================================================================
    // Section headers
    // =========================================================================

    bool DrawFoldoutHeader(string title, bool open, float w)
    {
        Rect r = GUILayoutUtility.GetRect(w - 20, SECTION_LABEL_H);
        r.x += 10; r.width = w - 20;
        EditorGUI.DrawRect(r, COL_SECTION_BG);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), COL_ACCENT_BAR);
        GUI.Label(new Rect(r.x + 10, r.y + 3, r.width - 30, r.height), title, SectionTitleStyle());
        GUI.Label(new Rect(r.x + r.width - 22, r.y + 3, 20, r.height), open ? "▾" : "▸", SectionTitleStyle());
        if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
        { open = !open; Event.current.Use(); Repaint(); }
        GUILayout.Space(8);
        return open;
    }

    // =========================================================================
    // AudioManager scan
    // =========================================================================

    void ScanForManager()
    {
        lastManagerScan = EditorApplication.timeSinceStartup;
        cachedManager   = GameObject.FindFirstObjectByType<AudioManager>();
        if (cachedManager != null) cachedConfig = cachedManager.channelConfig;
    }

    // =========================================================================
    // Mixer / config helpers
    // =========================================================================

    AudioMixer GetMixerFromConfig() => cachedConfig?.GetMixer();

    List<AudioMixerSnapshot> GetMixerSnapshots(AudioMixer mixer)
    {
        var list = new List<AudioMixerSnapshot>();
        if (mixer == null) return list;
        var so   = new SerializedObject(mixer);
        var prop = so.FindProperty("m_Snapshots");
        if (prop != null && prop.isArray)
            for (int i = 0; i < prop.arraySize; i++)
            {
                var snap = prop.GetArrayElementAtIndex(i).objectReferenceValue as AudioMixerSnapshot;
                if (snap != null) list.Add(snap);
            }
        return list;
    }

    void CreateSnapshot(AudioMixer mixer, string name)
    {
        EditorUtility.DisplayDialog("Create Snapshot",
            $"To create snapshot '{name}':\n\n" +
            "1. Open the AudioMixer window\n" +
            "2. Click the Snapshots '+' button\n" +
            "3. Rename it to: " + name + "\n\n" +
            "Adjust your mixer values and they will be saved into that snapshot automatically.",
            "OK");
        EditorGUIUtility.PingObject(mixer);
    }

    // =========================================================================
    // Volume / Mute / Solo application
    // =========================================================================

    void ApplyVolume(int idx, float linear)
    {
        if (cachedConfig == null || idx >= cachedConfig.channels.Count) return;
        var ch = cachedConfig.channels[idx];
        var mx = GetMixerFromConfig();
        if (mx == null || string.IsNullOrEmpty(ch.volumeParam)) return;
        mx.SetFloat(ch.volumeParam, LinearToDb(linear));
    }

    void ApplyMute(int idx)
    {
        if (cachedConfig == null || idx >= cachedConfig.channels.Count) return;
        var ch = cachedConfig.channels[idx];
        var mx = GetMixerFromConfig();
        if (mx == null || string.IsNullOrEmpty(ch.volumeParam)) return;
        mx.SetFloat(ch.volumeParam, muteState[idx] ? -80f : LinearToDb(volSlider[idx]));
    }

    void ApplySolo(int idx)
    {
        if (cachedConfig == null) return;
        var mx = GetMixerFromConfig();
        if (mx == null) return;

        for (int i = 0; i < cachedConfig.channels.Count; i++)
        {
            if (cachedConfig.channels[i].isMaster) continue;
            string param = cachedConfig.channels[i].volumeParam ?? "";
            if (string.IsNullOrEmpty(param)) continue;
            bool shouldMute = soloState[idx] && i != idx;
            mx.SetFloat(param, shouldMute ? -80f : LinearToDb(volSlider[i]));
        }

        if (!soloState[idx])
            for (int i = 0; i < cachedConfig.channels.Count; i++)
            {
                if (cachedConfig.channels[i].isMaster) continue;
                string param = cachedConfig.channels[i].volumeParam ?? "";
                if (!string.IsNullOrEmpty(param))
                    mx.SetFloat(param, muteState[i] ? -80f : LinearToDb(volSlider[i]));
            }
    }

    void ResetChannel(int idx)
    {
        if (idx >= volSlider.Length) return;
        volSlider[idx] = 1f;
        muteState[idx] = false;
        soloState[idx] = false;
        ApplyVolume(idx, 1f);
    }

    // =========================================================================
    // Live voice count
    // =========================================================================

    int CountLiveVoices(SoundCategory category)
    {
        if (cachedManager == null || cachedManager.channelConfig == null) return 0;
        var field = typeof(AudioManager).GetField("activeSounds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return 0;
        var list = field.GetValue(cachedManager) as System.Collections.IList;
        if (list == null) return 0;

        var targetGroup = cachedManager.GetMixerGroup(category);
        if (targetGroup == null) return 0;

        int count = 0;
        foreach (var item in list)
        {
            var sf = item.GetType().GetField("source");
            if (sf?.GetValue(item) is AudioSource src && src != null
                && src.outputAudioMixerGroup == targetGroup)
                count++;
        }
        return count;
    }

    // =========================================================================
    // Utility
    // =========================================================================

    static SoundCategory? ChannelCategoryToSoundCategory(ChannelCategory cc)
        => cc switch
        {
            ChannelCategory.SFX   => SoundCategory.SFX,
            ChannelCategory.BGM   => SoundCategory.BGM,
            ChannelCategory.UI    => SoundCategory.UI,
            ChannelCategory.Voice => SoundCategory.Voice,
            _                                        => (SoundCategory?)null,
        };

    static float LinearToDb(float linear) => linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    static float DbToLinear(float db)     => db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);

    void DrawBorder(Rect r, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x,             r.y,              r.width, 1),  c);
        EditorGUI.DrawRect(new Rect(r.x,             r.y + r.height-1, r.width, 1),  c);
        EditorGUI.DrawRect(new Rect(r.x,             r.y,              1, r.height), c);
        EditorGUI.DrawRect(new Rect(r.x + r.width-1, r.y,              1, r.height), c);
    }

    // =========================================================================
    // GUIStyles
    // =========================================================================

    GUIStyle _titleStyle;
    GUIStyle TitleStyle() => _titleStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.75f, 0.25f) } };

    GUIStyle _subtitleStyle;
    GUIStyle SubtitleStyle() => _subtitleStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 10, normal = { textColor = COL_TEXT_DIM } };

    GUIStyle _sectionTitleStyle;
    GUIStyle SectionTitleStyle() => _sectionTitleStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = COL_TEXT_BRIGHT } };

    GUIStyle _cardLabelStyle;
    GUIStyle CardLabelStyle() => _cardLabelStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = COL_TEXT_BRIGHT } };

    GUIStyle CardIconStyle(Color accent) => new GUIStyle(EditorStyles.label)
        { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = accent } };

    GUIStyle _dimLabelStyle;
    GUIStyle DimLabelStyle() => _dimLabelStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 10, normal = { textColor = COL_TEXT_DIM } };

    GUIStyle _rowLabelStyle;
    GUIStyle RowLabelStyle() => _rowLabelStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 11, normal = { textColor = COL_TEXT_BRIGHT } };

    GUIStyle _voiceCountStyle;
    GUIStyle VoiceCountStyle() => _voiceCountStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

    GUIStyle _muteSoloBtnStyle;
    GUIStyle MuteSoloBtnStyle() => _muteSoloBtnStyle ??= new GUIStyle(GUI.skin.button)
        { fontSize = 10, fontStyle = FontStyle.Bold };

    GUIStyle _smallBtnStyle;
    GUIStyle SmallBtnStyle() => _smallBtnStyle ??= new GUIStyle(GUI.skin.button)
        { fontSize = 10 };

    GUIStyle _deleteBtnStyle;
    GUIStyle DeleteBtnStyle() => _deleteBtnStyle ??= new GUIStyle(GUI.skin.button)
        { fontSize = 9, fontStyle = FontStyle.Bold,
          normal = { textColor = Color.white }, padding = new RectOffset(0,0,0,0) };

    GUIStyle _paramFieldStyle;
    GUIStyle ParamFieldStyle() => _paramFieldStyle ??= new GUIStyle(EditorStyles.textField)
        { fontSize = 10 };

    GUIStyle _headerIconFallbackStyle;
    GUIStyle HeaderIconFallbackStyle() => _headerIconFallbackStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 22, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = new Color(0.55f, 0.55f, 0.65f) } };

    GUIStyle _liveBadgeStyle;
    GUIStyle LiveBadgeStyle() => _liveBadgeStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = new Color(0.60f, 1.00f, 0.65f) } };

    GUIStyle _warningBadgeStyle;
    GUIStyle WarningBadgeStyle() => _warningBadgeStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = new Color(1.00f, 0.75f, 0.30f) } };
}
#endif