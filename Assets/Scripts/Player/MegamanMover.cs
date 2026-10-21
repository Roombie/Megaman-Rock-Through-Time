using UnityEngine;

/// <summary>
/// Mega Man's own movement and collision, NES style: no physics forces and no solver pushing him around. Each
/// physics step it moves his box by Velocity and stops it at solid ground with box casts. Works with any collider
/// shape: boxes, polygons, tilemaps (with or without a composite), rotated boxes, SlopeBlocks.
///
/// Slopes need nothing special. Walking, each step his box goes up by the most a walkable slope could rise over
/// that step (tan maxSlopeAngle × the step), across, then back down onto whatever is under it. So he walks up and
/// down any slope up to maxSlopeAngle at his normal speed; steeper ones and walls stop him. His box rests on the
/// highest point under it (a slope's corner, a peak's tip); MegamanVisual lowers the sprite onto the surface.
///
/// Solid: non-trigger colliders on the Ground layers (MegamanStats.groundLayer), except things with a Dynamic
/// Rigidbody2D (items...) unless they're one-way platforms (the balloon). One-way platforms (PlatformEffector2D
/// with Use One Way) only stop him falling onto them from above.
/// The Rigidbody2D is Kinematic and moved with MovePosition, so it still interpolates and still meets triggers
/// (ladders, water, items, enemies).
///
/// The modules only set Velocity (and gravity, in MegamanMotor); Megaman calls Step every FixedUpdate.
/// Added by Megaman at runtime if the prefab doesn't have it.
/// </summary>
[DisallowMultipleComponent]
public class MegamanMover : MonoBehaviour
{
    // Gap kept from solid ground. More than the physics shapes' own contact skin (0.01 each), or casts would see
    // the ground he stands on as already touched (distance 0)
    private const float Skin = 0.02f;

    /// <summary>Units per second. Gravity is added by MegamanMotor; landing or a ceiling zeroes Y, a wall zeroes X.</summary>
    public Vector2 Velocity;

    /// <summary>Standing on something (as of the last physics step).</summary>
    public bool Grounded { get; private set; }
    /// <summary>What he stands on is sloped (its normal isn't straight up).</summary>
    public bool OnSlope => Grounded && GroundNormal.y < 0.999f;
    public Vector2 GroundNormal { get; private set; } = Vector2.up;
    /// <summary>The collider he stands on (null in the air).</summary>
    public Collider2D GroundCollider { get; private set; }
    /// <summary>Stopped by a wall / a ceiling during the last physics step.</summary>
    public bool HitWall { get; private set; }
    public bool HitCeiling { get; private set; }

    /// <summary>Doesn't move at all (item refills, death...).</summary>
    public bool Frozen { get; set; }
    /// <summary>Moves by Velocity through everything (ladders: climbing through the platform at the top).</summary>
    public bool NoClip { get; set; }

    private Megaman mm;
    private Rigidbody2D body;
    private BoxCollider2D box;

    private static readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private static readonly Collider2D[] overlaps = new Collider2D[16];

    private MegamanStats Stats => mm.Stats;
    private float MaxSlopeTan => Mathf.Tan(Stats.maxSlopeAngle * Mathf.Deg2Rad);

    private void Awake()
    {
        mm = GetComponent<Megaman>();
        body = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;   // triggers and contacts with static colliders too (ladders, water)
        body.linearVelocity = Vector2.zero;
    }

