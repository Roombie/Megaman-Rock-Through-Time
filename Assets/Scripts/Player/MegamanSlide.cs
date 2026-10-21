using UnityEngine;

/// <summary>The slide: smaller collider, dust, stays under low ceilings, cancelled by turning or jumping.</summary>
[RequireComponent(typeof(Megaman))]
public class MegamanSlide : MonoBehaviour
{
    [SerializeField] private Transform slideDustPos;
    [SerializeField] private GameObject slideDustPrefab;

    public bool IsSliding { get; private set; }

    private Megaman mm;
    private MegamanStats Stats => mm.Stats;

    private float slideTime;
    private float slideTimeLength;
    private bool slideButtonRelease = true;
    private Vector2 defaultBoxOffset;
    private Vector2 defaultBoxSize;

    private void Awake() => mm = GetComponent<Megaman>();

    public void CacheDefaultCollider()
    {
        defaultBoxOffset = mm.BoxCollider.offset;
        defaultBoxSize = mm.BoxCollider.size;
    }

    /// <summary>Stops the slide (e.g. when hit). The collider goes back to normal on the next update.</summary>
    public void Cancel() => IsSliding = false;

    public bool CanSlideWithDownJump()
    {
        return GlobalVariables.canSlideWithDownJump && mm.MoveInput.y < 0 && mm.JumpPressed;
    }

    public void PerformSlide()
    {
        if (mm.state == Megaman.PlayerStates.Climb) return;

        mm.BoxCollider.offset = IsSliding ? Stats.slideBoxOffset : defaultBoxOffset;
        mm.BoxCollider.size = IsSliding ? Stats.slideBoxSize : defaultBoxSize;

        if (!IsSliding)
        {
            StartSliding();
            return;
        }

        bool isTouchingTop = mm.IsColAbove();
        bool isTouchingFront = mm.IsFrontCollision();
        slideTimeLength = Time.time - slideTime;

        if (ShouldExitSlide(isTouchingTop, isTouchingFront) || slideTimeLength >= Stats.slideDuration && !isTouchingTop || !mm.IsGrounded())
        {
            mm.Velocity = new Vector2(0, mm.Velocity.y);
            IsSliding = false;
            slideButtonRelease = true;
        }
        else
        {
            mm.Velocity = new Vector2(Stats.slideSpeed * (mm.FacingRight ? 1f : -1f), mm.Velocity.y);
        }
    }

    private void StartSliding()
    {
        bool pressed = (mm.SlidePressed && slideButtonRelease) || CanSlideWithDownJump();
        if (!pressed || !mm.IsGrounded() || IsSliding || mm.IsFrontCollision()) return;

        IsSliding = true;
        slideTime = Time.time;
        slideTimeLength = 0;

        if (slideDustPrefab != null && slideDustPos != null)
        {
            GameObject dust = Instantiate(slideDustPrefab);
            dust.name = slideDustPrefab.name;
            dust.transform.position = slideDustPos.position;
            if (!mm.FacingRight) dust.transform.Rotate(0f, 180f, 0f);
        }

        mm.SlidePressed = false;
        slideButtonRelease = false;
    }

    private bool ShouldExitSlide(bool isTouchingTop, bool isTouchingFront)
    {
        // Turning around: under a ceiling it just flips, otherwise it ends the slide
        if ((mm.MoveInput.x < 0 && mm.FacingRight) || (mm.MoveInput.x > 0 && !mm.FacingRight))
        {
            if (isTouchingTop) mm.Flip();
            else return true;
        }

        // Jump with room above
        if (mm.JumpPressed && !isTouchingTop) return true;

        // Wall ahead after a moment
        if (isTouchingFront && !isTouchingTop && slideTimeLength >= 0.1f) return true;

        return false;
    }
}
