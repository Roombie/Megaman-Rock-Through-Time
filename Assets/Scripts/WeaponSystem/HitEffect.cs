using System;
using UnityEngine;

/// <summary>What a BasicProjectile does to what it hits. Add several in the projectile's Inspector.</summary>
[Serializable]
public abstract class HitEffect
{
    public abstract void Apply(BasicProjectile p, Collider2D hit, IDamageable target);
}

[Serializable, DisplayInMenu("Damage", "The shot's damage (charge level / weapon), or the target's weakness value.")]
public class DamageEffect : HitEffect
{
    [Tooltip("Multiplies the shot's damage.")]
    public float multiplier = 1f;

    public override void Apply(BasicProjectile p, Collider2D hit, IDamageable target)
    {
        int amount = Mathf.RoundToInt(p.Shot.damage * multiplier);
        target.TakeDamage(new DamageInfo(amount, p.Shot.weapon, p.gameObject, p.Direction));
    }
}

[Serializable, DisplayInMenu("Splash Damage", "Also hurts everything around the hit.")]
public class SplashDamageEffect : HitEffect
{
    public float radius = 1.5f;
    public int damage = 1;
    public LayerMask layers = ~0;
    [Tooltip("Spawned at the hit (optional).")]
    public GameObject effectPrefab;

    private static readonly Collider2D[] hits = new Collider2D[16];

    public override void Apply(BasicProjectile p, Collider2D hit, IDamageable target)
    {
        if (effectPrefab != null) UnityEngine.Object.Instantiate(effectPrefab, p.Position, Quaternion.identity);
        int count = Physics2D.OverlapCircle(p.Position, radius, new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = true }, hits);
        for (int i = 0; i < count; i++)
        {
            if (!DamageInfo.TryFind(hits[i], out IDamageable other) || other == target) continue;
            other.TakeDamage(new DamageInfo(damage, p.Shot.weapon, p.gameObject, p.Direction));
        }
    }
}

[Serializable, DisplayInMenu("Freeze", "Freezes the enemy in place for a while (Ice Slasher).")]
public class FreezeEffect : HitEffect
{
    public float duration = 2f;

    public override void Apply(BasicProjectile p, Collider2D hit, IDamageable target)
    {
        if (target is EnemyController enemy) enemy.FreezeFor(duration);
    }
}

[Serializable, DisplayInMenu("Spawn Prefab", "Spawns something where it hits (sparks, explosion...).")]
public class SpawnOnHitEffect : HitEffect
{
    public GameObject prefab;

    public override void Apply(BasicProjectile p, Collider2D hit, IDamageable target)
    {
        if (prefab != null) UnityEngine.Object.Instantiate(prefab, p.Position, Quaternion.identity);
    }
}
