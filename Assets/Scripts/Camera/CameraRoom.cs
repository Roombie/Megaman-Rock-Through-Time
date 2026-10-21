using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A room (a section of a level) the camera stays inside, NES style: inside it the camera follows the player,
/// stopping at its edges; going into another room makes a screen transition (RoomTransition).
/// Sized in NES screens (16 × 15 tiles, 256 × 240 px): the position is its bottom-left corner. Place and resize
/// it in the Scene view (handles snap to screens, the corner snaps to tiles); GameObject > Mega Man > Camera Room
/// makes one. Rooms touching edge to edge are neighbours: that's where transitions happen.
/// </summary>
[DisallowMultipleComponent]
public class CameraRoom : MonoBehaviour
{
    public const int ScreenTilesX = 16;
    public const int ScreenTilesY = 15;
    /// <summary>One tile in world units (16 px at 16 pixels per unit).</summary>
    public const float Tile = 1f;
    public static readonly Vector2 ScreenSize = new(ScreenTilesX * Tile, ScreenTilesY * Tile);

    [Tooltip("Width in NES screens (16 tiles each).")]
    [Min(1)] public int screensWide = 1;
    [Tooltip("Height in NES screens (15 tiles each).")]
    [Min(1)] public int screensTall = 1;

    [Header("Scrolling inside the room")]
    [Tooltip("The camera follows the player left and right (when the room is wider than the view).")]
    public bool scrollHorizontally = true;
    [Tooltip("The camera follows the player up and down (when the room is taller than the view). NES rooms are " +
             "one screen tall and only change screens with transitions.")]
    public bool scrollVertically = true;

    [Header("Checkpoint")]
    [Tooltip("Reaching this room is a checkpoint: dying afterwards brings the players back here.")]
    public bool isCheckpoint;
    [Tooltip("Where they come back (on the ground). Empty: where player 1 was when the room was reached.")]
    public Transform spawnPoint;

    [Tooltip("Drawn in the Scene view.")]
    public Color gizmoColor = new(1f, 0.6f, 0.1f, 1f);

    private static readonly List<CameraRoom> all = new();
    public static IReadOnlyList<CameraRoom> All => all;

    /// <summary>The room's area in world units.</summary>
    public Rect Bounds => new((Vector2)transform.position, new Vector2(screensWide * ScreenSize.x, screensTall * ScreenSize.y));

    public bool Contains(Vector2 point) => Bounds.Contains(point);

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable() => all.Remove(this);

    // With Enter Play Mode Options (no domain reload) statics survive between play sessions
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    /// <summary>
    /// The room a point is in (null: none). Where rooms overlap, `prefer` wins if it contains the point (the room
    /// the camera is already in), then the smallest.
    /// </summary>
    public static CameraRoom At(Vector2 point, CameraRoom prefer = null)
    {
        if (prefer != null && prefer.isActiveAndEnabled && prefer.Contains(point)) return prefer;
        CameraRoom best = null;
        float bestArea = float.MaxValue;
        foreach (CameraRoom room in all)
        {
            if (!room.Contains(point)) continue;
            Rect b = room.Bounds;
            float area = b.width * b.height;
            if (area < bestArea)
            {
                bestArea = area;
                best = room;
            }
        }
        return best;
    }

    private void OnDrawGizmos()
    {
        Rect b = Bounds;
        // Screen divisions, faint
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.25f);
        for (int x = 1; x < screensWide; x++)
            Gizmos.DrawLine(new Vector3(b.xMin + x * ScreenSize.x, b.yMin), new Vector3(b.xMin + x * ScreenSize.x, b.yMax));
        for (int y = 1; y < screensTall; y++)
            Gizmos.DrawLine(new Vector3(b.xMin, b.yMin + y * ScreenSize.y), new Vector3(b.xMax, b.yMin + y * ScreenSize.y));
        // The outline
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(b.center, b.size);
        // The checkpoint's spawn point
        if (isCheckpoint && spawnPoint != null)
        {
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
            Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + Vector3.up * 1.2f);
        }
    }
}
