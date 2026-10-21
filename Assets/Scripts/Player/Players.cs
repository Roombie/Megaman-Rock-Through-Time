using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Mega Mans in the game (one in the campaign, several in multiplayer), registered while enabled. Use this
/// instead of looking for "the" Mega Man: things that affect every player go through All, things that belong to
/// one player (their HUD, their camera...) ask for theirs with Get(playerIndex).
/// </summary>
public static class Players
{
    private static readonly List<Megaman> all = new();

    public static IReadOnlyList<Megaman> All => all;
    public static int Count => all.Count;

    /// <summary>A Mega Man came into the game / left it (destroyed, disabled).</summary>
    public static event Action<Megaman> Joined, Left;

    /// <summary>The Mega Man of that player (0 = player 1), or null.</summary>
    public static Megaman Get(int playerIndex)
    {
        foreach (Megaman player in all)
            if (player != null && player.PlayerIndex == playerIndex) return player;
        return null;
    }

    /// <summary>Player 1 (the campaign's Mega Man), or else whoever is in the game. Null: nobody.</summary>
    public static Megaman First => Get(0) ?? (all.Count > 0 ? all[0] : null);

    /// <summary>The nearest Mega Man to a point (for enemies, items...). Null: nobody.</summary>
    public static Megaman Nearest(Vector2 position)
    {
        Megaman nearest = null;
        float best = float.MaxValue;
        foreach (Megaman player in all)
        {
            if (player == null) continue;
            float distance = ((Vector2)player.transform.position - position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = player;
            }
        }
        return nearest;
    }

    internal static void Add(Megaman player)
    {
        if (all.Contains(player)) return;
        all.Add(player);
        Joined?.Invoke(player);
    }

    internal static void Remove(Megaman player)
    {
        if (!all.Remove(player)) return;
        Left?.Invoke(player);
    }

    // With Enter Play Mode Options (no domain reload) statics survive between play sessions: start clean
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        Joined = Left = null;
    }
}
