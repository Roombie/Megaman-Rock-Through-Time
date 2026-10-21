using System;
using UnityEngine;

/// <summary>
/// What a movement drives: the real projectile (BasicProjectile) or the editor's trajectory preview, which runs
/// the same movement code so the preview matches the game.
/// </summary>
public interface IProjectileBody
{
    ShotInfo Shot { get; }
    float Speed { get; }
    Vector2 Position { get; }
    Vector2 Direction { get; set; }
    Vector2 Velocity { get; set; }
    /// <summary>Ends the projectile (e.g. a boomerang caught by the shooter).</summary>
    void End();
}

/// <summary>How a BasicProjectile moves. Pick one in the projectile's Inspector; add your own by subclassing.</summary>
[Serializable]
public abstract class ProjectileMovement
{
    /// <summary>Called once when fired.</summary>
    public virtual void Begin(IProjectileBody p) { }

    /// <summary>Called every physics step: set p.Velocity.</summary>
    public abstract void Move(IProjectileBody p, float deltaTime);

    /// <summary>Hit a bounce surface (normal points away from it). Default: mirror the direction.</summary>
    public virtual void OnBounce(IProjectileBody p, Vector2 normal)
    {
        p.Direction = Vector2.Reflect(p.Direction, normal).normalized;
        p.Velocity = Vector2.Reflect(p.Velocity, normal);
    }

    /// <summary>A copy with its own state (each shot and each preview needs its own).</summary>
    public ProjectileMovement Clone() => (ProjectileMovement)JsonUtility.FromJson(JsonUtility.ToJson(this), GetType());
}

[Serializable, DisplayInMenu("Straight", "Flies straight at the shot's speed (Mega Buster).")]
public class StraightMovement : ProjectileMovement
{
    public override void Begin(IProjectileBody p) => p.Velocity = p.Direction * p.Speed;
    public override void Move(IProjectileBody p, float deltaTime) => p.Velocity = p.Direction * p.Speed;
}

[Serializable, DisplayInMenu("Arc", "Thrown upwards and falls with gravity (Hyper Bomb).")]
public class ArcMovement : ProjectileMovement
{
    [Tooltip("Upward speed when thrown.")]
    public float launchUpSpeed = 8f;
    [Tooltip("Downward acceleration (units/s²).")]
    public float gravity = 30f;
    public float maxFallSpeed = 20f;
    [Tooltip("Speed kept when bouncing off a floor or ceiling (1 = same height every bounce).")]
    [Range(0f, 1f)] public float bounciness = 0.7f;

    private float vertical;

    public override void Begin(IProjectileBody p)
    {
        vertical = launchUpSpeed;
        p.Velocity = new Vector2(Mathf.Sign(p.Direction.x) * p.Speed, vertical);
    }

    public override void Move(IProjectileBody p, float deltaTime)
    {
        vertical = Mathf.Max(vertical - gravity * deltaTime, -maxFallSpeed);
        p.Velocity = new Vector2(Mathf.Sign(p.Direction.x) * p.Speed, vertical);
    }

    public override void OnBounce(IProjectileBody p, Vector2 normal)
    {
        if (Mathf.Abs(normal.x) > 0.5f) p.Direction = new Vector2(-p.Direction.x, p.Direction.y);   // wall
        if (Mathf.Abs(normal.y) > 0.5f) vertical = Mathf.Abs(vertical) * bounciness * Mathf.Sign(normal.y);   // floor / ceiling
        p.Velocity = new Vector2(Mathf.Sign(p.Direction.x) * p.Speed, vertical);
    }
}

[Serializable, DisplayInMenu("Sine Wave", "Forward with an up/down wave.")]
public class SineMovement : ProjectileMovement
{
    [Tooltip("Height of the wave (units).")]
    public float amplitude = 1f;
    [Tooltip("Waves per second.")]
    public float frequency = 2f;

    private float time;

    public override void Begin(IProjectileBody p) => time = 0f;

    public override void Move(IProjectileBody p, float deltaTime)
    {
        time += deltaTime;
        // derivative of amplitude·sin(2πft): the projectile follows the wave exactly
        float up = amplitude * 2f * Mathf.PI * frequency * Mathf.Cos(2f * Mathf.PI * frequency * time);
        p.Velocity = new Vector2(Mathf.Sign(p.Direction.x) * p.Speed, up);
    }

    public override void OnBounce(IProjectileBody p, Vector2 normal)
    {
        if (Mathf.Abs(normal.x) > 0.5f) p.Direction = new Vector2(-p.Direction.x, p.Direction.y);
    }
}

[Serializable, DisplayInMenu("Homing", "Turns towards the nearest target.")]
public class HomingMovement : ProjectileMovement
{
    [Tooltip("Degrees per second it can turn.")]
    public float turnRate = 180f;
    [Tooltip("How far it looks for a target.")]
    public float range = 8f;
    [Tooltip("Seconds flying straight before it starts homing.")]
    public float delay = 0.1f;
    public LayerMask targetLayers = ~0;

    private float time;
    private Transform target;
    private static readonly Collider2D[] hits = new Collider2D[16];

    public override void Begin(IProjectileBody p)
    {
        time = 0f;
        target = null;
        p.Velocity = p.Direction * p.Speed;
    }

    public override void Move(IProjectileBody p, float deltaTime)
    {
        time += deltaTime;
        if (time >= delay)
        {
            if (target == null) target = FindTarget(p);
            if (target != null)
            {
                Vector2 wanted = ((Vector2)target.position - p.Position).normalized;
                float angle = Vector2.SignedAngle(p.Direction, wanted);
                float step = Mathf.Clamp(angle, -turnRate * deltaTime, turnRate * deltaTime);
                p.Direction = (Quaternion.Euler(0, 0, step) * p.Direction).normalized;
            }
        }
        p.Velocity = p.Direction * p.Speed;
    }

    private Transform FindTarget(IProjectileBody p)
    {
        int count = Physics2D.OverlapCircle(p.Position, range, new ContactFilter2D { useLayerMask = true, layerMask = targetLayers, useTriggers = true }, hits);
        Transform best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!DamageInfo.TryFind(hits[i], out IDamageable damageable)) continue;
            var t = ((Component)damageable).transform;
            float d = ((Vector2)t.position - p.Position).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = t; }
        }
        return best;
    }
}

[Serializable, DisplayInMenu("Boomerang", "Goes out, then comes back to the shooter (Rolling Cutter).")]
public class BoomerangMovement : ProjectileMovement
{
    [Tooltip("Seconds going out before turning back.")]
    public float outwardTime = 0.5f;
    [Tooltip("Speed multiplier on the way back.")]
    public float returnSpeedMultiplier = 1f;
    [Tooltip("Ends when it gets this close to the shooter on the way back.")]
    public float catchDistance = 0.5f;

    private float time;

    public override void Begin(IProjectileBody p)
    {
        time = 0f;
        p.Velocity = p.Direction * p.Speed;
    }

    public override void Move(IProjectileBody p, float deltaTime)
    {
        time += deltaTime;
        if (time < outwardTime || p.Shot.owner == null)
        {
            p.Velocity = p.Direction * p.Speed;
            return;
        }

        Vector2 toOwner = (Vector2)p.Shot.owner.transform.position - p.Position;
        if (toOwner.magnitude <= catchDistance)
        {
            p.End();
            return;
        }
        p.Velocity = toOwner.normalized * p.Speed * returnSpeedMultiplier;
    }
}
