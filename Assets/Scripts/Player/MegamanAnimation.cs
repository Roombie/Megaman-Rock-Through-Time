using UnityEngine;

/// <summary>Feeds Mega Man's state to the Animator (parameter IDs hashed once).</summary>
[RequireComponent(typeof(Megaman))]
public class MegamanAnimation : MonoBehaviour
{
    private static readonly int IsTakingDamage = Animator.StringToHash("isTakingDamage");
    private static readonly int Hit = Animator.StringToHash("hit");
    private static readonly int IsStepping = Animator.StringToHash("isStepping");
    private static readonly int UseStep = Animator.StringToHash("useStep");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int Horizontal = Animator.StringToHash("horizontal");
    private static readonly int IsShooting = Animator.StringToHash("isShooting");
    private static readonly int IsSliding = Animator.StringToHash("isSliding");
    private static readonly int IsClimbing = Animator.StringToHash("isClimbing");
    private static readonly int IsClimbingToTop = Animator.StringToHash("isClimbingToTop");

    private Megaman mm;

    private void Awake() => mm = GetComponent<Megaman>();

    public void UpdateAnimations()
    {
        var animator = mm.Animator;
        bool takingDamage = mm.Health.IsTakingDamage;

        animator.SetBool(IsTakingDamage, takingDamage);
        if (takingDamage)
        {
            animator.SetTrigger(Hit);
            return;
        }

        bool useStep = mm.Stats.useStepDelay;
        animator.SetBool(IsStepping, useStep && mm.Motor.IsMoving && !mm.Motor.HasStepped);
        animator.SetBool(UseStep, useStep);
        animator.SetBool(IsGrounded, mm.IsGrounded());
        animator.SetFloat(Horizontal, Mathf.Abs(mm.MoveInput.x));
        animator.SetBool(IsShooting, mm.Weapons.IsShooting);
        animator.SetBool(IsSliding, mm.Slide.IsSliding);
        animator.SetBool(IsClimbing, mm.Ladder.IsClimbing);
        animator.SetBool(IsClimbingToTop, mm.Ladder.IsClimbingToTop);
    }
}
