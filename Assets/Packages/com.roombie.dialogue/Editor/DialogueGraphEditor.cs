using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System.Linq;
using System.IO;

public class DialogueGraphEditor : EditorWindow
{
    private string ScriptFolder =>
        Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)))
            .Replace('\\', '/');

    private DialogueGraphView _graphView;
    private MiniMap _miniMap;
    private string _currentFileName = "";
    private string _scopeName       = "";
    private Texture2D _icon;

    // Constants for Layout
    const float HEADER_HEIGHT   = 72f;
    const float TOOLBAR_HEIGHT  = 28f;
    const float TOOLBAR2_HEIGHT = 28f;
    const float TOP_HEIGHT      = HEADER_HEIGHT + TOOLBAR_HEIGHT + TOOLBAR2_HEIGHT;

    // Colors
    static readonly Color COL_HEADER_BG   = new Color(0.13f, 0.13f, 0.16f, 1f);
    static readonly Color COL_TOOLBAR_BG  = new Color(0.17f, 0.17f, 0.20f, 1f);
    static readonly Color COL_TOOLBAR2_BG = new Color(0.15f, 0.15f, 0.18f, 1f);
    static readonly Color COL_ACCENT_BAR  = new Color(0.25f, 0.50f, 0.85f, 1f);
    static readonly Color COL_TEXT_DIM    = new Color(0.55f, 0.55f, 0.60f, 1f);

    // Styles
    GUIStyle _titleStyle;
    GUIStyle TitleStyle() => _titleStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 16, fontStyle = FontStyle.Bold,
          normal = { textColor = new Color(1f, 0.75f, 0.25f) } };

    GUIStyle _subtitleStyle;
    GUIStyle SubtitleStyle() => _subtitleStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 10, normal = { textColor = COL_TEXT_DIM } };

    GUIStyle _iconFallbackStyle;
    GUIStyle IconFallbackStyle() => _iconFallbackStyle ??= new GUIStyle(EditorStyles.label)
        { fontSize = 22, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = new Color(0.55f, 0.55f, 0.65f) } };

    public static void OpenEditorWithAsset(DialogueContainer container)
    {
        var window = GetWindow<DialogueGraphEditor>();
        window.titleContent = new GUIContent("Dialogue Graph");
        window.minSize = new Vector2(800, 600);
        window.Show();

        window._currentFileName = container.name;
        window._scopeName       = container.scopeName;
        GraphSaveUtility.GetInstance(window._graphView).LoadGraph(container.name);
    }

    [MenuItem("Tools/Dialogue Graph Editor")]
    private static void OpenWindow()
    {
        var window = GetWindow<DialogueGraphEditor>();
        window.titleContent = new GUIContent("Dialogue Graph");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        _icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ScriptFolder}/Icons/RoombieEditorIcon.png");
        BuildGraphView();
    }

    private void OnDisable()
    {
        if (_graphView != null && _graphView.parent == rootVisualElement)
            rootVisualElement.Remove(_graphView);

        if (_miniMap != null && _miniMap.parent == _graphView)
            _graphView.Remove(_miniMap);
    }

    private void OnGUI()
    {
        float w = position.width;
        DrawHeader(w);
        DrawToolbar(w);
        DrawSecondaryToolbar(w);

        if (_graphView != null)
            _graphView.style.top = TOP_HEIGHT;
    }

    private void DrawHeader(float w)
    {
        EditorGUI.DrawRect(new Rect(0, 0, w, HEADER_HEIGHT), COL_HEADER_BG);
        EditorGUI.DrawRect(new Rect(0, HEADER_HEIGHT - 2, w, 2), COL_ACCENT_BAR);

        float iconSize = 50f, cx = 16f;
        float cy = HEADER_HEIGHT * 0.5f - iconSize * 0.55f;

        if (_icon != null)
            GUI.DrawTexture(new Rect(cx, cy, iconSize, iconSize), _icon, ScaleMode.ScaleToFit);
        else
        {
            EditorGUI.DrawRect(new Rect(cx, cy, iconSize, iconSize), new Color(0.22f, 0.22f, 0.28f));
            GUI.Label(new Rect(cx, cy, iconSize, iconSize), "✦", IconFallbackStyle());
        }

        float textX = cx + iconSize + 10f;
        GUI.Label(new Rect(textX, cy + 2,  w - textX - 16, 26), "DIALOGUE GRAPH EDITOR", TitleStyle());
        GUI.Label(new Rect(textX, cy + 28, w - textX - 16, 18), "Node graph · dialogue · branching", SubtitleStyle());
    }

    private void DrawToolbar(float w)
    {
        float y = HEADER_HEIGHT;
        EditorGUI.DrawRect(new Rect(0, y, w, TOOLBAR_HEIGHT), COL_TOOLBAR_BG);
        EditorGUI.DrawRect(new Rect(0, y + TOOLBAR_HEIGHT - 1, w, 1), new Color(0.08f, 0.08f, 0.08f));

        GUILayout.BeginArea(new Rect(8, y + 4, w - 16, TOOLBAR_HEIGHT - 8));
        GUILayout.BeginHorizontal();

        GUILayout.Label("File:", GUILayout.Width(28));
        _currentFileName = GUILayout.TextField(_currentFileName, GUILayout.Width(160));
        GUILayout.Space(4);
        GUILayout.Label("Scope:", GUILayout.Width(42));
        _scopeName = GUILayout.TextField(_scopeName, GUILayout.Width(120));
        GUILayout.Space(6);
        if (GUILayout.Button("Save",   GUILayout.Width(50))) Save();
        if (GUILayout.Button("Load",   GUILayout.Width(50))) Load();
        GUILayout.Space(6);
        if (GUILayout.Button("Sync Localization", GUILayout.Width(165))) SyncLocalization();
        GUILayout.Space(6);
        if (GUILayout.Button("Clear",  GUILayout.Width(50))) Clear();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawSecondaryToolbar(float w)
    {
        float y = HEADER_HEIGHT + TOOLBAR_HEIGHT;
        EditorGUI.DrawRect(new Rect(0, y, w, TOOLBAR2_HEIGHT), COL_TOOLBAR2_BG);
        EditorGUI.DrawRect(new Rect(0, y + TOOLBAR2_HEIGHT - 1, w, 1), new Color(0.08f, 0.08f, 0.08f));

        GUILayout.BeginArea(new Rect(8, y + 3, w - 16, TOOLBAR2_HEIGHT - 6));
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Group Selection", GUILayout.Width(110))) GroupSelected();

        // Vertical separator
        GUILayout.Space(8);
        DrawVerticalSeparator();
        GUILayout.Space(8);

        // View label + navigation buttons
        GUILayout.Label("View:",
            new GUIStyle(EditorStyles.label) { normal = { textColor = COL_TEXT_DIM } },
            GUILayout.Width(32));
        GUILayout.Space(2);
        if (GUILayout.Button("Frame All",      GUILayout.Width(75)))  FrameAll();
        GUILayout.Space(4);
        if (GUILayout.Button("Frame Selected", GUILayout.Width(100))) FrameSelected();

        GUILayout.FlexibleSpace();

        // MiniMap Toggle
        bool mapVisible = _miniMap != null && _miniMap.style.display == DisplayStyle.Flex;
        if (GUILayout.Button(mapVisible ? "Hide MiniMap" : "Show MiniMap", GUILayout.Width(100)))
            ToggleMiniMap();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawVerticalSeparator()
    {
        Rect r = GUILayoutUtility.GetRect(1, TOOLBAR2_HEIGHT - 8, GUILayout.Width(1));
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.40f));
    }

    private void BuildGraphView()
    {
        _graphView = new DialogueGraphView(this) { name = "Dialogue Graph" };

        _graphView.style.position = Position.Absolute;
        _graphView.style.top      = TOP_HEIGHT;
        _graphView.style.bottom   = 0;
        _graphView.style.left     = 0;
        _graphView.style.right    = 0;

        rootVisualElement.Add(_graphView);
        SetupMiniMap();
    }

    private void SetupMiniMap()
    {
        _miniMap = new MiniMap { anchored = false };
        _miniMap.SetPosition(new Rect(10, 10, 200, 140));
        _miniMap.style.display = DisplayStyle.Flex;
        _graphView.Add(_miniMap);
    }

    private void ToggleMiniMap()
    {
        if (_miniMap == null) return;
        _miniMap.style.display = (_miniMap.style.display == DisplayStyle.Flex)
            ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void Save()
    {
        if (!ValidateFileName()) return;
        if (string.IsNullOrWhiteSpace(_scopeName)) _scopeName = _currentFileName;
        GraphSaveUtility.GetInstance(_graphView).SaveGraph(_currentFileName, _scopeName);
    }

    private void Load()
    {
        if (!ValidateFileName()) return;
        // Loading replaces the entire graph — record current state first
        _graphView.UndoManager.RecordSnapshot();
        GraphSaveUtility.GetInstance(_graphView).LoadGraph(_currentFileName);
        var container = Resources.Load<DialogueContainer>($"DialogueAssets/{_currentFileName}");
        if (container != null && !string.IsNullOrWhiteSpace(container.scopeName))
            _scopeName = container.scopeName;
    }

    private void SyncLocalization()
    {
        if (DialogueEditorServices.SyncLocalization == null)
        {
            EditorUtility.DisplayDialog(
                "Dialogue Localization",
                "No localization integration is registered for this project. The reusable Dialogue package does not require one.",
                "OK");
            return;
        }

        if (!DialogueEditorServices.SyncLocalization())
            return;

        if (_graphView != null)
        {
            foreach (var node in _graphView.nodes.ToList().OfType<DialogueNode>())
                node.ForcePreviewUpdate();
        }
    }

    private void Clear()
    {
        if (!EditorUtility.DisplayDialog("Clear Graph", "Remove all nodes and connections?", "Clear", "Cancel"))
            return;

        _graphView.UndoManager.RecordSnapshot();

        var allNodes    = _graphView.nodes.ToList().OfType<DialogueNode>().ToList();
        var keepNodes   = allNodes.Where(n => n.NodeType == DialogueNodeType.Start || n.NodeType == DialogueNodeType.End).ToList();
        var deleteNodes = allNodes.Except(keepNodes).ToList();

        _graphView.DeleteElements(_graphView.edges.ToList());
        _graphView.DeleteElements(_graphView.graphElements.ToList().OfType<DialogueGroup>().ToList<GraphElement>());
        _graphView.DeleteElements(deleteNodes);
    }

    private void GroupSelected()
    {
        var selected = _graphView.selection.OfType<DialogueNode>().ToList();
        if (selected.Count == 0) return;

        _graphView.UndoManager.RecordSnapshot();

        var centre = selected.Aggregate(Vector2.zero, (acc, n) => acc + n.GetPosition().position) / selected.Count;
        var group = _graphView.CreateGroup("New Group", centre - new Vector2(20, 30));
        group.AddElements(selected);
    }

    private void FrameAll()      => _graphView?.FrameAll();
    private void FrameSelected() => _graphView?.FrameSelection();

    private bool ValidateFileName()
    {
        if (!string.IsNullOrEmpty(_currentFileName)) return true;
        EditorUtility.DisplayDialog("No File Name", "Please enter a file name.", "OK");
        return false;
    }
}