using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mega Man's core: shared references, input, sensors and the public API other scripts use. Input comes from an
/// IPlayerInputSource (LocalPlayerInput: his PlayerInput) as PlayerCommands, read once per frame.
/// The behaviour lives in modules on the same object, which this runs every frame in a fixed order:
///   MegamanMotor (move, jump, gravity) · MegamanSlide · MegamanLadder · MegamanWeapons
///   MegamanHealth (damage, blink, death, refills) · MegamanWater (bubbles) · MegamanAnimation
/// Every tuning value is in the MegamanStats asset. The sprite and Animator are on the "Visual" child (MegamanVisual),
/// so the picture can be moved without the collider.
/// Movement and collision are his own (MegamanMover, every FixedUpdate after gravity): the modules set Velocity.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
[RequireComponent(typeof(MegamanMotor), typeof(MegamanHealth))]
[RequireComponent(typeof(MegamanWeapons), typeof(MegamanSlide), typeof(MegamanLadder))]
[RequireComponent(typeof(MegamanWater), typeof(MegamanAnimation))]
public class Megaman : MonoBehaviour
{
    [Tooltip("Speed, jump, slide, checks... Every tuning value of Mega Man.")]
    [SerializeField] private MegamanStats stats;

    [Header("Functional Options")]
    [SerializeField] private bool canMove = true;
    [SerializeField] private bool canJump = true;
    [SerializeField] private bool canShoot = true;
    [SerializeField] private bool canSlide = true;
    [SerializeField] private bool canClimb = true;

    public enum PlayerStates { Normal, Still, Frozen, Climb, Hurt, Fallen, Paused, Riding }
    public PlayerStates state = PlayerStates.Normal;

    #region References
    public MegamanStats Stats => stats;
    public Rigidbody2D Body { get; private set; }
    public BoxCollider2D BoxCollider { get; private set; }
    public SpriteRenderer SpriteRenderer { get; private set; }
    public Animator Animator { get; private set; }

    public MegamanMover Mover { get; private set; }
    public MegamanMotor Motor { get; private set; }
    public MegamanHealth Health { get; private set; }
    public MegamanWeapons Weapons { get; private set; }
    public MegamanSlide Slide { get; private set; }
    public MegamanLadder Ladder { get; private set; }
    public MegamanWater Water { get; private set; }
    public MegamanAnimation Animation { get; private set; }
    #endregion

