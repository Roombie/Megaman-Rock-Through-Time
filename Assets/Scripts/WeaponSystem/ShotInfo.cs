using UnityEngine;

/// <summary>Everything a projectile needs to know when it's fired.</summary>
public struct ShotInfo
{
    public WeaponData weapon;
    public GameObject owner;
    public int damage;
    public float speed;
    public int level;          // charge level (0 for uncharged / non-charging weapons)
    public bool facingRight;

    public Vector2 Direction => facingRight ? Vector2.right : Vector2.left;
}

/// <summary>The shoot button this frame, as fire modes see it.</summary>
public struct WeaponInput
{
    public bool held;
    public bool pressed;    // went down this frame
    public bool released;   // went up this frame
}

/// <summary>Who's holding the weapon (Mega Man). Fire modes only talk to this.</summary>
public interface IWeaponUser
{
    Transform transform { get; }
    bool FacingRight { get; }
    /// <summary>Where shots come out (world position).</summary>
    Vector2 MuzzlePosition { get; }
    /// <summary>False while sliding, hurt... (a pressed shot waits, a beam stops).</summary>
    bool CanFire { get; }
    /// <summary>Blinking after a hit (a beam can't start).</summary>
    bool IsInvincible { get; }
    bool AllowCharge { get; }
    void SetArmorColors(Color main, Color secondary, Color outline);
    void ResetArmorColors();
    void OnEnergyChanged(WeaponData weapon);
}
