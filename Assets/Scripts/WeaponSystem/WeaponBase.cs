using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base of every fire mode. A weapon prefab has one of these; the player spawns it once (as a child) when it
/// gets the weapon and feeds it the shoot button each frame. Subclass it for a new kind of weapon, or use the
/// ready ones: SingleShotWeapon, ChargeShotWeapon, HoldBeamWeapon.
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    public WeaponData Data { get; private set; }
    protected IWeaponUser User { get; private set; }

    private readonly List<GameObject> activeShots = new();

    /// <summary>Mega Man's shooting pose.</summary>
    public virtual bool IsShooting => false;

    public void Setup(WeaponData data, IWeaponUser user)
    {
        Data = data;
        User = user;
    }

    public virtual void OnEquip() { }

    public virtual void OnUnequip() => Cancel();

    /// <summary>Stops anything in progress (charge, pending shot, beam). Safe to call any time.</summary>
    public virtual void Cancel() { }

    /// <summary>The user got hit.</summary>
    public virtual void OnUserHurt() { }

    /// <summary>Called every frame while equipped and the player can act.</summary>
    public abstract void Tick(WeaponInput input);

    #region Helpers for fire modes
    protected bool CanAfford(int cost) => Data.usesHealthAsEnergy || cost <= 0 || Data.CurrentEnergy >= cost;

    protected void Spend(int cost)
    {
        if (Data.usesHealthAsEnergy || cost <= 0) return;
        Data.CurrentEnergy -= cost;
        User.OnEnergyChanged(Data);
    }

    protected int ActiveShots
    {
        get
        {
            activeShots.RemoveAll(s => s == null);
            return activeShots.Count;
        }
    }

    protected bool HasRoomOnScreen => !Data.limitBulletsOnScreen || ActiveShots < Data.maxBulletsOnScreen;

    /// <summary>Spawns a projectile at the muzzle and starts it.</summary>
    protected GameObject Fire(GameObject prefab, int damage, float speed, int level = 0)
    {
        if (prefab == null)
        {
            Debug.LogError($"{name}: no projectile prefab to fire.", this);
            return null;
        }

        var shot = Instantiate(prefab, User.MuzzlePosition, Quaternion.identity);
        activeShots.Add(shot);
        if (shot.TryGetComponent(out Projectile projectile))
        {
            projectile.Initialize(new ShotInfo
            {
                weapon = Data,
                owner = User.transform.gameObject,
                damage = damage,
                speed = speed,
                level = level,
                facingRight = User.FacingRight,
            });
        }
        return shot;
    }

    protected static void PlaySound(AudioEvent sound) => Audio.Play(sound);
    #endregion
}
