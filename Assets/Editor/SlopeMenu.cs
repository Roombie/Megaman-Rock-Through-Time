using UnityEditor;
using UnityEngine;

/// <summary>GameObject > Mega Man > Slope: a SlopeBlock on the Ground layer, on the 16 px grid at the scene view.</summary>
public static class SlopeMenu
{
    [MenuItem("GameObject/Mega Man/Slope 45°", false, 10)]
    private static void CreateSlope45(MenuCommand command) => CreateSlope(command, 32, 32);

    [MenuItem("GameObject/Mega Man/Slope 2-1 (gentle)", false, 11)]
    private static void CreateSlopeGentle(MenuCommand command) => CreateSlope(command, 64, 32);

    private static void CreateSlope(MenuCommand command, int width, int height)
    {
        var go = new GameObject("Slope", typeof(SlopeBlock));
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0) go.layer = ground;
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        var view = SceneView.lastActiveSceneView;
        if (command.context == null && view != null)
        {
            Vector3 p = view.pivot;
            go.transform.position = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f);
        }
        var slope = go.GetComponent<SlopeBlock>();
        slope.widthPixels = width;
        slope.heightPixels = height;
        slope.Rebuild();
        Undo.RegisterCreatedObjectUndo(go, "Create Slope");
        Selection.activeObject = go;
    }
}