    /// <summary>Puts him somewhere at once (no interpolation from where he was).</summary>
    public void Teleport(Vector2 position)
    {
        body.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    /// <summary>
    /// Puts him on the ground right now if it's at most `maxDistance` below his feet (grounded this frame, not
    /// after falling onto it: e.g. off the top of a ladder). False: no ground that close.
    /// </summary>
    public bool SnapToGround(float maxDistance)
    {
        Vector2 position = body.position;
        Rect b = BoxAt(position);
        if (!Cast(b.center, b.size, Vector2.down, maxDistance + Skin, b.yMin, out RaycastHit2D floor)) return false;
        position.y -= floor.distance - Skin;
        Teleport(position);
        Velocity.y = 0f;
        SetGround(floor);
        return true;
    }

    #region Step
    public void Step(float dt)
    {
        HitWall = HitCeiling = false;
        if (Frozen) return;

        Vector2 position = body.position;
        if (NoClip)
        {
            position += Velocity * dt;
            body.MovePosition(position);
            Rect b = BoxAt(position);
            SetGround(Cast(b.center, b.size, Vector2.down, 0.05f + Skin, b.yMin, out RaycastHit2D ladderGround) ? ladderGround : default);
            return;
        }

        Depenetrate(ref position);

        // Carried by what he stands on (a rising balloon...)
        Rigidbody2D ground = GroundCollider != null ? GroundCollider.attachedRigidbody : null;
        if (Grounded && ground != null && ground.bodyType != RigidbodyType2D.Static)
            position += ground.linearVelocity * dt;

        float dx = Velocity.x * dt, dy = Velocity.y * dt;
        if (Grounded && dy <= 0f)
        {
            WalkX(ref position, dx);
            MoveY(ref position, dy);
        }
        else if (dy > 0f)
        {
            // Rising: up first, so jumping while walking up a slope doesn't catch on it
            MoveY(ref position, dy);
            MoveX(ref position, dx);
        }
        else
        {
            MoveX(ref position, dx);
            MoveY(ref position, dy);
        }

        body.MovePosition(position);
    }

    // Pushed out of anything solid his box is inside (placed partly in the floor, something moved into him...),
    // the shortest way out. Uses the collider where the physics has it: call before moving him this step.
    private void Depenetrate(ref Vector2 position)
    {
        Rect b = BoxAt(position);
        int count = Physics2D.OverlapBox(b.center, b.size, 0f, Filter(), overlaps);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = overlaps[i];
            if (!IsSolid(other) || IsOneWay(other)) continue;
            // Only really inside it: resting on a slope, the skin straight down is less than that at right angles to
            // the slope, so the shapes' own contact skins read as a slight overlap. Pushing him out of that every
            // step would bounce him on the slope.
            ColliderDistance2D gap = box.Distance(other);
            if (!gap.isValid || gap.distance > -Skin) continue;
            // Moving his box (A) by pointB - pointA separates them
            position += gap.pointB - gap.pointA;
        }
    }

    // On the ground: up by the most a walkable slope could rise over this step, across, then back down onto the
    // ground (as far as it could fall away under him: walking down a slope). Nothing under him: he walked off a
    // ledge, and starts falling from his own height.
    private void WalkX(ref Vector2 position, float dx)
    {
        if (dx == 0f) return;
        float rise = Mathf.Abs(dx) * MaxSlopeTan + Skin;

        float up = Sweep(ref position, Vector2.up, rise, out _);
        MoveX(ref position, dx);

        Rect b = BoxAt(position);
        if (Cast(b.center, b.size, Vector2.down, up + rise + Skin, b.yMin, out RaycastHit2D floor))
            position.y -= floor.distance - Skin;
        else
            position.y -= up;
    }

    // Straight across; a wall (or a slope too steep to walk up) stops him
    private void MoveX(ref Vector2 position, float dx)
    {
        if (dx == 0f) return;
        Vector2 direction = dx > 0 ? Vector2.right : Vector2.left;
        Sweep(ref position, direction, Mathf.Abs(dx), out bool blocked);
        if (!blocked) return;
        Velocity.x = 0f;
        HitWall = true;
    }

    private void MoveY(ref Vector2 position, float dy)
    {
        if (dy > 0f)
        {
            // Rising (jump, knock-back): ceilings stop him
            Sweep(ref position, Vector2.up, dy, out bool ceiling);
            if (ceiling)
            {
                Velocity.y = 0f;
                HitCeiling = true;
            }
            SetGround(default);
            return;
        }

        // Falling, or standing (gravity pulls a little every step): lands on the highest point under his box
        Rect b = BoxAt(position);
        if (Cast(b.center, b.size, Vector2.down, -dy + Skin, b.yMin, out RaycastHit2D floor))
        {
            position.y -= floor.distance - Skin;
            Velocity.y = 0f;
            SetGround(floor);
        }
        else
        {
            position.y += dy;
            SetGround(default);
        }
    }

    // Moves the box that way, stopping a skin before anything solid. Returns how far it went.
    private float Sweep(ref Vector2 position, Vector2 direction, float distance, out bool blocked)
    {
        Rect b = BoxAt(position);
        blocked = Cast(b.center, b.size, direction, distance + Skin, b.yMin, out RaycastHit2D hit);
        float moved = blocked ? Mathf.Max(0f, hit.distance - Skin) : distance;
        position += direction * moved;
        return moved;
    }

    private void SetGround(RaycastHit2D ground)
    {
        Grounded = ground.collider != null;
        GroundCollider = ground.collider;
        GroundNormal = Grounded ? ground.normal : Vector2.up;
    }
    #endregion

