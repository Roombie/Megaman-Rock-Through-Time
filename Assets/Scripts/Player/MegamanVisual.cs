using UnityEngine;

/// <summary>
/// The "Visual" child of Mega Man: his SpriteRenderer and Animator, apart from the physics root so the picture can
/// move without the collider. His box keeps a hair above the ground, and on a slope sits on the highest point under
/// it (see MegamanMover), so the sprite is lowered until his feet touch the ground (on a slope, the centre of his
/// feet). Animation events reach the Animator's own object, so they're passed on from here.
/// </summary>
public class MegamanVisual : MonoBehaviour
{
    private const float RayStart = 0.05f;   // above his feet, so the ray doesn't start inside the ground
    private const float MaxFlatGap = 0.1f;  // standing on flat ground his feet are never further above it

    private Megaman mm;

    private void Awake() => mm = GetComponentInParent<Megaman>();

    private void LateUpdate()
    {
        transform.localPosition = new Vector3(0f, -GroundGap(), 0f);
    }

    // How far his feet are above the ground he stands on (after the Rigidbody's interpolation, so it stays smooth).
    // On flat ground: the hair MegamanMover keeps between his box and the ground, which the pixel-perfect camera
    // would round up to a pixel. On or over a slope (whatever it's made of): from the centre of his feet to the
    // surface under it. That's also where his box stands on a slope but the centre of his feet is already over the
    // flat ground at its bottom; on the flat edge of a ledge it isn't (the sprite stays on the ledge).
    private float GroundGap()
    {
        if (!mm.IsOnGround()) return 0f;
        BoxCollider2D box = mm.BoxCollider;
        Vector2 scale = mm.transform.lossyScale;
        float centre = mm.transform.position.x + box.offset.x * scale.x;
        float feet = mm.transform.position.y + (box.offset.y - box.size.y / 2) * scale.y;
        float halfWidth = box.size.x / 2 * Mathf.Abs(scale.x);
        float maxGap = halfWidth * Mathf.Tan(mm.Stats.maxSlopeAngle * Mathf.Deg2Rad);

        RaycastHit2D under = mm.Mover.RaycastSolid(new Vector2(centre, feet + RayStart), Vector2.down, RayStart + maxGap);
        bool overSlope = under.collider != null && under.normal.y < 0.999f;
        if ((mm.OnSlope || overSlope) && under.collider != null)
            return Mathf.Clamp(under.distance - RayStart, 0f, maxGap);

        // Flat: the nearest ground under his box (one ray still finds it on a ledge's edge)
        float gap = float.MaxValue;
        for (int side = -1; side <= 1; side++)
        {
            Vector2 origin = new(centre + side * (halfWidth - 0.01f), feet + RayStart);
            RaycastHit2D hit = mm.Mover.RaycastSolid(origin, Vector2.down, RayStart + MaxFlatGap);
            if (hit.collider != null) gap = Mathf.Min(gap, hit.distance - RayStart);
        }
        return gap == float.MaxValue ? 0f : Mathf.Max(0f, gap);
    }

    // Animation event on the last frame of Megaman_Hit
    private void StopDamageAnimation() => mm.Health.StopDamageAnimation();
}
