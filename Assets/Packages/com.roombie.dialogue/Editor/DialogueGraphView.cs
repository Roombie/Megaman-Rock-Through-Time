using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

public class DialogueGraphView : GraphView
{
    private static string FolderOf([CallerFilePath] string sourcePath = "")
    {
        var dir = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
        int i = dir.IndexOf("/Assets/", StringComparison.Ordinal);
        return i >= 0 ? dir.Substring(i + 1) : dir;
    }

    private SearchWindowProvider _searchWindow;
    public  GraphUndoManager     UndoManager { get; private set; }

    // Clipboard — holds snapshots of copied nodes and the edges between them
    private List<GraphUndoManager.NodeSnapshot> _clipboard     = new();
    private List<GraphUndoManager.LinkSnapshot> _clipboardLinks = new();
    private const float PasteOffset = 24f;

    public DialogueGraphView(EditorWindow editorWindow)
    {
        this.StretchToParentSize();
        style.flexGrow = 1;

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();
        grid.SendToBack();

        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            $"{FolderOf()}/Resources/GraphViewStyles.uss");
        if (styleSheet != null)
            styleSheets.Add(styleSheet);

        UndoManager = new GraphUndoManager(this);

        AddSearchWindow(editorWindow);

        graphViewChanged = OnGraphViewChanged;

        RegisterCallback<KeyDownEvent>(OnKeyDown);

        contentViewContainer.RegisterCallback<ContextClickEvent>(evt =>
        {
            if (selection == null || selection.Count == 0)
            {
                evt.StopImmediatePropagation();
                nodeCreationRequest?.Invoke(new NodeCreationContext
                {
                    screenMousePosition = evt.mousePosition
                });
            }
        });