    #region Queries
    /// <summary>His box (world) with its body at `position`.</summary>
    public Rect BoxAt(Vector2 position) => BoxAt(position, box.offset, box.size);

    /// <summary>A box (collider offset and size, like his BoxCollider2D's) with his body at `position`, in world.</summary>
    public Rect BoxAt(Vector2 position, Vector2 offset, Vector2 boxSize)
    {
        Vector2 scale = transform.lossyScale;
        Vector2 size = new(boxSize.x * Mathf.Abs(scale.x), boxSize.y * Mathf.Abs(scale.y));
        Vector2 centre = position + new Vector2(offset.x * scale.x, offset.y * scale.y);
        return new Rect(centre - size / 2, size);
    }

    /// <summary>
    /// Something a box of that shape (collider offset and size; e.g. his slide box) can't walk into within
    /// `distance` that way, from where he is: a wall, or a slope too steep to walk up (a walkable slope ahead
    /// isn't). Something above that box, like a low ceiling he can slide under, isn't either.
    /// </summary>
    public bool WallAhead(float direction, float distance, Vector2 offset, Vector2 boxSize)
    {
        Rect b = BoxAt(body.position, offset, boxSize);
        // Lifted as walking up a slope would (as far as a ceiling allows), then across
        float rise = distance * MaxSlopeTan + Skin;
        if (Cast(b.center, b.size, Vector2.up, rise + Skin, b.yMin, out RaycastHit2D ceiling))
            rise = Mathf.Max(0f, ceiling.distance - Skin);
        b.y += rise;
        return Cast(b.center, b.size, new Vector2(Mathf.Sign(direction), 0f), distance + Skin, b.yMin, out _);
    }

    /// <summary>Anything solid overlapping that box (one-way platforms aren't walls or ceilings).</summary>
    public bool OverlapsSolid(Vector2 centre, Vector2 size)
    {
        int count = Physics2D.OverlapBox(centre, size, 0f, Filter(), overlaps);
        for (int i = 0; i < count; i++)
            if (IsSolid(overlaps[i]) && !IsOneWay(overlaps[i])) return true;
        return false;
    }

    /// <summary>The nearest solid hit along the ray (one-way platforms count from above). None: collider is null.</summary>
    public RaycastHit2D RaycastSolid(Vector2 origin, Vector2 direction, float distance)
    {
        int count = Physics2D.Raycast(origin, direction, Filter(), hits, distance);
        RaycastHit2D nearest = default;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = hits[i];
            if (!IsSolid(hit.collider)) continue;
            if (IsOneWay(hit.collider) && (direction.y >= 0f || hit.distance <= 0f)) continue;
            if (nearest.collider == null || hit.distance < nearest.distance) nearest = hit;
        }
        return nearest;
    }

    // The nearest solid surface the box hits moving that way, facing it (already touching it counts: distance 0).
    // The cast is his whole box: a surface it slides along (the floor moving sideways, a wall beside him moving
    // up or down) doesn't face the movement, so it's skipped. A narrower box would miss a corner at its very edge
    // that his real box stands on.
    // A one-way platform only counts moving down onto it with his feet (at the start) on or above its top.
    private bool Cast(Vector2 centre, Vector2 size, Vector2 direction, float distance, float feet, out RaycastHit2D nearest)
    {
        int count = Physics2D.BoxCast(centre, size, 0f, direction, Filter(), hits, distance);
        nearest = default;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = hits[i];
            if (!IsSolid(hit.collider) || Vector2.Dot(hit.normal, -direction) <= 0.01f) continue;
            if (IsOneWay(hit.collider) && (direction.y >= 0f || feet < hit.point.y - Skin)) continue;
            if (nearest.collider == null || hit.distance < nearest.distance) nearest = hit;
        }
        return nearest.collider != null;
    }

    private ContactFilter2D Filter()
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Stats.groundLayer);
        return filter;
    }

    private static bool IsSolid(Collider2D collider)
    {
        if (collider == null || collider.isTrigger) return false;
        Rigidbody2D rb = collider.attachedRigidbody;
        return rb == null || rb.bodyType != RigidbodyType2D.Dynamic || IsOneWay(collider);
    }

    private static bool IsOneWay(Collider2D collider) =>
        collider.usedByEffector && collider.TryGetComponent(out PlatformEffector2D effector) && effector.useOneWay;
    #endregion
}