    #region Shared state
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; set; }          // cleared by slide / ladder so the press isn't used twice
    public bool ShootPressed { get; private set; }
    public bool SlidePressed { get; set; }
    public float LastJumpTime { get; set; }        // last jump press or jump (jump buffer)
    public float LastGroundedTime { get; private set; }

    public bool FacingRight { get; private set; } = true;

    /// <summary>Units per second (MegamanMover moves him by it).</summary>
    public Vector2 Velocity
    {
        get => Mover.Velocity;
        set => Mover.Velocity = value;
    }

    /// <summary>Standing / walking on a slope (as of the last physics step).</summary>
    public bool OnSlope => Mover.OnSlope;
    public Vector2 SlopeNormal => Mover.GroundNormal;
    public bool IsFacingRight => FacingRight;      // referenced by WeaponBase
    public bool IsInputFrozen { get; private set; }
    private bool playerFrozen;
    #endregion

    private void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        BoxCollider = GetComponent<BoxCollider2D>();
        SpriteRenderer = GetComponentInChildren<SpriteRenderer>();   // the Visual child
        Animator = GetComponentInChildren<Animator>();

        // Not on older copies of the prefab: added here
        Mover = TryGetComponent(out MegamanMover mover) ? mover : gameObject.AddComponent<MegamanMover>();
        InputSource = TryGetComponent(out IPlayerInputSource source) ? source : gameObject.AddComponent<LocalPlayerInput>();
        Motor = GetComponent<MegamanMotor>();
        Health = GetComponent<MegamanHealth>();
        Weapons = GetComponent<MegamanWeapons>();
        Slide = GetComponent<MegamanSlide>();
        Ladder = GetComponent<MegamanLadder>();
        Water = GetComponent<MegamanWater>();
        Animation = GetComponent<MegamanAnimation>();

        if (stats == null) Debug.LogError("Megaman: no MegamanStats asset assigned.", this);
    }

    private void OnEnable() => Players.Add(this);
    private void OnDisable() => Players.Remove(this);

    private void Start()
    {
        FacingRight = true;      // always start facing right
        Health.ResetHealth();
        Slide.CacheDefaultCollider();
        Weapons.Begin();
    }

    private void Update()
    {
        ReadInput();
        if (!IsInputFrozen)
        {
            if (canMove) Motor.Move();
            if (canJump) Motor.CheckJump();
            if (canShoot) Weapons.UseWeapon();
            if (canSlide) Slide.PerformSlide();
            if (canClimb) Ladder.HandleClimbing();
        }

        Weapons.UpdateSwitchIcon();
        Animation.UpdateAnimations();
        Water.HandleBubbles();
        Health.UpdateLowHealthSmoke();
    }

    private void FixedUpdate()
    {
        Motor.ApplyGravity();
        Mover.Step(Time.fixedDeltaTime);
    }

    public void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // Turns towards the horizontal input
    public void FaceInput()
    {
        if (MoveInput.x > 0 && !FacingRight) Flip();
        else if (MoveInput.x < 0 && FacingRight) Flip();
    }

    #region Sensors (ground, above, front, water)
    public bool IsGrounded()
    {
        bool grounded = Mover.Grounded;
        if (grounded) LastGroundedTime = Time.time;
        return grounded;
    }

    /// <summary>On solid ground, a one-way platform or a slope (as of the last physics step).</summary>
    public bool IsOnGround() => Mover.Grounded;

    // Used when sliding: something low overhead
    public bool IsColAbove()
    {
        Vector2 position = (Vector2)transform.position + stats.topCheckOffset;
        return Mover.OverlapsSolid(position, stats.topCheckSize);
    }

    /// <summary>
    /// A wall right in front of him (as far ahead as the front check's far side). A walkable slope ahead isn't one,
    /// whatever it's made of.
    /// </summary>
    public bool IsFrontCollision()
    {
        float reach = stats.frontCheckOffset.x + stats.frontCheckSize.x / 2 - BoxCollider.bounds.extents.x;
        return Mover.WallAhead(FacingRight ? 1f : -1f, Mathf.Max(0.05f, reach));
    }

    public bool IsInWater()
    {
        return Physics2D.OverlapBox((Vector2)transform.position + BoxCollider.offset, BoxCollider.size, 0f, stats.waterLayer) != null;
    }
    #endregion

    #region Freeze
    public void FreezeInput(bool freeze)
    {
        IsInputFrozen = freeze;
    }

    // He stops where he is (keeping his velocity for when he's unfrozen)
    public void FreezePlayer(bool shouldFreeze)
    {
        if (shouldFreeze == playerFrozen) return;
        playerFrozen = shouldFreeze;
        Mover.Frozen = shouldFreeze;
        Animator.speed = shouldFreeze ? 0 : 1;
    }
    #endregion

    #region Public API (enemies, items, menus, GameManager)
    public int CurrentHealth => Health.Current;
    public int MaxHealth => stats.maxHealth;
    public bool IsMoving() => Motor.IsMoving;

    public void TakeDamage(int damage) => Health.TakeDamage(damage);
    public void HitSide(bool rightSide) => Health.HitSide(rightSide);
    public void Invincible(bool invincibility) => Health.SetInvincible(invincibility);

    public void RestoreHealth(int amount, AudioEvent itemSound, bool freezeEverything = true) => Health.Restore(amount, itemSound, freezeEverything);
    public void RestoreFullHealth(AudioEvent itemSound) => Health.RestoreFull(itemSound);
    public void RestoreWeaponEnergy(int amount, AudioEvent itemSound, bool freezeEverything = true) => Weapons.RestoreEnergy(amount, itemSound, freezeEverything);
    public void RestoreFullWeaponEnergy(AudioEvent itemSound) => Weapons.RestoreFullEnergy(itemSound);

    public IReadOnlyList<WeaponData> GetWeapons() => Weapons.All;
    public WeaponData GetCurrentWeapon() => Weapons.Current;
    public void SwitchWeapon(WeaponData weapon) => Weapons.SwitchWeapon(weapon);
    #endregion

    #region Input
    /// <summary>Where his commands come from (LocalPlayerInput; a network source later). 0 = player 1.</summary>
    public IPlayerInputSource InputSource { get; private set; }
    public int PlayerIndex => InputSource != null ? InputSource.PlayerIndex : 0;
    private PlayerCommands lastCommands;

    // Turns this frame's commands into the shared input state the modules read. Presses and releases come from
    // comparing with the last commands, so the source only has to say what's held.
    private void ReadInput()
    {
        PlayerCommands commands = InputSource != null ? InputSource.ReadCommands() : default;
        PlayerCommands last = lastCommands;
        lastCommands = commands;

        MoveInput = commands.Move;

        if (commands.Pressed(PlayerButtons.Jump, last))
        {
            JumpPressed = true;
            LastJumpTime = Time.time;
        }
        else if (commands.Released(PlayerButtons.Jump, last)) JumpPressed = false;

        ShootPressed = commands.IsHeld(PlayerButtons.Shoot);

        if (commands.Pressed(PlayerButtons.Slide, last)) SlidePressed = true;
        else if (commands.Released(PlayerButtons.Slide, last)) SlidePressed = false;

        if (!IsInputFrozen)
        {
            if (commands.Pressed(PlayerButtons.NextWeapon, last)) Weapons.SwitchToNext();
            if (commands.Pressed(PlayerButtons.PreviousWeapon, last)) Weapons.SwitchToPrevious();
        }
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmos()
    {
        if (stats == null) return;
        bool facingRight = transform.localScale.x >= 0;

        // Ground rays
        Vector2 position = (Vector2)transform.position + stats.groundCheckOffset;
        float halfWidth = stats.groundCheckWidth / 2;
        Vector2 down = Vector2.down * stats.groundCheckDistance;
        Gizmos.color = Application.isPlaying && IsGrounded() ? Color.green : Color.red;
        Gizmos.DrawLine(position, position + down);
        Gizmos.DrawLine(position - new Vector2(halfWidth, 0), position - new Vector2(halfWidth, 0) + down);
        Gizmos.DrawLine(position + new Vector2(halfWidth, 0), position + new Vector2(halfWidth, 0) + down);

        // Shoot ray
        Vector2 shootDirection = facingRight ? Vector2.right : Vector2.left;
        Vector2 shootStart = (Vector2)transform.position + new Vector2(facingRight ? stats.bulletShootOffset.x : -stats.bulletShootOffset.x, stats.bulletShootOffset.y);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(shootStart, shootStart + shootDirection * stats.shootRayLength);

        // Top and front checks
        Gizmos.color = Application.isPlaying && IsColAbove() ? Color.green : Color.red;
        Gizmos.DrawWireCube((Vector2)transform.position + stats.topCheckOffset, stats.topCheckSize);
        Vector2 front = facingRight ? stats.frontCheckOffset : new Vector2(-stats.frontCheckOffset.x, stats.frontCheckOffset.y);
        Gizmos.color = Application.isPlaying && IsFrontCollision() ? Color.green : Color.red;
        Gizmos.DrawWireCube((Vector2)transform.position + front, stats.frontCheckSize);

        // Slide collider
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube((Vector2)transform.position + stats.slideBoxOffset, stats.slideBoxSize);

        // Ladder ends
        var ladderModule = GetComponent<MegamanLadder>();
        var ladder = ladderModule != null ? ladderModule.ladder : null;
        if (ladder != null)
        {
            Vector3 top = new(ladder.transform.position.x, ladder.posTopHandlerY, transform.position.z);
            Vector3 bottom = new(ladder.transform.position.x, ladder.posBottomHandlerY, transform.position.z);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(top, 0.2f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(bottom, 0.2f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(top, bottom);
        }
    }
    #endregion
}
