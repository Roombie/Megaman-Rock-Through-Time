using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The NES Mega Man camera. Inside a CameraRoom it follows its target (player 1, or overrideTarget), locked to it
/// by default, and stops at the room's edges; a room smaller than the view is centred. The view is whatever the
/// camera shows (256 px wide normally, wider in the Wide screen mode), always 15 tiles tall.
/// Going into another room changes rooms (RoomChanged; transitions hook in there). No rooms in the scene: it just
/// follows. Replaces CameraFollow.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RoomCamera : MonoBehaviour
{
    [Tooltip("Follow this instead of the players (a cutscene, a boss intro...). Empty: player 1.")]
    public Transform overrideTarget;
    [Tooltip("Where on the target the camera centres, from its position (Mega Man's is at his feet).")]
    public Vector2 targetOffset = new(0f, 0.6f);
    [Tooltip("0 = locked to the target (NES). More: seconds to catch up, smoothly.")]
    [Min(0f)] public float smoothTime = 0f;
    [Tooltip("Snap the rendered position to whole game pixels (1 / PPU), so the world and the HUD don't wobble.")]
    public bool snapToPixelGrid = true;

    [Header("Outside the room")]
    [Tooltip("Cover what the view shows outside the current room (in the Wide screen mode a narrow room otherwise " +
             "shows its neighbours). During a transition both rooms show.")]
    public bool maskOutsideRoom;
    public Color maskColor = Color.black;
    [Tooltip("Sorting layer and order of the cover: above the level, below the HUD.")]
    public string maskSortingLayer = "Water";
    public int maskSortingOrder = 32767;

    /// <summary>The room a transition is scrolling into (shown too while it runs), or null.</summary>
    public CameraRoom IncomingRoom { get; set; }
    [System.NonSerialized] private SpriteRenderer[] masks;   // not kept by the editor's script reloads
    private static Sprite maskSprite;

    /// <summary>The cameras following rooms (one, unless split screen).</summary>
    public static IReadOnlyList<RoomCamera> All => all;
    private static readonly List<RoomCamera> all = new();

    /// <summary>The room it's in (null: no rooms here, or the target is outside every room).</summary>
    public CameraRoom CurrentRoom { get; private set; }
    /// <summary>It went from one room into another (from may be null at the start).</summary>
    public event Action<CameraRoom, CameraRoom> RoomChanged;

    private Camera cam;
    private PixelPerfectCamera pixelPerfect;
    private Vector2 position;          // un-snapped; the transform gets the snapped one
    private Vector2 velocity;
    private bool placed;

    /// <summary>What it follows (null: nothing yet, e.g. before Mega Man spawns).</summary>
    public Transform Target => overrideTarget != null ? overrideTarget : Players.First != null ? Players.First.transform : null;

    /// <summary>Half the view in world units (wider in the Wide screen mode).</summary>
    public Vector2 HalfView => new(cam.orthographicSize * cam.aspect, cam.orthographicSize);

    /// <summary>Something else is moving the camera (a transition): it doesn't follow meanwhile.</summary>
    public bool Driven { get; set; }

    private RoomTransition transition;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        pixelPerfect = GetComponent<PixelPerfectCamera>();
        transition = GetComponent<RoomTransition>();
    }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable() => all.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private void LateUpdate()
    {
        Transform target = Target;
        if (target == null || Driven) return;
        Vector2 point = (Vector2)target.position + targetOffset;

        // Its room: the one the target is in (staying in the current one where rooms overlap). Into another room
        // from one it was in: a transition scrolls there (if the camera has one); at the start it just goes there.
        CameraRoom room = CameraRoom.At(point, CurrentRoom);
        if (room != null && room != CurrentRoom)
        {
            if (CurrentRoom != null && placed && transition != null && transition.isActiveAndEnabled
                && transition.Begin(this, CurrentRoom, room, point))
                return;
            EnterRoom(room);
        }

        Vector2 goal = Framed(point, CurrentRoom);
        if (!placed || smoothTime <= 0f)
        {
            position = goal;
            velocity = Vector2.zero;
            placed = true;
        }
        else
        {
            position = Vector2.SmoothDamp(position, goal, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
        Apply();
    }

    /// <summary>Where the camera centres to show `point` in that room: following it, kept inside the room.</summary>
    public Vector2 Framed(Vector2 point, CameraRoom room)
    {
        if (room == null) return point;
        Rect b = room.Bounds;
        Vector2 half = HalfView;
        return new Vector2(
            FrameAxis(point.x, position.x, b.xMin, b.xMax, half.x, room.scrollHorizontally),
            FrameAxis(point.y, position.y, b.yMin, b.yMax, half.y, room.scrollVertically));
    }

    // One axis: centred if the room is smaller than the view; else following (or staying, if it doesn't scroll
    // that way) without showing past the room's edges
    private float FrameAxis(float target, float current, float min, float max, float half, bool scrolls)
    {
        if (max - min <= half * 2f) return (min + max) / 2f;
        float wanted = scrolls || !placed ? target : current;
        return Mathf.Clamp(wanted, min + half, max - half);
    }

    /// <summary>Makes `room` the camera's room (transitions call this once they've scrolled into it).</summary>
    public void EnterRoom(CameraRoom room)
    {
        CameraRoom from = CurrentRoom;
        CurrentRoom = room;

        // Reaching a checkpoint room (not starting in it: that's where the checkpoint brought them)
        if (from != null && room != null && room.isCheckpoint)
        {
            Transform target = Target;
            if (room.spawnPoint != null) Checkpoints.Reach(room.spawnPoint.position);
            else if (target != null) Checkpoints.Reach(target.position);
        }

        RoomChanged?.Invoke(from, room);
    }

    /// <summary>Puts the camera at a centre position at once (respawns, transitions moving it themselves).</summary>
    public void SnapTo(Vector2 centre)
    {
        position = centre;
        velocity = Vector2.zero;
        placed = true;
        Apply();
    }

    /// <summary>The camera's centre (un-snapped).</summary>
    public Vector2 Position => position;

    /// <summary>What the camera shows, in world units.</summary>
    public Rect View
    {
        get
        {
            Vector2 half = HalfView;
            return new Rect(position - half, half * 2f);
        }
    }

    /// <summary>
    /// Some camera shows that point (widened by `margin` on every side) in its current room: things in a
    /// neighbouring room that a wide view happens to show don't count.
    /// </summary>
    public static bool Sees(Vector2 point, float margin = 0f)
    {
        foreach (RoomCamera camera in all)
        {
            if (!camera.placed) continue;
            Rect view = camera.View;
            view.xMin -= margin; view.yMin -= margin; view.xMax += margin; view.yMax += margin;
            if (!view.Contains(point)) continue;
            if (camera.CurrentRoom == null || camera.CurrentRoom.Contains(point)) return true;
        }
        return false;
    }

    private void Apply()
    {
        Vector3 p = new(position.x, position.y, transform.position.z);
        if (snapToPixelGrid)
        {
            float ppu = pixelPerfect != null ? pixelPerfect.assetsPPU : 16f;
            // Multiples of 1/16 are exact floats, so the Screen Space Camera HUD lands on the same pixels every frame
            p.x = Mathf.Round(p.x * ppu) / ppu;
            p.y = Mathf.Round(p.y * ppu) / ppu;
        }
        transform.position = p;
        UpdateMask(p);
    }

    #region Mask outside the room
    // Four strips (left, right, below, above) covering the view outside the shown room(s)
    private void UpdateMask(Vector3 centre)
    {
        bool show = maskOutsideRoom && CurrentRoom != null;
        if (!show)
        {
            if (masks != null) foreach (var m in masks) if (m != null) m.enabled = false;
            return;
        }
        if (!MasksReady()) CreateMasks();

        Rect room = CurrentRoom.Bounds;
        if (IncomingRoom != null)
        {
            Rect other = IncomingRoom.Bounds;
            room = Rect.MinMaxRect(Mathf.Min(room.xMin, other.xMin), Mathf.Min(room.yMin, other.yMin),
                                   Mathf.Max(room.xMax, other.xMax), Mathf.Max(room.yMax, other.yMax));
        }
        Vector2 half = HalfView + Vector2.one;   // a tile over the edges, for pixel snapping
        Rect view = new((Vector2)centre - half, half * 2f);

        SetStrip(masks[0], view.xMin, view.yMin, room.xMin, view.yMax);
        SetStrip(masks[1], room.xMax, view.yMin, view.xMax, view.yMax);
        SetStrip(masks[2], view.xMin, view.yMin, view.xMax, room.yMin);
        SetStrip(masks[3], view.xMin, room.yMax, view.xMax, view.yMax);
    }

    private void SetStrip(SpriteRenderer strip, float xMin, float yMin, float xMax, float yMax)
    {
        bool visible = xMax > xMin && yMax > yMin;
        strip.enabled = visible;
        if (!visible) return;
        strip.color = maskColor;
        strip.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
        strip.transform.localScale = new Vector3(xMax - xMin, yMax - yMin, 1f);
    }

    // Four live strips (a script reload or a scene change can leave the array empty or the strips gone)
    private bool MasksReady()
    {
        if (masks == null || masks.Length != 4) return false;
        foreach (var m in masks) if (m == null) return false;
        return true;
    }

    private void CreateMasks()
    {
        if (masks != null) foreach (var old in masks) if (old != null) Destroy(old.gameObject);
        if (maskSprite == null)
            maskSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        masks = new SpriteRenderer[4];
        for (int i = 0; i < masks.Length; i++)
        {
            var go = new GameObject("Room Mask " + i) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var strip = go.AddComponent<SpriteRenderer>();
            strip.sprite = maskSprite;
            strip.sortingLayerName = maskSortingLayer;
            strip.sortingOrder = maskSortingOrder;
            masks[i] = strip;
        }
    }
    #endregion
}
