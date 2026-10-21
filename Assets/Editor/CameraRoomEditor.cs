using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view tools for CameraRoom: a handle on each edge resizes it a whole screen at a time, and the room's
/// corner stays on the tile grid when it's moved. GameObject > Mega Man > Camera Room creates one.
/// </summary>
[CustomEditor(typeof(CameraRoom))]
[CanEditMultipleObjects]
public class CameraRoomEditor : Editor
{
    private void OnSceneGUI()
    {
        var room = (CameraRoom)target;
        SnapCorner(room);

        Rect b = room.Bounds;
        Vector2 screen = CameraRoom.ScreenSize;
        Handles.color = room.gizmoColor;
        float size = HandleUtility.GetHandleSize(b.center) * 0.08f;

        // Right / top: the size changes, the corner stays
        float right = EdgeHandle(new Vector3(b.xMax, b.center.y), Vector3.right, size).x;
        float top = EdgeHandle(new Vector3(b.center.x, b.yMax), Vector3.up, size).y;
        // Left / bottom: the corner moves, the opposite edge stays
        float left = EdgeHandle(new Vector3(b.xMin, b.center.y), Vector3.left, size).x;
        float bottom = EdgeHandle(new Vector3(b.center.x, b.yMin), Vector3.down, size).y;

        int wide = room.screensWide, tall = room.screensTall;
        Vector3 corner = room.transform.position;
        if (!Mathf.Approximately(right, b.xMax)) wide = Screens(right - b.xMin, screen.x);
        if (!Mathf.Approximately(top, b.yMax)) tall = Screens(top - b.yMin, screen.y);
        if (!Mathf.Approximately(left, b.xMin))
        {
            wide = Screens(b.xMax - left, screen.x);
            corner.x = b.xMax - wide * screen.x;
        }
        if (!Mathf.Approximately(bottom, b.yMin))
        {
            tall = Screens(b.yMax - bottom, screen.y);
            corner.y = b.yMax - tall * screen.y;
        }

        if (wide != room.screensWide || tall != room.screensTall || corner != room.transform.position)
        {
            Undo.RecordObjects(new Object[] { room, room.transform }, "Resize Camera Room");
            room.screensWide = wide;
            room.screensTall = tall;
            room.transform.position = corner;
        }

        // Its size, at the top-left corner
        Handles.Label(new Vector3(b.xMin + 0.2f, b.yMax - 0.2f),
            $"{room.name}  {room.screensWide}×{room.screensTall} screens", EditorStyles.boldLabel);
    }

    private static Vector3 EdgeHandle(Vector3 position, Vector3 direction, float size) =>
        Handles.Slider(position, direction, size, Handles.DotHandleCap, 0f);

    private static int Screens(float length, float screen) => Mathf.Max(1, Mathf.RoundToInt(length / screen));

    // Moved with the normal tools: the corner goes back on the tile grid once the drag ends
    private static void SnapCorner(CameraRoom room)
    {
        if (GUIUtility.hotControl != 0) return;
        Vector3 p = room.transform.position;
        Vector3 snapped = new(Mathf.Round(p.x / CameraRoom.Tile) * CameraRoom.Tile, Mathf.Round(p.y / CameraRoom.Tile) * CameraRoom.Tile, p.z);
        if (snapped == p) return;
        Undo.RecordObject(room.transform, "Snap Camera Room");
        room.transform.position = snapped;
    }

    [MenuItem("GameObject/Mega Man/Camera Room", false, 12)]
    private static void Create(MenuCommand command)
    {
        var go = new GameObject("Camera Room", typeof(CameraRoom));
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        var view = SceneView.lastActiveSceneView;
        if (command.context == null && view != null)
        {
            // Under the Scene view's centre, on the screen grid
            Vector2 screen = CameraRoom.ScreenSize;
            Vector3 p = view.pivot;
            go.transform.position = new Vector3(Mathf.Floor(p.x / screen.x) * screen.x, Mathf.Floor(p.y / screen.y) * screen.y, 0f);
        }
        Undo.RegisterCreatedObjectUndo(go, "Create Camera Room");
        Selection.activeObject = go;
    }
}
