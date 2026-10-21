using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A labelled group box that visually groups DialogueNodes on the canvas.
/// Double-click the title to rename.
/// Shift+drag a node to remove it from the group.
/// Right-click a node inside a group for "Remove from Group".
/// </summary>
public class DialogueGroup : Group
{
    public string GUID { get; private set; }

    public DialogueGroup(string guid)
    {
        GUID = guid;

        style.backgroundColor   = new Color(0.15f, 0.15f, 0.20f, 0.45f);
        style.borderTopWidth    = 2;
        style.borderBottomWidth = 2;
        style.borderLeftWidth   = 2;
        style.borderRightWidth  = 2;
        style.borderTopColor    = new Color(0.35f, 0.55f, 0.90f, 0.80f);
        style.borderBottomColor = new Color(0.35f, 0.55f, 0.90f, 0.80f);
        style.borderLeftColor   = new Color(0.35f, 0.55f, 0.90f, 0.80f);
        style.borderRightColor  = new Color(0.35f, 0.55f, 0.90f, 0.80f);
        style.borderTopLeftRadius     = 6;
        style.borderTopRightRadius    = 6;
        style.borderBottomLeftRadius  = 6;
        style.borderBottomRightRadius = 6;
    }

    public override bool AcceptsElement(GraphElement element, ref string reasonWhyNotAccepted)
    {
        if (element is DialogueNode) return true;
        reasonWhyNotAccepted = "Only dialogue nodes can be grouped.";
        return false;
    }
}