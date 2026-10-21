using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class GraphSaveUtility
{
    private DialogueGraphView graphView;
    private DialogueContainer cache;

    private List<Edge> Edges => graphView.edges.ToList();
    private List<DialogueNode> Nodes => graphView.nodes.ToList().Cast<DialogueNode>().ToList();
    private List<DialogueGroup> Groups => graphView.graphElements.ToList().OfType<DialogueGroup>().ToList();

    public static GraphSaveUtility GetInstance(DialogueGraphView graphView) =>
        new() { graphView = graphView };

    public void SaveGraph(string fileName, string scopeName = "")
    {
        var container = ScriptableObject.CreateInstance<DialogueContainer>();
        container.scopeName = string.IsNullOrWhiteSpace(scopeName) ? fileName : scopeName;

        foreach (Edge edge in Edges.Where(e => e.input.node != null))
        {
            if (edge.output.node is not DialogueNode outNode || edge.input.node is not DialogueNode inNode)
                continue;

            string savedPortName =
                (outNode.NodeType == DialogueNodeType.Choice || outNode.NodeType == DialogueNodeType.Branch)
                    ? edge.output.userData as string ?? edge.output.portName
                    : edge.output.portName;

            container.NodeLinks.Add(new NodeLinkData
            {
                baseNodeGuid = outNode.GUID,
                portName = savedPortName,
                targetNodeGuid = inNode.GUID,
            });
        }

        var allTextIds = new HashSet<string>();

        foreach (DialogueNode node in Nodes)
        {
            var data = new DialogueNodeData
            {
                guid = node.GUID,
                nodeId = node.NodeType.ToString(),
                nodeType = node.NodeType,
                position = node.GetPosition().position,
                textId = node.GetTextId(),
                speakerRole = node.GetSpeakerRole(),
                expressionId = node.GetExpressionId(),
                conditionKey = node.GetConditionKey(),
                targetActorRole = node.GetTargetActorRole(),
                actionType = node.GetActionType(),
                secondaryActorRole = node.GetSecondaryActorRole(),
                actionVector = node.GetActionVector(),
                actionDuration = node.GetActionDuration(),
                actionString = node.GetActionString(),
                actionNumber = node.GetActionNumber(),
                actionBool = node.GetActionBool(),
                waitForAction = node.GetWaitForAction(),
                waitDuration = node.GetWaitDuration(),
                signalId = node.GetSignalId(),
                signalPayload = node.GetSignalPayload(),
                subDialogue = node.GetSubDialogue(),
            };

            if (!string.IsNullOrWhiteSpace(data.textId))
                allTextIds.Add(data.textId.Trim());

            switch (node.NodeType)
            {
                case DialogueNodeType.Choice:
                    data.choiceIds = node.GetChoiceIds();
                    data.choicePortIds = node.GetChoicePortIds();
                    foreach (string id in data.choiceIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                        allTextIds.Add(id.Trim());
                    break;

                case DialogueNodeType.Branch:
                    data.branchLabels = node.GetBranchLabels();
                    data.branchPortIds = node.GetBranchPortIds();
                    break;
            }

            container.DialogueNodeData.Add(data);
        }

        foreach (DialogueGroup group in Groups)
        {
            container.Groups.Add(new GroupData
            {
                guid = group.GUID,
                title = group.title,
                position = group.GetPosition().position,
                nodeGuids = group.containedElements
                    .OfType<DialogueNode>()
                    .Select(n => n.GUID)
                    .ToList(),
            });
        }

        WarnUnusedDialogues(allTextIds, container.scopeName);

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/DialogueAssets"))
            AssetDatabase.CreateFolder("Assets/Resources", "DialogueAssets");

        int savedNodeCount = container.DialogueNodeData.Count;
        int savedGroupCount = container.Groups.Count;
        string savedScope = container.scopeName;

        string assetPath = $"Assets/Resources/DialogueAssets/{fileName}.asset";
        DialogueContainer existing = AssetDatabase.LoadAssetAtPath<DialogueContainer>(assetPath);
        if (existing != null)
        {
            existing.scopeName = container.scopeName;
            existing.NodeLinks = container.NodeLinks;
            existing.DialogueNodeData = container.DialogueNodeData;
            existing.Groups = container.Groups;
            EditorUtility.SetDirty(existing);
            DestroyTemporary(container);
        }
        else
        {
            AssetDatabase.CreateAsset(container, assetPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[DialogueGraph] Saved '{fileName}' (scope: {savedScope}) — " +
            $"{savedNodeCount} nodes, {savedGroupCount} groups.");
    }

    public void LoadGraph(string fileName)
    {
        cache = Resources.Load<DialogueContainer>($"DialogueAssets/{fileName}");
        if (cache == null)
        {
            EditorUtility.DisplayDialog(
                "File Not Found",
                $"Could not find dialogue graph '{fileName}' in Resources/DialogueAssets/.",
                "OK");
            return;
        }

        ClearGraph();
        CreateNodes();
        ConnectNodes();
        RestoreGroups();
    }

    private void ClearGraph()
    {
        graphView.DeleteElements(graphView.graphElements.ToList());
    }

    private void CreateNodes()
    {
        foreach (DialogueNodeData data in cache.DialogueNodeData)
        {
            DialogueNodeType type = Enum.TryParse(data.nodeId, out DialogueNodeType parsed)
                ? parsed
                : data.nodeType;

            DialogueNode node = graphView.CreateNode(type);
            node.GUID = data.guid;
            node.SetPosition(new Rect(data.position, new Vector2(250, 180)));
            node.SetTextId(data.textId);
            node.SetSpeakerRole(data.speakerRole);
            node.SetExpressionId(data.expressionId);
            node.SetConditionKey(data.conditionKey);
            node.SetActionData(
                data.targetActorRole,
                data.actionType,
                data.secondaryActorRole,
                data.actionVector,
                data.actionDuration,
                data.actionString,
                data.actionNumber,
                data.actionBool,
                data.waitForAction);
            node.SetWaitDuration(data.waitDuration);
            node.SetSignalId(data.signalId);
            node.SetSignalPayload(data.signalPayload);
            node.SetSubDialogue(data.subDialogue);

            if (type == DialogueNodeType.Choice)
            {
                for (int i = 0; i < data.choiceIds.Count; i++)
                {
                    string portId = i < data.choicePortIds.Count ? data.choicePortIds[i] : null;
                    node.AddChoicePort(data.choiceIds[i], portId);
                }
            }
            else if (type == DialogueNodeType.Branch)
            {
                for (int i = 0; i < data.branchLabels.Count; i++)
                {
                    string portId = i < data.branchPortIds.Count ? data.branchPortIds[i] : null;
                    node.AddBranchPort(data.branchLabels[i], portId);
                }
            }

            graphView.AddElement(node);
        }
    }

    private void ConnectNodes()
    {
        Dictionary<string, DialogueNode> nodeMap = Nodes.ToDictionary(n => n.GUID);
        var legacyDynamicPortIndices = new Dictionary<string, int>();

        foreach (NodeLinkData link in cache.NodeLinks)
        {
            if (!nodeMap.TryGetValue(link.baseNodeGuid, out DialogueNode baseNode) ||
                !nodeMap.TryGetValue(link.targetNodeGuid, out DialogueNode targetNode))
            {
                continue;
            }

            List<Port> outputPorts = baseNode.outputContainer.Children().OfType<Port>().ToList();
            Port outputPort = outputPorts.FirstOrDefault(p =>
                p.portName == link.portName || (p.userData as string) == link.portName);

            if (outputPort == null &&
                (baseNode.NodeType == DialogueNodeType.Choice || baseNode.NodeType == DialogueNodeType.Branch) &&
                outputPorts.Count > 0)
            {
                legacyDynamicPortIndices.TryGetValue(baseNode.GUID, out int legacyIndex);
                outputPort = outputPorts[Mathf.Clamp(legacyIndex, 0, outputPorts.Count - 1)];
                legacyDynamicPortIndices[baseNode.GUID] = legacyIndex + 1;
            }

            outputPort ??= outputPorts.FirstOrDefault();
            Port inputPort = targetNode.inputContainer.Children().OfType<Port>().FirstOrDefault();

            if (outputPort == null || inputPort == null)
                continue;

            var edge = new Edge { output = outputPort, input = inputPort };
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            graphView.Add(edge);
        }
    }

    private void RestoreGroups()
    {
        if (cache.Groups == null)
            return;

        Dictionary<string, DialogueNode> nodeMap = Nodes.ToDictionary(n => n.GUID);
        foreach (GroupData groupData in cache.Groups)
        {
            var group = new DialogueGroup(groupData.guid) { title = groupData.title };
            group.SetPosition(new Rect(groupData.position, new Vector2(300, 200)));
            graphView.AddElement(group);

            foreach (string nodeGuid in groupData.nodeGuids)
                if (nodeMap.TryGetValue(nodeGuid, out DialogueNode node))
                    group.AddElement(node);
        }
    }

    private static void WarnUnusedDialogues(HashSet<string> usedIds, string scope)
    {
        List<string> allIds = DialogueEditorServices.GetAllTextIds != null ? DialogueEditorServices.GetAllTextIds() : null;
        if (allIds == null || allIds.Count == 0)
            return;

        string prefix = string.IsNullOrWhiteSpace(scope) ? "" : scope + "/";
        List<string> scopedIds = prefix.Length > 0
            ? allIds.Where(id => id.StartsWith(prefix)).ToList()
            : allIds;

        List<string> unused = scopedIds.Where(id => !usedIds.Contains(id)).ToList();
        if (unused.Count == 0)
            return;

        string list = string.Join("\n", unused.Take(20));
        if (unused.Count > 20)
            list += $"\n… and {unused.Count - 20} more";

        bool remove = EditorUtility.DisplayDialog(
            "Unused Dialogue IDs",
            $"Found {unused.Count} text ID(s) in scope '{scope}' that are not used by any node:\n\n" +
            $"{list}\n\nRemove them from the project localization source?",
            "Remove",
            "Keep");

        if (remove)
            DialogueEditorServices.RemoveTextIds?.Invoke(unused);
    }

    private static void DestroyTemporary(DialogueContainer container)
    {
        if (container != null && !AssetDatabase.Contains(container))
            UnityEngine.Object.DestroyImmediate(container);
    }
}
