using UnityEngine;

/// <summary>
/// Every tuning value of Mega Man in one asset. The player components read from here, so the feel can be
/// tweaked (or a variant made: another character, an easy mode...) without touching the prefab or the code.
/// </summary>
[CreateAssetMenu(menuName = "Mega Man/Player Stats", fileName = "MegamanStats")]
public class MegamanStats : ScriptableObject
{
    [Header("Health")]
    public int maxHealth = 28;
    [Tooltip("Seconds between losing the last point of health and exploding.")]
    public float delayBeforeDeath = 0.5f;
    [Tooltip("Health at or below this (and above 0) shows the low-health smoke.")]
    public int lowHealthSmoke = 5;

    [Header("Damage")]
    public Vector2 hitPushForce = new(0.5f, 1.5f);
    [Tooltip("Blinks after the hit animation (each blink = hidden + shown).")]
    public int blinkCount = 10;
    [Tooltip("Seconds per blink half. The hit animation is 12 samples, so 1/12 s keeps it in step.")]
    public float blinkInterval = 0.0833f;

    [Header("Refills")]
    [Tooltip("Seconds between each point when health or weapon energy refills.")]
    public float refillInterval = 0.05f;

    [Header("Gravity")]
    public float gravityScale = 1f;
    public float waterGravityScale = 0.5f;

    [Header("Movement")]
    public float speed = 5f;
    public bool useStepDelay = true;
    [Tooltip("Time for the 1-pixel step.")]
    public float stepDelay = 0.1f;
    [Tooltip("Amount of the 1-pixel step.")]
    public float stepDistance = 2f;

    [Header("Jumping")]
    public float jumpForce = 18f;
    public int maxExtraJumps = 1;
    public float extraJumpForce = 12f;
    public float jumpBufferTime = 0.125f;
    public float coyoteTime = 0.125f;

    [Header("Shooting")]
    public Vector2 bulletShootOffset = new(0.5f, 1f);
    public float shootRayLength = 5f;
    [Tooltip("Mega Man can charge shots (charge-type weapons).")]
    public bool chargerEnabled = true;
    [Tooltip("Seconds the weapon switch icon stays above Mega Man.")]
    public float iconDisplayTime = 1.5f;

    [Header("Sliding")]
    public float slideSpeed = 6f;
    public float slideDuration = 0.35f;
    public Vector2 slideBoxOffset;
    public Vector2 slideBoxSize;

    [Header("Slopes")]
    [Tooltip("Steepest slope he walks up and down (degrees), whatever it's made of (tiles, polygons, rotated boxes, " +
             "SlopeBlocks). Steeper ones are walls. Steps up to tan(angle) × one physics step of walking are climbed too.")]
    [Range(0f, 75f)] public float maxSlopeAngle = 50f;

    [Header("Climbing")]
    public float climbSpeed = 3.5f;

    [Header("Under Water")]
    public LayerMask waterLayer;
    public BubbleType bubbleType = BubbleType.NextBubble;
    public float bubbleSpawnInterval = 1.25f;

    [Header("Ground Check (Ground Layer: what's solid to him. The rest is only drawn as gizmos)")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.1f;
    public Vector2 groundCheckOffset = new(0f, -0.5f);
    public float groundCheckWidth = 0.5f;

    [Header("Top Collision Check")]
    public Vector2 topCheckOffset = new(0f, 0.5f);
    public Vector2 topCheckSize = new(1f, 0.2f);

    [Header("Front Collision Check")]
    public Vector2 frontCheckOffset = new(0.5f, 0f);
    public Vector2 frontCheckSize = new(0.2f, 1f);

    public enum BubbleType
    {
        NextBubble,    // a new bubble once the previous one is gone
        TimerBased     // a bubble every bubbleSpawnInterval seconds
    }
}