        schedule.Execute(CreateEssentialNodes);
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.ctrlKey || evt.commandKey)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Z:
                    UndoManager.Undo();
                    evt.StopPropagation();
                    break;
                case KeyCode.Y:
                    UndoManager.Redo();
                    evt.StopPropagation();
                    break;
                case KeyCode.C:
                    CopySelected();
                    evt.StopPropagation();
                    break;
                case KeyCode.V:
                    PasteClipboard();
                    evt.StopPropagation();
                    break;
            }
        }
    }

    // ── Copy ──────────────────────────────────────────────────────────────────

    private void CopySelected()
    {
        var selectedNodes = selection.OfType<DialogueNode>().ToList();
        if (selectedNodes.Count == 0) return;

        // Never copy Start or End — they are singletons
        selectedNodes = selectedNodes
            .Where(n => n.NodeType != DialogueNodeType.Start && n.NodeType != DialogueNodeType.End)
            .ToList();

        if (selectedNodes.Count == 0) return;

        var selectedGuids = new HashSet<string>(selectedNodes.Select(n => n.GUID));

        // Snapshot each selected node
        _clipboard = selectedNodes.Select(UndoManager.CaptureNode).ToList();

        // Only keep edges where both endpoints are in the selection
        _clipboardLinks = edges.ToList()
            .Where(e =>
                e.output?.node is DialogueNode outNode &&
                e.input?.node  is DialogueNode inNode  &&
                selectedGuids.Contains(outNode.GUID)   &&
                selectedGuids.Contains(inNode.GUID))
            .Select(e => new GraphUndoManager.LinkSnapshot
            {
                BaseNodeGuid   = ((DialogueNode)e.output.node).GUID,
                PortName       = e.output.userData as string ?? e.output.portName,
                TargetNodeGuid = ((DialogueNode)e.input.node).GUID,
            })
            .ToList();
    }

    // ── Paste ─────────────────────────────────────────────────────────────────

    private void PasteClipboard()
    {
        if (_clipboard.Count == 0) return;

        UndoManager.RecordSnapshot();

        // Map old GUID → new GUID so edges can be remapped
        var guidMap = new Dictionary<string, string>();
        foreach (var ns in _clipboard)
            guidMap[ns.Guid] = Guid.NewGuid().ToString();

        // Deselect everything so only pasted nodes end up selected
        ClearSelection();

        var newNodes = new Dictionary<string, DialogueNode>();

        foreach (var ns in _clipboard)
        {
            var node = CreateNode(ns.NodeType);
            node.GUID = guidMap[ns.Guid];
            node.SetPosition(new Rect(ns.Position + new Vector2(PasteOffset, PasteOffset),
                                      new Vector2(250, 180)));
            UndoManager.ApplyNodeSnapshot(node, ns);

            AddElement(node);
            AddToSelection(node);
            newNodes[node.GUID] = node;
        }

        // Reconnect edges using remapped GUIDs
        foreach (var ls in _clipboardLinks)
        {
            if (!guidMap.TryGetValue(ls.BaseNodeGuid,   out var newBase))   continue;
            if (!guidMap.TryGetValue(ls.TargetNodeGuid, out var newTarget)) continue;
            if (!newNodes.TryGetValue(newBase,   out var baseNode))   continue;
            if (!newNodes.TryGetValue(newTarget, out var targetNode)) continue;

            var outputPort = baseNode.outputContainer.Children().OfType<Port>()
                .FirstOrDefault(p => p.portName == ls.PortName ||
                                     (p.userData as string) == ls.PortName)
                ?? baseNode.outputContainer.Children().OfType<Port>().FirstOrDefault();

            var inputPort = targetNode.inputContainer.Children().OfType<Port>().FirstOrDefault();

            if (outputPort == null || inputPort == null) continue;

            var edge = new Edge { output = outputPort, input = inputPort };
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            Add(edge);
        }

        // Shift clipboard offset so repeated pastes don't stack on the same spot
        for (int i = 0; i < _clipboard.Count; i++)
            _clipboard[i].Position += new Vector2(PasteOffset, PasteOffset);
    }

    // ── Graph change — snapshot before delete/move, auto-delete empty groups ──

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            UndoManager.RecordSnapshot();

        if (change.movedElements != null && change.movedElements.Count > 0)
            UndoManager.RecordSnapshot();

        if (change.movedElements != null)
            schedule.Execute(DeleteEmptyGroups);

        return change;
    }

    private void DeleteEmptyGroups()
    {
        var emptyGroups = graphElements
            .ToList()
            .OfType<DialogueGroup>()
            .Where(g => !g.containedElements.Any())
            .ToList();

        if (emptyGroups.Count > 0)
            DeleteElements(emptyGroups);
    }

    // ── Context menu — "Remove from Group" ───────────────────────────────────

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);

        var nodesInGroup = selection
            .OfType<DialogueNode>()
            .Where(n => n.GetContainingScope() is DialogueGroup)
            .ToList();

        if (nodesInGroup.Count == 0) return;

        string label = nodesInGroup.Count == 1
            ? "Remove from Group"
            : $"Remove {nodesInGroup.Count} Nodes from Group";

        evt.menu.AppendSeparator();
        evt.menu.AppendAction(label, _ =>
        {
            UndoManager.RecordSnapshot();
            foreach (var node in nodesInGroup)
            {
                var group = node.GetContainingScope() as DialogueGroup;
                group?.RemoveElement(node);
            }
            schedule.Execute(DeleteEmptyGroups);
        });
    }

    // ── Node creation ─────────────────────────────────────────────────────────

    public void CreateNodeAt(DialogueNodeType type, Vector2 position)
    {
        if ((type == DialogueNodeType.Start || type == DialogueNodeType.End) && HasNodeOfType(type))
        {
            EditorUtility.DisplayDialog("Node Already Exists",
                $"Only one {type} node is allowed per graph.", "OK");
            return;
        }

        UndoManager.RecordSnapshot();
        var node = CreateNode(type);
        node.SetPosition(new Rect(position, new Vector2(250, 180)));
        AddElement(node);
    }

    public DialogueNode CreateNode(DialogueNodeType type)
    {
        var node = new DialogueNode { GUID = Guid.NewGuid().ToString() };
        node.Initialize(type);
        return node;
    }

    // ── Group creation ────────────────────────────────────────────────────────

    public DialogueGroup CreateGroup(string title, Vector2 position)
    {
        var group = new DialogueGroup(Guid.NewGuid().ToString())
        {
            title = title
        };
        group.SetPosition(new Rect(position, new Vector2(300, 200)));
        AddElement(group);
        return group;
    }

    // ── Port compatibility ────────────────────────────────────────────────────

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports
            .ToList()
            .Where(p => p != startPort && p.node != startPort.node && p.direction != startPort.direction)
            .ToList();
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void AddSearchWindow(EditorWindow editorWindow)
    {
        _searchWindow = ScriptableObject.CreateInstance<SearchWindowProvider>();
        _searchWindow.Init(editorWindow, this);
        nodeCreationRequest = ctx =>
            SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _searchWindow);
    }

    private void CreateEssentialNodes()
    {
        if (!HasNodeOfType(DialogueNodeType.Start))
            CreateNodeAt(DialogueNodeType.Start, new Vector2(80, 200));

        if (!HasNodeOfType(DialogueNodeType.End))
            CreateNodeAt(DialogueNodeType.End, new Vector2(600, 200));
    }

    private bool HasNodeOfType(DialogueNodeType type)
        => nodes.ToList().OfType<DialogueNode>().Any(n => n.NodeType == type);
}