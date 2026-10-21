using UnityEngine;

/// <summary>
/// Walking (with the NES 1-pixel step), jumping (buffer, coyote time, extra jumps) and gravity. Sets his velocity;
/// MegamanMover moves him and handles collisions and slopes.
/// </summary>
[RequireComponent(typeof(Megaman))]
public class MegamanMotor : MonoBehaviour
{
    [SerializeField] private AudioEvent landSound;

    public bool IsMoving { get; private set; }
    public bool HasStepped { get; private set; }

    private Megaman mm;
    private MegamanStats Stats => mm.Stats;

    private float stepTimer;
    private bool isJumping;
    private bool isFalling;
    private bool inAirFromJump;
    private int extraJumpCount;

    private void Awake() => mm = GetComponent<Megaman>();

    #region Movement
    public void Move()
    {
        if (mm.Slide.IsSliding || mm.Ladder.IsClimbing) return;

        float x = mm.MoveInput.x;
        if (x != 0)
        {
            if (!IsMoving)
            {
                // Start moving with a step delay
                IsMoving = true;
                HasStepped = false;
                stepTimer = Stats.stepDelay;
            }
            else
            {
                bool grounded = mm.IsGrounded();
                // Direction changed while falling: no step
                bool directionChangedWhileFalling = Mathf.Sign(x) != Mathf.Sign(mm.Velocity.x) && !grounded;

                if (Stats.useStepDelay && !HasStepped && grounded && !directionChangedWhileFalling)
                {
                    // The short step before walking
                    mm.Velocity = new Vector2(x * Stats.stepDistance, mm.Velocity.y);
                    stepTimer -= Time.deltaTime;
                    if (stepTimer <= 0)
                    {
                        HasStepped = true;
                        stepTimer = Stats.stepDelay;
                    }
                }
                else
                {
                    mm.Velocity = new Vector2(x * Stats.speed, mm.Velocity.y);
                    HasStepped = true;
                }
            }
        }
        else
        {
            IsMoving = false;
            HasStepped = false;
            mm.Velocity = new Vector2(0, mm.Velocity.y);
        }

        mm.FaceInput();
    }
    #endregion

    #region Jumping
    public void CheckJump()
    {
        if (mm.Ladder.IsClimbing) return;

        bool grounded = mm.IsGrounded();

        if (grounded)
        {
            inAirFromJump = false;
            isJumping = false;
            extraJumpCount = Stats.maxExtraJumps;
        }
        else if (mm.Velocity.y < 0)
        {
            isFalling = true;
        }

        // Landing sound after falling
        if (grounded && isFalling)
        {
            Audio.Play(landSound);
            isFalling = false;
        }

        // Jump ends at its peak
        if (isJumping && mm.Velocity.y <= 0) isJumping = false;

        // No jumping out of a slide under a low ceiling
        if (mm.Slide.IsSliding && mm.IsColAbove())
        {
            mm.JumpPressed = false;
            return;
        }

        // Down + jump slides (when enabled)
        if (mm.Slide.CanSlideWithDownJump() && grounded && !mm.Slide.IsSliding)
        {
            mm.Slide.PerformSlide();
            mm.JumpPressed = false;
            return;
        }

        if (mm.JumpPressed && Time.time - mm.LastJumpTime <= Stats.jumpBufferTime)
        {
            // Normal jump or coyote time jump
            if (grounded || (Time.time - mm.LastGroundedTime <= Stats.coyoteTime && !inAirFromJump))
            {
                Jump(Stats.jumpForce);
            }
            else if (!grounded && extraJumpCount > 0 && !isJumping)
            {
                Jump(Stats.extraJumpForce);
                extraJumpCount--;
            }
        }

        // Variable jump height: releasing the button mid-air cuts the rise
        if (!mm.JumpPressed && mm.Velocity.y > 0 && inAirFromJump)
            mm.Velocity = new Vector2(mm.Velocity.x, mm.Velocity.y * 0.5f);
    }

    private void Jump(float force)
    {
        isJumping = true;
        inAirFromJump = true;
        mm.Velocity = new Vector2(mm.Velocity.x, force);
        mm.LastJumpTime = Time.time;
    }
    #endregion

    #region Gravity
    /// <summary>
    /// Gravity, every physics step (a fixed amount per step, as before). MegamanMover then moves him and lands him:
    /// on the ground or a slope it cancels the fall, so on a slope he stays put instead of sliding down.
    /// </summary>
    public void ApplyGravity()
    {
        if (mm.Mover.Frozen || mm.Mover.NoClip) return;   // frozen, climbing
        float gravity = mm.IsInWater() ? Stats.gravityScale * Stats.waterGravityScale : Stats.gravityScale;
        mm.Velocity += gravity * Vector2.down;
    }
    #endregion
}
