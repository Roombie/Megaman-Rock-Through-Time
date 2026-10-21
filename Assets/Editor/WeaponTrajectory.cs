using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Simulates a BasicProjectile without spawning it, with the same movement and collision code the game uses,
/// for the Scene view's Weapon Preview.
/// </summary>
public static class WeaponTrajectory
{
    public struct Bounce { public Vector2 position, normal; }
    public struct Hit { public Vector2 position; public string target; public int damage; }

    public class Result
    {
        public readonly List<Vector2> points = new();
        public readonly List<Bounce> bounces = new();
        public readonly List<Hit> hits = new();
        public Vector2 end;
        public string endReason;
        public float time;
        public float radius;
    }

    private class Body : IProjectileBody
    {
        public ShotInfo Shot { get; set; }
        public float Speed => Shot.speed;
        public Vector2 Position { get; set; }
        public Vector2 Direction { get; set; }
        public Vector2 Velocity { get; set; }
        public bool Ended;
        public void End() => Ended = true;
    }

    private static readonly RaycastHit2D[] castHits = new RaycastHit2D[16];

    public static Result Simulate(BasicProjectile prefab, ShotInfo shot, Vector2 start, Rect screen, float maxTime = 6f)
    {
        var result = new Result { radius = ProjectileCollision.RadiusOf(prefab.gameObject) };
        var movement = prefab.movement != null ? prefab.movement.Clone() : new StraightMovement();
        var body = new Body { Shot = shot, Position = start, Direction = shot.Direction };
        var damageEffect = prefab.hitEffects?.OfType<DamageEffect>().FirstOrDefault();
        var hitTargets = new HashSet<Collider2D>();

        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        float duration = prefab.lifetime > 0f ? Mathf.Min(prefab.lifetime, 30f) : maxTime;
        int bouncesLeft = prefab.bounces;
        LayerMask destroyMask = prefab.destroyOnLayers.value & ~(prefab.bounces > 0 ? prefab.bounceLayers.value : 0);

        result.points.Add(start);
        movement.Begin(body);

        float t = 0f;
        for (; t < duration; t += dt)
        {
            movement.Move(body, dt);
            if (body.Ended) { Finish(result, body.Position, "Caught", t); return result; }
            Vector2 delta = body.Velocity * dt;

            // Bounce surfaces (same rule as the game)
            if (prefab.bounces > 0 && ProjectileCollision.Cast(body.Position, result.radius, delta, prefab.bounceLayers, out RaycastHit2D wall))
            {
                if (bouncesLeft <= 0) { Finish(result, wall.centroid, "Wall (no bounces left)", t); return result; }
                body.Position = wall.centroid + wall.normal * 0.01f;
                movement.OnBounce(body, wall.normal);
                bouncesLeft--;
                result.bounces.Add(new Bounce { position = body.Position, normal = wall.normal });
                result.points.Add(body.Position);
                continue;
            }

            // Walls that destroy it
            if (ProjectileCollision.Cast(body.Position, result.radius, delta, destroyMask, out RaycastHit2D stop))
            {
                Finish(result, stop.centroid, "Wall", t);
                return result;
            }

            // Targets
            float distance = delta.magnitude;
            if (distance > 0f)
            {
                var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
                int count = Physics2D.CircleCast(body.Position, result.radius, delta / distance, filter, castHits, distance);
                for (int i = 0; i < count; i++)
                {
                    var c = castHits[i].collider;
                    if (hitTargets.Contains(c) || !DamageInfo.TryFind(c, out IDamageable target)) continue;
                    if (shot.owner != null && c.transform.IsChildOf(shot.owner.transform)) continue;
                    hitTargets.Add(c);

                    int amount = damageEffect != null ? Mathf.RoundToInt(shot.damage * damageEffect.multiplier) : 0;
                    var info = new DamageInfo(amount, shot.weapon, null, body.Direction);
                    if (target is IDamagePreview preview) amount = damageEffect != null ? preview.PreviewDamage(info) : 0;
                    result.hits.Add(new Hit { position = castHits[i].centroid, target = ((Component)target).name, damage = amount });

                    if (!prefab.pierce) { Finish(result, castHits[i].centroid, $"Hit {((Component)target).name}", t); return result; }
                }
            }

            body.Position += delta;
            result.points.Add(body.Position);

            if (prefab.destroyOffScreen && !screen.Contains(body.Position))
            {
                Finish(result, body.Position, "Off screen", t);
                return result;
            }
        }

        Finish(result, body.Position, prefab.lifetime > 0f ? $"Lifetime ({prefab.lifetime:0.##}s)" : $"Preview limit ({maxTime:0}s)", t);
        return result;
    }

    private static void Finish(Result r, Vector2 end, string reason, float time)
    {
        r.end = end;
        r.endReason = reason;
        r.time = time;
        if (r.points.Count == 0 || r.points[^1] != end) r.points.Add(end);
    }
}
