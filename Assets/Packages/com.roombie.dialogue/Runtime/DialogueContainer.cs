using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Graph")]
public class DialogueContainer : ScriptableObject
{
    [Tooltip("Unique namespace for this graph. Text IDs are stored as scope/id internally.")]
    public string scopeName = "";

    public List<NodeLinkData>     NodeLinks        = new();
    public List<DialogueNodeData> DialogueNodeData = new();
    public List<GroupData>        Groups           = new();
}

[Serializable]
public class GroupData
{
    public string       guid;
    public string       title;
    public Vector2      position;
    public List<string> nodeGuids = new();
}
