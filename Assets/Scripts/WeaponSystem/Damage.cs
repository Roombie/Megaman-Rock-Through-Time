using UnityEngine;

/// <summary>Anything weapons can hurt: enemies, bosses, breakable blocks...</summary>
public interface IDamageable
{
    /// <summary>Returns true if the hit landed (false when invincible, immune...).</summary>
    bool TakeDamage(in DamageInfo info);
}

/// <summary>Lets the editor's Weapon Preview show how much a hit would do (weakness tables...).</summary>
public interface IDamagePreview
{
    /// <summary>Damage this hit would do, without applying it (0 = immune).</summary>
    int PreviewDamage(in DamageInfo info);
}

/// <summary>One hit: how much, from which weapon (for weakness tables), from where.</summary>
public struct DamageInfo
{
    public int amount;
    public WeaponData weapon;     // null for contact damage, explosions...
    public GameObject source;     // the projectile
    public Vector2 direction;     // travel direction of the hit

    public DamageInfo(int amount, WeaponData weapon = null, GameObject source = null, Vector2 direction = default)
    {
        this.amount = amount;
        this.weapon = weapon;
        this.source = source;
        this.direction = direction;
    }

    public static bool TryFind(Component hit, out IDamageable target)
    {
        target = hit.GetComponentInParent<IDamageable>();
        return target != null;
    }
}

/// <summary>Damage a specific weapon does to a specific enemy (Mega Man weakness tables). 0 = immune.</summary>
[System.Serializable]
public struct WeaponDamage
{
    public WeaponData weapon;
    [Min(0)] public int damage;
}

public static class WeaknessTable
{
    /// <summary>The damage a hit does with a weakness table: the weapon's entry if it has one, else the hit's amount.</summary>
    public static int Resolve(WeaponDamage[] table, in DamageInfo info)
    {
        if (info.weapon != null && table != null)
            foreach (var w in table)
                if (w.weapon == info.weapon) return w.damage;
        return info.amount;
    }
}
