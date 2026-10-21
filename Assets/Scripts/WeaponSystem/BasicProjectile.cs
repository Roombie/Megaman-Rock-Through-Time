using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A projectile built in the Inspector: pick a movement (straight, arc, sine, homing, boomerang), the hit
/// effects (damage, splash, freeze...) and how it bounces. Covers most weapons without code. Needs a
/// Rigidbody2D (kinematic is fine) and a trigger collider. See its path in the Scene view's Weapon Preview.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BasicProjectile : Projectile, IProjectileBody
{
    [SerializeReference, SubclassSelector]
    public ProjectileMovement movement = new StraightMovement();

    [SerializeReference, SubclassSelector]
    public List<HitEffect> hitEffects = new() { new DamageEffect() };

    [Tooltip("Keeps flying after hitting (otherwise it's destroyed on the first hit).")]
    public bool pierce;
    [Tooltip("Seconds before it disappears on its own (0 = never).")]
    [Min(0f)] public float lifetime = 5f;
    [Tooltip("Destroyed when it leaves the screen.")]
    public bool destroyOffScreen = true;
    [Tooltip("Destroyed when it touches these layers (walls...). Nothing = passes through.")]
    public LayerMask destroyOnLayers;

    [Header("Bouncing")]
    [Tooltip("How many times it bounces off Bounce Layers. After the last one, the next surface destroys it.")]
    [Min(0)] public int bounces;
    [Tooltip("Surfaces it bounces off (usually Ground).")]
    public LayerMask bounceLayers;
    public AudioEvent bounceSound;

    private float age;
    private int bouncesLeft;
    private float radius;
    private bool ended;
    private readonly HashSet<IDamageable> alreadyHit = new();

    public int BouncesLeft => bouncesLeft;
    public float Speed => Shot.speed;
    public Vector2 Position => rb != null ? rb.position : (Vector2)transform.position;

    public Vector2 Velocity
    {
        get => rb != null ? rb.linearVelocity : Vector2.zero;
        set { if (rb != null) rb.linearVelocity = value; }
    }

    protected override void Awake()
    {
        base.Awake();
        radius = ProjectileCollision.RadiusOf(gameObject);
    }

    public override void Initialize(ShotInfo shot)
    {
        base.Initialize(shot);
        age = 0f;
        bouncesLeft = bounces;
        ended = false;
        alreadyHit.Clear();
        movement?.Begin(this);
    }

    private void FixedUpdate()
    {
        if (ended || movement == null) return;
        float dt = Time.fixedDeltaTime;
        movement.Move(this, dt);
        if (ended) return;

        // Bounce surfaces ahead this step
        if (bounces > 0 && ProjectileCollision.Cast(Position, radius, Velocity * dt, bounceLayers, out RaycastHit2D hit))
        {
            if (bouncesLeft <= 0)
            {
                End();
                return;
            }
            rb.position = hit.centroid + hit.normal * 0.01f;
            movement.OnBounce(this, hit.normal);
            bouncesLeft--;
            Audio.Play(bounceSound);
        }
    }

    private void Update()
    {
        if (lifetime <= 0f) return;
        age += Time.deltaTime;
        if (age >= lifetime) End();
    }

    public void End()
    {
        if (ended) return;
        ended = true;
        Destroy(gameObject);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // Bounce surfaces are handled in FixedUpdate
        int layerBit = 1 << other.gameObject.layer;
        if ((destroyOnLayers.value & layerBit) != 0 && (bounces == 0 || (bounceLayers.value & layerBit) == 0))
        {
            End();
            return;
        }
        base.OnTriggerEnter2D(other);
    }

    protected override void OnHit(Collider2D other, IDamageable target)
    {
        if (!alreadyHit.Add(target)) return;   // a piercing shot hits each target once

        foreach (var effect in hitEffects)
            effect?.Apply(this, other, target);

        if (!pierce) End();
    }

    protected override void OnBecameInvisible()
    {
        if (destroyOffScreen) End();
    }
}
