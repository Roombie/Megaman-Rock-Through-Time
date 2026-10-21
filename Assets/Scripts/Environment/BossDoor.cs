using System.Collections;
using UnityEngine;

/// <summary>
/// An NES boss shutter, placed on the edge between two rooms: walking into it freezes the game, the shutter opens
/// a segment at a time, the screen scrolls into the next room carrying the players through, and it closes behind
/// them. One-way doors (the default) stay shut afterwards.
/// It's solid ground until then (Ground layer, its BoxCollider2D sized from the tiles). The shutter graphic is an
/// optional SpriteRenderer in Tiled draw mode, resized as it opens (it retracts upwards).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossDoor : MonoBehaviour
{
    [Tooltip("Size in tiles (NES shutters: 1 wide, 4 tall).")]
    [Min(1)] public int widthTiles = 1;
    [Min(1)] public int heightTiles = 4;
    [Tooltip("Seconds per tile segment, opening and closing.")]
    [Min(0f)] public float secondsPerSegment = 0.1f;
    [Tooltip("Shut for good once the players are through (NES boss doors). Off: it opens every time.")]
    public bool oneWay = true;
    [Tooltip("The shutter graphic (SpriteRenderer in Tiled draw mode). Empty: the one on this object, if any.")]
    public SpriteRenderer shutter;

    [Header("Sounds")]
    public AudioEvent openSound;
    public AudioEvent closeSound;

    private BoxCollider2D solid;
    private bool busy, locked;

    private Vector2 Size => new Vector2(widthTiles, heightTiles) * CameraRoom.Tile;
    private Rect Area => new((Vector2)transform.position - Size / 2f, Size);

    private void Awake()
    {
        solid = GetComponent<BoxCollider2D>();
        if (shutter == null) shutter = GetComponent<SpriteRenderer>();
        Shape(heightTiles);
    }

    // The collider and the shutter follow the size in tiles. Not inside OnValidate itself: resizing a SpriteRenderer
    // sends a message, which Unity doesn't allow there; right after it.
    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) FitToSize(); };
#endif
    }

    private void FitToSize()
    {
        if (TryGetComponent(out BoxCollider2D box))
        {
            box.size = Size;
            box.offset = Vector2.zero;
        }
        var graphic = shutter != null ? shutter : GetComponent<SpriteRenderer>();
        if (graphic != null && graphic.drawMode != SpriteDrawMode.Simple) graphic.size = Size;
    }

    private void Update()
    {
        if (busy || locked || RoomTransition.AnyRunning) return;

        Rect door = Area;
        foreach (Megaman player in Players.All)
        {
            if (player == null || player.IsInputFrozen) continue;
            Rect box = player.Mover.BoxAt(player.Body.position);
            bool overlapsVertically = box.yMax > door.yMin && box.yMin < door.yMax;
            if (!overlapsVertically) continue;

            // Right next to it (a hair's gap: the mover stops short of solid things) and walking into it
            const float touch = 0.1f;
            if (player.MoveInput.x > 0.5f && box.xMax >= door.xMin - touch && box.xMax <= door.center.x)
            {
                Pass(player, RoomTransition.Direction.Right, box);
                return;
            }
            if (player.MoveInput.x < -0.5f && box.xMin <= door.xMax + touch && box.xMin >= door.center.x)
            {
                Pass(player, RoomTransition.Direction.Left, box);
                return;
            }
        }
    }

    private void Pass(Megaman player, RoomTransition.Direction direction, Rect playerBox)
    {
        Rect door = Area;
        bool right = direction == RoomTransition.Direction.Right;
        float sign = right ? 1f : -1f;

        // The room behind it, and the camera that will scroll there
        Vector2 beyond = new(right ? door.xMax + CameraRoom.Tile : door.xMin - CameraRoom.Tile, door.center.y);
        CameraRoom to = CameraRoom.At(beyond);
        RoomCamera camera = null;
        RoomTransition transition = null;
        foreach (RoomCamera c in RoomCamera.All)
            if (c.TryGetComponent(out transition)) { camera = c; break; }
        if (to == null || camera == null || transition == null || to == camera.CurrentRoom) return;

        // Carried until his whole box is a tile past the door
        float carry = right ? door.xMax + CameraRoom.Tile - playerBox.xMin : playerBox.xMax - (door.xMin - CameraRoom.Tile);
        if (transition.Through(camera, to, direction, carry / CameraRoom.Tile, Open(), Close()))
        {
            busy = true;
            if (player.FacingRight != right) player.Flip();
        }
    }

    // Segment by segment, from the bottom up; then it's no longer solid
    private IEnumerator Open()
    {
        Play(openSound);
        for (int h = heightTiles - 1; h >= 0; h--)
        {
            yield return new WaitForSecondsRealtime(secondsPerSegment);
            Shape(h);
        }
        solid.enabled = false;
    }

    private IEnumerator Close()
    {
        solid.enabled = true;
        Play(closeSound);
        for (int h = 1; h <= heightTiles; h++)
        {
            yield return new WaitForSecondsRealtime(secondsPerSegment);
            Shape(h);
        }
        locked = oneWay;
        busy = false;
    }

    // The shutter `h` tiles tall, hanging from the top of the door
    private void Shape(int h)
    {
        if (shutter == null) return;
        shutter.enabled = h > 0;
        if (shutter.drawMode == SpriteDrawMode.Simple || h <= 0) return;
        shutter.size = new Vector2(widthTiles, h) * CameraRoom.Tile;
        if (shutter.transform == transform) return;   // can't move itself: only a child shutter keeps its top fixed
        shutter.transform.localPosition = new Vector3(0f, (heightTiles - h) / 2f * CameraRoom.Tile, 0f);
    }

    private static void Play(AudioEvent sound)
    {
        if (sound != null && Audio.IsReady) Audio.Play(sound);
    }

    private void OnDrawGizmos()
    {
        Rect door = Area;
        Gizmos.color = locked ? Color.gray : new Color(0.3f, 0.8f, 1f);
        Gizmos.DrawWireCube(door.center, door.size);
    }
}
