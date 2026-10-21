using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class GraphUndoManager
{
    [Serializable]
    public class GraphSnapshot
    {
        public List<NodeSnapshot> Nodes = new();
        public List<LinkSnapshot> Links = new();
        public List<GroupSnapshot> Groups = new();
    }

    [Serializable]
    public class NodeSnapshot
    {
        public string Guid;
        public string NodeId;
        public Vector2 Position;
        public DialogueNodeType NodeType;

        public string TextId;
        public string SpeakerRole;
        public string ExpressionId;

        public List<string> ChoiceIds = new();
        public List<string> ChoicePortIds = new();
        public string ConditionKey;
        public List<string> BranchLabels = new();
        public List<string> BranchPortIds = new();

        public string TargetActorRole;
        public DialogueActionType ActionType;
        public string SecondaryActorRole;
        public Vector2 ActionVector;
        public float ActionDuration;
        public string ActionString;
        public float ActionNumber;
        public bool ActionBool;
        public bool WaitForAction;

        public float WaitDuration;
        public string SignalId;
        public string SignalPayload;
        public DialogueContainer SubDialogue;
    }

    [Serializable]
    public class LinkSnapshot
    {
        public string BaseNodeGuid;
        public string PortName;
        public string TargetNodeGuid;
    }

    [Serializable]
    public class GroupSnapshot
    {
        public string Guid;
        public string Title;
        public Vector2 Position;
        public List<string> NodeGuids = new();
    }

    private readonly Stack<GraphSnapshot> undoStack = new();
    private readonly Stack<GraphSnapshot> redoStack = new();
    private readonly DialogueGraphView graphView;
    private bool isRestoring;

    public bool CanUndo => undoStack.Count > 0;
    public bool CanRedo => redoStack.Count > 0;

    public GraphUndoManager(DialogueGraphView graphView)
    {
        this.graphView = graphView;
    }

    public void RecordSnapshot()
    {
        if (isRestoring)
            return;

        undoStack.Push(Capture());
        redoStack.Clear();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        redoStack.Push(Capture());
        isRestoring = true;
        Restore(undoStack.Pop());
        isRestoring = false;
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        undoStack.Push(Capture());
        isRestoring = true;
        Restore(redoStack.Pop());
        isRestoring = false;
    }

    public NodeSnapshot CaptureNode(DialogueNode node)
    {
        return new NodeSnapshot
        {
            Guid = node.GUID,
            NodeId = node.NodeType.ToString(),
            NodeType = node.NodeType,
            Position = node.GetPosition().position,
            TextId = node.GetTextId(),
            SpeakerRole = node.GetSpeakerRole(),
            ExpressionId = node.GetExpressionId(),
            ChoiceIds = node.GetChoiceIds(),
            ChoicePortIds = node.GetChoicePortIds(),
            ConditionKey = node.GetConditionKey(),
            BranchLabels = node.GetBranchLabels(),
            BranchPortIds = node.GetBranchPortIds(),
            TargetActorRole = node.GetTargetActorRole(),
            ActionType = node.GetActionType(),
            SecondaryActorRole = node.GetSecondaryActorRole(),
            ActionVector = node.GetActionVector(),
            ActionDuration = node.GetActionDuration(),
            ActionString = node.GetActionString(),
            ActionNumber = node.GetActionNumber(),
            ActionBool = node.GetActionBool(),
            WaitForAction = node.GetWaitForAction(),
            WaitDuration = node.GetWaitDuration(),
            SignalId = node.GetSignalId(),
            SignalPayload = node.GetSignalPayload(),
            SubDialogue = node.GetSubDialogue(),
        };
    }

    public void ApplyNodeSnapshot(DialogueNode node, NodeSnapshot snapshot)
    {
        node.SetTextId(snapshot.TextId);
        node.SetSpeakerRole(snapshot.SpeakerRole);
        node.SetExpressionId(snapshot.ExpressionId);
        node.SetConditionKey(snapshot.ConditionKey);
        node.SetActionData(
            snapshot.TargetActorRole,
            snapshot.ActionType,
            snapshot.SecondaryActorRole,
            snapshot.ActionVector,
            snapshot.ActionDuration,
            snapshot.ActionString,
            snapshot.ActionNumber,
            snapshot.ActionBool,
            snapshot.WaitForAction);
        node.SetWaitDuration(snapshot.WaitDuration);
        node.SetSignalId(snapshot.SignalId);
        node.SetSignalPayload(snapshot.SignalPayload);
        node.SetSubDialogue(snapshot.SubDialogue);

        for (int i = 0; i < snapshot.ChoiceIds.Count; i++)
        {
            string portId = i < snapshot.ChoicePortIds.Count ? snapshot.ChoicePortIds[i] : null;
            node.AddChoicePort(snapshot.ChoiceIds[i], portId);
        }

        for (int i = 0; i < snapshot.BranchLabels.Count; i++)
        {
            string portId = i < snapshot.BranchPortIds.Count ? snapshot.BranchPortIds[i] : null;
            node.AddBranchPort(snapshot.BranchLabels[i], portId);
        }
    }

    private GraphSnapshot Capture()
    {
        var snapshot = new GraphSnapshot();

        foreach (DialogueNode node in graphView.nodes.ToList().OfType<DialogueNode>())
            snapshot.Nodes.Add(CaptureNode(node));

        foreach (Edge edge in graphView.edges.ToList())
        {
            if (edge.output?.node is DialogueNode outNode && edge.input?.node is DialogueNode inNode)
            {
                snapshot.Links.Add(new LinkSnapshot
                {
                    BaseNodeGuid = outNode.GUID,
                    PortName = edge.output.userData as string ?? edge.output.portName,
                    TargetNodeGuid = inNode.GUID,
                });
            }
        }

        foreach (DialogueGroup group in graphView.graphElements.ToList().OfType<DialogueGroup>())
        {
            snapshot.Groups.Add(new GroupSnapshot
            {
                Guid = group.GUID,
                Title = group.title,
                Position = group.GetPosition().position,
                NodeGuids = group.containedElements.OfType<DialogueNode>().Select(n => n.GUID).ToList(),
            });
        }

        return snapshot;
    }

    private void Restore(GraphSnapshot snapshot)
    {
        graphView.DeleteElements(graphView.graphElements.ToList());

        foreach (NodeSnapshot nodeSnapshot in snapshot.Nodes)
        {
            DialogueNode node = graphView.CreateNode(nodeSnapshot.NodeType);
            node.GUID = nodeSnapshot.Guid;
            node.SetPosition(new Rect(nodeSnapshot.Position, new Vector2(250, 180)));
            ApplyNodeSnapshot(node, nodeSnapshot);
            graphView.AddElement(node);
        }

        Dictionary<string, DialogueNode> nodeMap = graphView.nodes.ToList()
            .OfType<DialogueNode>()
            .ToDictionary(n => n.GUID);

        foreach (LinkSnapshot linkSnapshot in snapshot.Links)
        {
            if (!nodeMap.TryGetValue(linkSnapshot.BaseNodeGuid, out DialogueNode baseNode) ||
                !nodeMap.TryGetValue(linkSnapshot.TargetNodeGuid, out DialogueNode targetNode))
            {
                continue;
            }

            Port outputPort = baseNode.outputContainer.Children().OfType<Port>()
                .FirstOrDefault(p => p.portName == linkSnapshot.PortName ||
                                     (p.userData as string) == linkSnapshot.PortName)
                ?? baseNode.outputContainer.Children().OfType<Port>().FirstOrDefault();
            Port inputPort = targetNode.inputContainer.Children().OfType<Port>().FirstOrDefault();
            if (outputPort == null || inputPort == null)
                continue;

            var edge = new Edge { output = outputPort, input = inputPort };
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            graphView.Add(edge);
        }

        foreach (GroupSnapshot groupSnapshot in snapshot.Groups)
        {
            var group = new DialogueGroup(groupSnapshot.Guid) { title = groupSnapshot.Title };
            group.SetPosition(new Rect(groupSnapshot.Position, new Vector2(300, 200)));
            graphView.AddElement(group);

            foreach (string guid in groupSnapshot.NodeGuids)
                if (nodeMap.TryGetValue(guid, out DialogueNode node))
                    group.AddElement(node);
        }
    }
}
