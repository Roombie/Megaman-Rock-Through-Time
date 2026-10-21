using UnityEngine;

/// <summary>
/// The basic weapon: each press fires one projectile (or keeps firing while held, with Auto Fire).
/// Most special weapons are this plus a projectile prefab built from movement and hit effects.
/// </summary>
public class SingleShotWeapon : WeaponBase
{
    [Tooltip("Projectile to fire (a Projectile component, e.g. BasicProjectile).")]
    public GameObject projectilePrefab;
    [Min(0)] public int damage = 1;
    public float speed = 15f;

    [Tooltip("Minimum seconds between shots.")]
    [Min(0f)] public float fireCooldown = 0.1f;
    [Tooltip("Keep firing while the button is held.")]
    public bool autoFire;
    [Tooltip("Seconds Mega Man keeps the shooting pose after a shot.")]
    [Min(0f)] public float poseDuration = 0.25f;
    [Tooltip("Overrides the weapon's default fire sound.")]
    public AudioEvent fireSound;

    private float nextShotTime;
    private float poseUntil;

    public override bool IsShooting => Time.time < poseUntil;

    public override void Tick(WeaponInput input)
    {
        bool wantsToFire = input.pressed || (autoFire && input.held);
        if (!wantsToFire || Time.time < nextShotTime || !User.CanFire) return;
        if (!CanAfford(Data.energyCost) || !HasRoomOnScreen) return;

        Fire(projectilePrefab, damage, speed);
        Spend(Data.energyCost);
        PlaySound(fireSound != null ? fireSound : Data.weaponClip);
        nextShotTime = Time.time + fireCooldown;
        poseUntil = Time.time + poseDuration;
    }

    public override void Cancel() => poseUntil = 0f;
}
