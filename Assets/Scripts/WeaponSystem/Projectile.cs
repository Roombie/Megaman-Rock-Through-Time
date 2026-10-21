using System;
using UnityEngine;

/// <summary>
/// Base of everything a weapon fires. The weapon calls Initialize with the shot (damage, speed, direction).
/// Use BasicProjectile for most weapons (movement + hit effects picked in the Inspector), or subclass this
/// for something special (e.g. MagnetBeam).
/// </summary>
public abstract class Projectile : MonoBehaviour
{
    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected Vector2 direction = Vector2.right;

    public ShotInfo Shot { get; protected set; }
    public Vector2 Direction
    {
        get => direction;
        set
        {
            direction = value;
            if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f) spriteRenderer.flipX = direction.x < 0;
        }
    }
    public Rigidbody2D Body => rb;

    /// <summary>Raised when the projectile is gone (frees its on-screen slot).</summary>
    public event Action Destroyed;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public virtual void Initialize(ShotInfo shot)
    {
        Shot = shot;
        direction = shot.Direction;
        if (spriteRenderer != null) spriteRenderer.flipX = direction.x < 0;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (DamageInfo.TryFind(other, out IDamageable target)) OnHit(other, target);
    }

    /// <summary>Touched something that can take damage.</summary>
    protected virtual void OnHit(Collider2D other, IDamageable target) { }

    // Off-screen shots are gone (override for platforms, boomerangs...)
    protected virtual void OnBecameInvisible() => Destroy(gameObject);

    protected virtual void OnDestroy() => Destroyed?.Invoke();
}
