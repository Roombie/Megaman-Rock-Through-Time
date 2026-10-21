using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class SearchWindowProvider : ScriptableObject, ISearchWindowProvider
{
    private DialogueGraphView graphView;
    private EditorWindow window;
    private Texture2D icon;

    public void Init(EditorWindow window, DialogueGraphView graphView)
    {
        this.window = window;
        this.graphView = graphView;
    }

    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        return new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("Create Node"), 0),

            new SearchTreeGroupEntry(new GUIContent("Flow"), 1),
            Entry("Start", "Start", 2),
            Entry("End", "End", 2),
            Entry("Wait", "Wait", 2),
            Entry("Sub Dialogue", "SubDialogue", 2),

            new SearchTreeGroupEntry(new GUIContent("Dialogue"), 1),
            Entry("Dialogue", "Dialogue", 2),
            Entry("Choice", "Choice", 2),

            new SearchTreeGroupEntry(new GUIContent("World / Cutscene"), 1),
            Entry("Action", "Action", 2),
            Entry("Signal / Event", "Signal", 2),

            new SearchTreeGroupEntry(new GUIContent("Logic"), 1),
            Entry("Condition (flag check)", "Condition", 2),
            Entry("Branch (multi-output)", "Branch", 2),
        };
    }

    public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
    {
        if (entry.userData is not string nodeTypeName)
            return false;

        VisualElement root = window.rootVisualElement;
        Vector2 localInRoot = root.ChangeCoordinatesTo(
            root.parent,
            context.screenMousePosition - window.position.position);
        Vector2 localInGraph = graphView.contentViewContainer.WorldToLocal(localInRoot);

        if (System.Enum.TryParse(nodeTypeName, out DialogueNodeType nodeType))
        {
            graphView.CreateNodeAt(nodeType, localInGraph);
            return true;
        }

        return false;
    }

    private SearchTreeEntry Entry(string label, string userData, int level) =>
        new(new GUIContent(label, icon)) { level = level, userData = userData };
}
