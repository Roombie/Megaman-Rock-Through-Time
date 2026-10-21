using System;
using UnityEngine;

/// <summary>The buttons gameplay cares about, as flags (held this frame).</summary>
[Flags]
public enum PlayerButtons : ushort
{
    None = 0,
    Jump = 1 << 0,
    Shoot = 1 << 1,
    Slide = 1 << 2,
    NextWeapon = 1 << 3,
    PreviousWeapon = 1 << 4,
}

/// <summary>
/// One player's input at a moment: the stick and the buttons held. Everything gameplay reads from a player goes
/// through this, never a keyboard or gamepad directly, so where it comes from can change: a local PlayerInput
/// today, another player's machine (online) later. Presses and releases are worked out by comparing with the
/// previous commands (see Pressed / Released).
/// Plain data (no references), small and easy to send or record.
/// </summary>
[Serializable]
public struct PlayerCommands : IEquatable<PlayerCommands>
{
    public Vector2 Move;
    public PlayerButtons Held;

    public readonly bool IsHeld(PlayerButtons button) => (Held & button) != 0;

    /// <summary>Held now but not in `previous`.</summary>
    public readonly bool Pressed(PlayerButtons button, PlayerCommands previous) => IsHeld(button) && !previous.IsHeld(button);

    /// <summary>Held in `previous` but not now.</summary>
    public readonly bool Released(PlayerButtons button, PlayerCommands previous) => !IsHeld(button) && previous.IsHeld(button);

    public readonly bool Equals(PlayerCommands other) => Move == other.Move && Held == other.Held;
    public override readonly bool Equals(object obj) => obj is PlayerCommands other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Move, Held);
}

/// <summary>Where a player's commands come from (LocalPlayerInput; a network source later).</summary>
public interface IPlayerInputSource
{
    /// <summary>0 = player 1.</summary>
    int PlayerIndex { get; }

    PlayerCommands ReadCommands();
}
