using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The NES screen transition between rooms (on the camera, next to RoomCamera): when the player crosses into a
/// neighbouring room the game freezes, the camera scrolls into the new room at a steady speed and the players are
/// carried a little way past the edge, then the game goes on in the new room.
/// Everything is frozen with the time scale (enemies, shots, physics, animations); the scroll runs on real time.
/// Hooks: Started / Ended (boss doors, despawning enemies, checkpoints, music...).
/// </summary>
[RequireComponent(typeof(RoomCamera))]
public class RoomTransition : MonoBehaviour
{
    public enum Direction { Up, Down, Left, Right }

    [Header("Speed (NES defaults)")]
    [Tooltip("Seconds to scroll one screen up or down (NES: about 1).")]
    [Min(0.05f)] public float secondsPerScreenVertical = 1f;
    [Tooltip("Seconds to scroll one screen left or right (NES boss doors: about 1.5).")]
    [Min(0.05f)] public float secondsPerScreenHorizontal = 1.5f;

    [Header("Players")]
    [Tooltip("How far the players are carried past the edge during a vertical transition (tiles).")]
    [Min(0f)] public float carryVertical = 1.5f;
    [Tooltip("How far the players are carried past the edge during a horizontal transition (tiles).")]
    [Min(0f)] public float carryHorizontal = 1.5f;

    /// <summary>A transition is scrolling right now (on any camera): menus and pausing should wait.</summary>
    public static bool AnyRunning => running > 0;
    private static int running;

    /// <summary>A transition starts / has ended: from, to, direction.</summary>
    public static event Action<CameraRoom, CameraRoom, Direction> Started, Ended;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Starts scrolling from one room to the next (RoomCamera calls this when its target crosses into `to`).
    /// False: it can't now, and the camera just changes rooms.
    /// </summary>
    public bool Begin(RoomCamera camera, CameraRoom from, CameraRoom to, Vector2 targetPoint)
    {
        if (IsRunning) return true;   // already on its way
        Direction direction = DirectionOf(from.Bounds, targetPoint);
        bool vertical = direction is Direction.Up or Direction.Down;
        StartCoroutine(Run(camera, from, to, direction, vertical ? carryVertical : carryHorizontal, null, null));
        return true;
    }

    /// <summary>
    /// A transition something else starts (a boss door): from the camera's room into `to`, carrying the players
    /// `carryTiles` that way. `beforeScroll` runs once the game is frozen (the door opening), `afterScroll` once the
    /// camera is in the new room (the door closing). False: one is already running.
    /// </summary>
    public bool Through(RoomCamera camera, CameraRoom to, Direction direction, float carryTiles,
        IEnumerator beforeScroll = null, IEnumerator afterScroll = null)
    {
        if (IsRunning || camera == null || to == null) return false;
        StartCoroutine(Run(camera, camera.CurrentRoom, to, direction, carryTiles, beforeScroll, afterScroll));
        return true;
    }

    // Which edge of the old room the target went past
    private static Direction DirectionOf(Rect from, Vector2 point)
    {
        if (point.y > from.yMax) return Direction.Up;
        if (point.y < from.yMin) return Direction.Down;
        return point.x > from.xMax ? Direction.Right : Direction.Left;
    }

    private IEnumerator Run(RoomCamera camera, CameraRoom from, CameraRoom to, Direction direction, float carryTiles,
        IEnumerator beforeScroll, IEnumerator afterScroll)
    {
        IsRunning = true;
        running++;
        camera.Driven = true;
        camera.IncomingRoom = to;

        // Freeze the game: time stops, and the players don't act on input meanwhile
        float timeScaleBefore = Time.timeScale;
        Time.timeScale = 0f;
        var frozen = new List<Megaman>();
        foreach (Megaman player in Players.All)
        {
            if (player.IsInputFrozen) continue;
            player.FreezeInput(true);
            frozen.Add(player);
        }
        Started?.Invoke(from, to, direction);
        if (beforeScroll != null) yield return beforeScroll;

        // Where everyone goes: the players a little way past the edge, the camera framing player 1 there
        Vector2 step = direction switch
        {
            Direction.Up => Vector2.up,
            Direction.Down => Vector2.down,
            Direction.Right => Vector2.right,
            _ => Vector2.left,
        } * carryTiles * CameraRoom.Tile;

        var starts = new List<(Megaman player, Vector2 position)>();
        foreach (Megaman player in Players.All) starts.Add((player, player.Body.position));

        Transform target = camera.Target;
        Vector2 targetEnd = target != null ? (Vector2)target.position + step + camera.targetOffset : to.Bounds.center;
        Vector2 cameraStart = camera.Position;
        Vector2 cameraEnd = camera.Framed(targetEnd, to);

        // Steady speed: as long as the distance needs (a full screen takes secondsPerScreen)
        Vector2 distance = cameraEnd - cameraStart;
        float duration = Mathf.Max(
            Mathf.Abs(distance.x) / CameraRoom.ScreenSize.x * secondsPerScreenHorizontal,
            Mathf.Abs(distance.y) / CameraRoom.ScreenSize.y * secondsPerScreenVertical);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            camera.SnapTo(Vector2.Lerp(cameraStart, cameraEnd, k));
            MovePlayers(starts, step * k);
            yield return null;
        }
        camera.SnapTo(cameraEnd);
        MovePlayers(starts, step);

        // Go on in the new room
        camera.EnterRoom(to);
        camera.IncomingRoom = null;
        if (afterScroll != null) yield return afterScroll;
        camera.Driven = false;
        Time.timeScale = timeScaleBefore;
        foreach (Megaman player in frozen)
            if (player != null) player.FreezeInput(false);

        running--;
        IsRunning = false;
        Ended?.Invoke(from, to, direction);
    }

    private static void MovePlayers(List<(Megaman player, Vector2 position)> starts, Vector2 offset)
    {
        foreach (var (player, position) in starts)
            if (player != null) player.Mover.Teleport(position + offset);
    }

    private void OnDisable()
    {
        // Never leave the game frozen if the camera goes away mid-transition
        if (!IsRunning) return;
        StopAllCoroutines();
        Time.timeScale = 1f;
        foreach (Megaman player in Players.All) player.FreezeInput(false);
        running = Mathf.Max(0, running - 1);
        IsRunning = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        running = 0;
        Started = Ended = null;
    }
}
