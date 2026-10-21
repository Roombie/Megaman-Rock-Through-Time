using System.Collections;
using UnityEngine;

// Magnet Beam (Mega Man 1): a platform that grows tile by tile while the shoot button is held,
// stays solid for a while after it stops, flashes, and disappears.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class MagnetBeam : Projectile
{
    [SerializeField] private float maxBeamLength = 30f;
    [SerializeField] private float tileExtendInterval = 0.2f;
    [Tooltip("Seconds the beam stays solid after it stops extending, before it starts flashing.")]
    [SerializeField] private float lifetime = 5f;
    [Tooltip("Seconds of flashing before it disappears (it is still solid while flashing).")]
    [SerializeField] private float flashDuration = 2f;
    [SerializeField] private float flashInterval = 0.1f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private Vector2 offset = new(1f, 0f);

    private SpriteRenderer beamSpriteRenderer;
    private BoxCollider2D boxCollider;
    private float tileLength;
    private float currentBeamLength = 0f;
    private float nextTileTime = 0f;
    private bool isExtending = false;
    private bool isFinished = false;
    public bool hasReachedMaxLength = false; // Track if beam reaches max length

    /// <summary>
    /// Asked before each new tile; return false (e.g. out of energy) to stop the beam where it is.
    /// </summary>
    public System.Func<bool> canAddTile;

    // For the editor's Weapon Preview
    public float MaxBeamLength => maxBeamLength;
    public LayerMask WallLayer => wallLayer;
    public float TileLength
    {
        get
        {
            var sr = GetComponent<SpriteRenderer>();
            return sr != null && sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        beamSpriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
        tileLength = beamSpriteRenderer.sprite != null ? beamSpriteRenderer.sprite.bounds.size.x : 1f;
    }

    private void Update()
    {
        if (isExtending && Time.time >= nextTileTime)
        {
            ExtendBeam();
            nextTileTime = Time.time + tileExtendInterval;
        }
    }

    public void StartExtending()
    {
        isExtending = true;
        isFinished = false;
        hasReachedMaxLength = false;
        currentBeamLength = 0f;

        // First tile appears right away, the rest follow every tileExtendInterval
        ExtendBeam();
        nextTileTime = Time.time + tileExtendInterval;
    }

    /// <summary>
    /// Stops growing (button released, max length or wall). The beam then stays as a platform for
    /// `lifetime` seconds, flashes for `flashDuration` and disappears. Safe to call more than once.
    /// </summary>
    public void StopExtending()
    {
        if (isFinished) return;

        isFinished = true;
        isExtending = false;
        hasReachedMaxLength = true;
        StartCoroutine(LifetimeThenFlash());
    }

    public void SetBeamDirection(Vector2 direction)
    {
        this.direction = direction.normalized; // Using direction from Projectile class
        UpdateBeamFlip();
        UpdateBeamLength(currentBeamLength);
    }

    public void UpdateBeamPosition(Vector2 newPosition)
    {
        // This will be called to update the position as the player moves or extends the beam
        if (isExtending)
        {
            transform.position = newPosition;
        }
    }

    private void ExtendBeam()
    {
        float nextLength = currentBeamLength + tileLength;
        if (nextLength > maxBeamLength + 0.001f)
        {
            StopExtending();
            return;
        }

        // Don't grow into walls: stop flush against the first wall tile in front of the beam
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, nextLength, wallLayer);
        if (hit.collider != null)
        {
            float fitting = Mathf.Floor(hit.distance / tileLength + 0.001f) * tileLength;
            if (fitting > currentBeamLength)
            {
                currentBeamLength = fitting;
                UpdateBeamLength(currentBeamLength);
            }
            StopExtending();
            return;
        }

        if (canAddTile != null && !canAddTile())
        {
            StopExtending();
            return;
        }

        currentBeamLength = nextLength;
        UpdateBeamLength(currentBeamLength);
    }

    private void UpdateBeamLength(float newLength)
    {
        if (beamSpriteRenderer.drawMode == SpriteDrawMode.Tiled)
        {
            beamSpriteRenderer.size = new Vector2(newLength, beamSpriteRenderer.size.y);
        }

        if (boxCollider != null)
        {
            boxCollider.size = new Vector2(newLength, boxCollider.size.y);
            boxCollider.offset = new Vector2(direction.x < 0 ? -newLength / 2f : newLength / 2f, boxCollider.offset.y);
        }
    }

    private void UpdateBeamFlip()
    {
        if (beamSpriteRenderer != null)
        {
            beamSpriteRenderer.flipX = direction.x < 0;
        }
    }

    private IEnumerator LifetimeThenFlash()
    {
        yield return new WaitForSeconds(lifetime);

        // Flash by toggling transparency, not the renderer: disabling the renderer fires OnBecameInvisible
        Color visible = beamSpriteRenderer.color;
        Color hidden = new Color(visible.r, visible.g, visible.b, 0f);
        float elapsedTime = 0f;
        bool shown = true;

        while (elapsedTime < flashDuration)
        {
            shown = !shown;
            beamSpriteRenderer.color = shown ? visible : hidden;
            yield return new WaitForSeconds(flashInterval);
            elapsedTime += flashInterval;
        }

        Destroy(gameObject);
    }

    // The beam is a platform with its own lifetime: it must not vanish just because the camera scrolled
    // away from it (the base Projectile destroys itself when it goes off-screen).
    protected override void OnBecameInvisible() { }
}
