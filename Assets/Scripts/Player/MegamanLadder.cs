using UnityEngine;

/// <summary>Ladders: grab from below or from the top platform, climb, pause, shoot while hanging, jump off.</summary>
[RequireComponent(typeof(Megaman))]
public class MegamanLadder : MonoBehaviour
{
    [HideInInspector] public LadderHandlers ladder;   // set by the ladder's trigger

    public bool IsClimbing { get; private set; }

    private Megaman mm;
    private MegamanStats Stats => mm.Stats;

    private void Awake() => mm = GetComponent<Megaman>();

    /// <summary>Climbing up and close to the top end (animation).</summary>
    public bool IsClimbingToTop => ladder != null && IsClimbing && Mathf.Abs(transform.position.y - ladder.posTopHandlerY) < 0.5f;

    public void HandleClimbing()
    {
        if (ladder == null || !ladder.isNearLadder)
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
        mm.JumpPressed = false;   // don't jump straight away
        IsClimbing = false;
        mm.Animator.speed = 1;
        mm.Mover.NoClip = false;
        mm.Velocity = Vector2.zero;
    }

    private bool IsAtLadderTop() => ladder != null && IsClimbing && transform.position.y + 0.24f > ladder.posTopHandlerY;
    private bool IsAtLadderBottom() => ladder != null && IsClimbing && transform.position.y + 1.5f < ladder.posBottomHandlerY;
    private bool IsOnPlatformLadder() => ladder != null && transform.position.y > ladder.posPlatformY;
}
