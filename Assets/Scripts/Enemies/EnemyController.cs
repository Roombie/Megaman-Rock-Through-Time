using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class EnemyController : MonoBehaviour, IDamageable, IDamagePreview
{
    bool isInvincible;
    float animatorSpeed;
    Vector2 freezeVelocity;
    RigidbodyConstraints2D rb2dConstraints;

    public bool freezeEnemy;
    public int currentHealth;
    public int maxHealth = 1;
    public int contactDamage = 1;
    public int explosionDamage = 0;
    public float explosionDelayBeforeDestroy = 2f;
    public AudioEvent damageClip;

    [Header("Weaknesses")]
    [Tooltip("Damage specific weapons do to this enemy (0 = immune). Weapons not listed do their normal damage.")]
    public WeaponDamage[] weaknesses;
    [Tooltip("Played when a weapon does no damage (immune).")]
    public AudioEvent immuneClip;

    private float weaponFreezeRemaining;

    GameObject explodeEffect;
    [SerializeField] GameObject explosionEffectPrefab;

    SpriteRenderer spriteRenderer;
    BoxCollider2D boxCollider2D;
    Rigidbody2D rb;
    Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider2D = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // start at full health
        currentHealth = maxHealth;
    }

    public void Invincible(bool invincibility)
    {
        isInvincible = invincibility;
    }

    public void FreezeEnemy(bool freeze)
    {
        // Freezing twice would save the frozen values as the originals and keep the enemy frozen forever
        if (freeze == freezeEnemy) return;

        if (freeze)
        {
            freezeEnemy = true;
            animatorSpeed = animator.speed;
            rb2dConstraints = rb.constraints;
            freezeVelocity = rb.linearVelocity;
            animator.speed = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            freezeEnemy = false;
            animator.speed = animatorSpeed;
            rb.constraints = rb2dConstraints;
            rb.linearVelocity = freezeVelocity;
        }
    }

    /// <summary>Hit by a weapon: uses the weakness table, then the normal damage.</summary>
    public bool TakeDamage(in DamageInfo info)
    {
        if (isInvincible) return false;

        int amount = WeaknessTable.Resolve(weaknesses, info);

        if (amount <= 0)
        {
            Audio.Play(immuneClip);   // "dink"
            return false;
        }

        TakeDamage(amount);
        return true;
    }

    public int PreviewDamage(in DamageInfo info) => WeaknessTable.Resolve(weaknesses, info);

    /// <summary>Frozen by a weapon (Ice Slasher...) for some seconds.</summary>
    public void FreezeFor(float seconds)
    {
        if (seconds <= 0f) return;
        if (!freezeEnemy) FreezeEnemy(true);
        weaponFreezeRemaining = Mathf.Max(weaponFreezeRemaining, seconds);
    }

    private void Update()
    {
        if (weaponFreezeRemaining <= 0f) return;
        weaponFreezeRemaining -= Time.deltaTime;
        if (weaponFreezeRemaining <= 0f) FreezeEnemy(false);
    }

    public void TakeDamage(int damage)
    {
        // take damage if not invincible
        if (!isInvincible)
        {
            Audio.Play(damageClip);
            // take damage amount from health and call defeat if no health
            currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);
            if (currentHealth <= 0)
            {
                Defeat();
            }
        }
    }

    void StartDefeatAnimation()
    {
        explodeEffect = Instantiate(explosionEffectPrefab);
        explodeEffect.name = explosionEffectPrefab.name;
        explodeEffect.transform.position = spriteRenderer.bounds.center;
        explodeEffect.GetComponent<Explosion>().SetDamageValue(this.explosionDamage);
        Destroy(explodeEffect, explosionDelayBeforeDestroy);
    }

    void StopDefeatAnimation()
    {
        Destroy(gameObject);
    }

    void Defeat()
    {
        StartDefeatAnimation();
        // remove this enemy *poof*
        Destroy(gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Megaman player = other.gameObject.GetComponent<Megaman>();
            if (player != null)
            {
                // colliding with player inflicts damage and takes contact damage away from health
                player.HitSide(transform.position.x > player.transform.position.x);
                player.TakeDamage(this.contactDamage);
            }
        }
    }
}
