using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomEditor(typeof(DialogueContainer))]
public class DialogueContainerInspector : Editor
{
    private bool _showNodes  = true;
    private bool _showGroups = true;
    private bool _showLinks  = false;

    static readonly Color COL_BG      = new Color(0.15f, 0.15f, 0.18f);
    static readonly Color COL_HEADER  = new Color(0.20f, 0.20f, 0.24f);
    static readonly Color COL_ACCENT  = new Color(0.25f, 0.50f, 0.85f);
    static readonly Color COL_START   = new Color(0.15f, 0.15f, 0.15f);
    static readonly Color COL_END     = new Color(0.15f, 0.15f, 0.15f);
    static readonly Color COL_DIAL    = new Color(0.18f, 0.50f, 0.18f);
    static readonly Color COL_CHOICE  = new Color(0.20f, 0.38f, 0.60f);
    static readonly Color COL_COND    = new Color(0.65f, 0.35f, 0.10f);
    static readonly Color COL_BRANCH  = new Color(0.50f, 0.18f, 0.50f);
    static readonly Color COL_ACTION  = new Color(0.65f, 0.28f, 0.18f);
    static readonly Color COL_WAIT    = new Color(0.20f, 0.45f, 0.50f);
    static readonly Color COL_SIGNAL  = new Color(0.55f, 0.45f, 0.12f);
    static readonly Color COL_SUB     = new Color(0.35f, 0.25f, 0.62f);

    public override void OnInspectorGUI()
    {
        var container = (DialogueContainer)target;

        // ── Header ────────────────────────────────────────────────────────────
        DrawColorRect(COL_ACCENT, 2);
        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Dialogue Graph", EditorStyles.boldLabel);
        if (GUILayout.Button("Open Editor", GUILayout.Width(100)))
            DialogueGraphEditor.OpenEditorWithAsset(container);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ── Scope ─────────────────────────────────────────────────────────────
        DrawCard(() =>
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Scope", EditorStyles.miniBoldLabel, GUILayout.Width(60));
            EditorGUI.BeginChangeCheck();
            var newScope = EditorGUILayout.TextField(container.scopeName);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(container, "Change Scope");
                container.scopeName = newScope;
                EditorUtility.SetDirty(container);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Nodes",  EditorStyles.miniLabel, GUILayout.Width(60));
            GUILayout.Label(container.DialogueNodeData.Count.ToString(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Links",  EditorStyles.miniLabel, GUILayout.Width(40));
            GUILayout.Label(container.NodeLinks.Count.ToString(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Groups", EditorStyles.miniLabel, GUILayout.Width(45));
            GUILayout.Label(container.Groups?.Count.ToString() ?? "0", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        });

        GUILayout.Space(6);

        // ── Nodes ─────────────────────────────────────────────────────────────
        _showNodes = EditorGUILayout.Foldout(_showNodes, $"Nodes ({container.DialogueNodeData.Count})", true, EditorStyles.foldoutHeader);
        if (_showNodes)
        {
            foreach (var node in container.DialogueNodeData)
            {
                Color typeColor = node.nodeType switch
                {
                    DialogueNodeType.Start                 => COL_START,
                    DialogueNodeType.End                   => COL_END,
                    DialogueNodeType.Dialogue              => COL_DIAL,
                    DialogueNodeType.Choice                => COL_CHOICE,
                    DialogueNodeType.Condition             => COL_COND,
                    DialogueNodeType.Branch                => COL_BRANCH,
                    DialogueNodeType.Action                => COL_ACTION,
                    DialogueNodeType.Wait                  => COL_WAIT,
                    DialogueNodeType.Signal                => COL_SIGNAL,
                    DialogueNodeType.SubDialogue           => COL_SUB,
                    _                                      => COL_HEADER
                };

                DrawCard(() =>
                {
                    EditorGUILayout.BeginHorizontal();

                    // Coloured type badge
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = typeColor;
                    GUILayout.Box(node.nodeType.ToString(), GUILayout.Width(130), GUILayout.Height(18));
                    GUI.backgroundColor = oldBg;

                    GUILayout.Space(6);

                    if (!string.IsNullOrWhiteSpace(node.textId))
                    {
                        GUILayout.Label(node.textId, EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        var preview = DialogueEditorServices.Text(node.textId, "EN");
                        if (preview != node.textId)
                            GUILayout.Label(preview, EditorStyles.miniLabel, GUILayout.MaxWidth(200));
                    }
                    else
                    {
                        GUILayout.Label($"GUID: {node.guid.Substring(0, 8)}…", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                });

                GUILayout.Space(2);
            }
        }

        GUILayout.Space(4);

        // ── Groups ────────────────────────────────────────────────────────────
        if (container.Groups != null && container.Groups.Count > 0)
        {
            _showGroups = EditorGUILayout.Foldout(_showGroups, $"Groups ({container.Groups.Count})", true, EditorStyles.foldoutHeader);
            if (_showGroups)
            {
                foreach (var group in container.Groups)
                {
                    DrawCard(() =>
                    {
                        GUILayout.Label($"  {group.title}  ({group.nodeGuids.Count} nodes)", EditorStyles.miniLabel);
                    });
                    GUILayout.Space(2);
                }
            }
            GUILayout.Space(4);
        }

        // ── Links ─────────────────────────────────────────────────────────────
        _showLinks = EditorGUILayout.Foldout(_showLinks, $"Links ({container.NodeLinks.Count})", true, EditorStyles.foldoutHeader);
        if (_showLinks)
        {
            foreach (var link in container.NodeLinks)
            {
                DrawCard(() =>
                {
                    GUILayout.Label(
                        $"  {link.baseNodeGuid.Substring(0,8)}… [{link.portName}] → {link.targetNodeGuid.Substring(0,8)}…",
                        EditorStyles.miniLabel);
                });
                GUILayout.Space(2);
            }
        }
    }

    private void DrawCard(System.Action content)
    {
        var rect = EditorGUILayout.BeginVertical();
        EditorGUI.DrawRect(rect, COL_HEADER);
        GUILayout.Space(4);
        content();
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();
    }

    private void DrawColorRect(Color color, float height)
    {
        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(height));
        EditorGUI.DrawRect(rect, color);
    }
}
