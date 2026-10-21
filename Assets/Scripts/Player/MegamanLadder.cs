using UnityEngine;

/// <summary>Ladders: grab from below or from the top platform, climb, pause, shoot while hanging, jump off.</summary>
[RequireComponent(typeof(Megaman))]
public class MegamanLadder : MonoBehaviour
{
    [HideInInspector] public LadderHandlers ladder;   // the ladder he's at (found every frame), or null

    public bool IsClimbing { get; private set; }

    private Megaman mm;
    private MegamanStats Stats => mm.Stats;

    // He finds the ladder himself (each player his own), reaching this far below his feet: standing on the
    // platform at a ladder's top, his box is a hair above the ladder (MegamanMover keeps a gap from the ground)
    private const float ReachBelowFeet = 0.05f;
    private static readonly Collider2D[] overlaps = new Collider2D[8];

    private void Awake() => mm = GetComponent<Megaman>();

    private LadderHandlers FindLadder()
    {
        Rect b = mm.Mover.BoxAt(mm.Body.position);
        b.yMin -= ReachBelowFeet;
        ContactFilter2D filter = ContactFilter2D.noFilter;   // ladders are triggers, on any layer
        int count = Physics2D.OverlapBox(b.center, b.size, 0f, filter, overlaps);

        // The nearest one across, if ladders are side by side
        LadderHandlers nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!overlaps[i].isTrigger || !overlaps[i].TryGetComponent(out LadderHandlers found)) continue;
            float distance = Mathf.Abs(found.transform.position.x - b.center.x);
            if (distance < best)
            {
                best = distance;
                nearest = found;
            }
        }
        return nearest;
    }

    /// <summary>Climbing up and close to the top end (animation).</summary>
    public bool IsClimbingToTop => ladder != null && IsClimbing && Mathf.Abs(transform.position.y - ladder.posTopHandlerY) < 0.5f;

    public void HandleClimbing()
    {
        ladder = FindLadder();
        if (ladder == null)
        {
            if (IsClimbing) EndClimbing();
            return;
        }

        bool onPlatformLadder = IsOnPlatformLadder();
        bool atTop = IsAtLadderTop();
        bool atBottom = IsAtLadderBottom();
        bool grounded = mm.IsGrounded();
        bool shooting = mm.Weapons.IsShooting;
        float y = mm.MoveInput.y;

        bool shouldStartClimbing =
            // grounded on the platform above it, pressing down
            (grounded && onPlatformLadder && y < 0 && !shooting) ||
            // grounded below it, pressing up
            (grounded && !atBottom && !onPlatformLadder && y > 0 && !shooting) ||
            // in the air, pressing up or down
            (!grounded && y != 0 && !shooting);

        bool shouldPauseClimbing = y == 0 || shooting;

        if (shouldStartClimbing) StartClimbing();
        else if (shouldPauseClimbing && IsClimbing) PauseClimbing();

        if (!IsClimbing) return;

        // Shooting from the ladder can turn around
        if (shooting) mm.FaceInput();

        // Reached the ground before the bottom while climbing down
        if (mm.IsGrounded() && !atBottom && !atTop && !onPlatformLadder && y < 0) EndClimbing();

        // Jump off when not moving vertically
        if (y == 0 && mm.JumpPressed) EndClimbing();
    }

    private void StartClimbing()
    {
        IsClimbing = true;
        mm.Mover.NoClip = true;     // climbs through the platform at the top
        mm.Animator.speed = 1;
        float ladderX = ladder.transform.position.x;   // centre on the ladder (once: a teleport skips interpolation)
        if (!Mathf.Approximately(mm.Body.position.x, ladderX)) mm.Mover.Teleport(new Vector2(ladderX, mm.Body.position.y));
        mm.Velocity = new Vector2(0, mm.MoveInput.y * Stats.climbSpeed);
    }

    private void PauseClimbing()
    {
        mm.Velocity = Vector2.zero;
        mm.Animator.speed = 0;
    }

    public void EndClimbing()
    {
        if (!IsClimbing) return;
        bool climbingUp = mm.Velocity.y > 0f;
        mm.JumpPressed = false;   // don't jump straight away
        IsClimbing = false;
        mm.Animator.speed = 1;
        mm.Mover.NoClip = false;
        mm.Velocity = Vector2.zero;
        // Off the top he's just above the platform: standing on it at once, not falling the last hair (which
        // flashed the jump animation). Only climbing up: starting down from the platform the climb can end for a
        // moment while he's still at it, and he must drop through it then, not be put back on top.
        if (climbingUp) mm.Mover.SnapToGround(LandingReach);
    }

    private const float LandingReach = 0.25f;   // ground at most this far below his feet: he's put on it

    private bool IsAtLadderTop() => ladder != null && IsClimbing && transform.position.y + 0.24f > ladder.posTopHandlerY;
    private bool IsAtLadderBottom() => ladder != null && IsClimbing && transform.position.y + 1.5f < ladder.posBottomHandlerY;
    private bool IsOnPlatformLadder() => ladder != null && transform.position.y > ladder.posPlatformY;
}
