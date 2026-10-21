using UnityEngine;

/// <summary>
/// The collision step shared by BasicProjectile and the editor's trajectory preview, so the preview's bounces
/// land exactly where the game's do.
/// </summary>
public static class ProjectileCollision
{
    private static readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    /// <summary>First solid surface on these layers the projectile (a circle) touches moving by delta.</summary>
    public static bool Cast(Vector2 from, float radius, Vector2 delta, LayerMask layers, out RaycastHit2D hit)
    {
        hit = default;
        float distance = delta.magnitude;
        if (distance <= 0f || layers.value == 0) return false;

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = false };
        int count = Physics2D.CircleCast(from, radius, delta / distance, filter, hits, distance);
        for (int i = 0; i < count; i++)
        {
            // Ignore surfaces it's already touching and moving away from (just bounced off them)
            if (hits[i].distance <= 0f && Vector2.Dot(hits[i].normal, delta) >= 0f) continue;
            hit = hits[i];
            return true;
        }
        return false;
    }

    /// <summary>Collision radius of a projectile prefab / instance (its collider, slightly smaller).</summary>
    public static float RadiusOf(GameObject projectile)
    {
        if (projectile == null) return 0.1f;
        var collider = projectile.GetComponent<Collider2D>();
        if (collider is BoxCollider2D box) return Mathf.Max(0.02f, Mathf.Min(Mathf.Abs(box.size.x * projectile.transform.localScale.x), Mathf.Abs(box.size.y * projectile.transform.localScale.y)) * 0.45f);
        if (collider is CircleCollider2D circle) return Mathf.Max(0.02f, circle.radius * Mathf.Abs(projectile.transform.localScale.x) * 0.9f);
        if (collider != null) return Mathf.Max(0.02f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.y) * 0.9f);
        return 0.1f;
    }
}
