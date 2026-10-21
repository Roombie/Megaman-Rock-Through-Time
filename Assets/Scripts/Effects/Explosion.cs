using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    int damage = 0;
    bool freezeExplosion;
    float animatorSpeed;

    float destroyTimer;
    float destroyDelay;

    Animator animator;
    SpriteRenderer spriteRenderer;

    void Awake()
    {
        // get components
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        SetDestroyDelay(destroyDelay);
    }

    // NES: an effect that scrolls off screen is gone (with the room camera; scenes without one keep the timer only),
    // and a screen transition clears them all
    private const float OffScreenMargin = 1f;   // tiles past the screen edge
    private static readonly List<Explosion> all = new();

    private void OnEnable()
    {
        all.Add(this);
        if (all.Count == 1) RoomTransition.Started += ClearAll;
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (all.Count == 0) RoomTransition.Started -= ClearAll;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private static void ClearAll(CameraRoom from, CameraRoom to, RoomTransition.Direction direction)
    {
        foreach (Explosion explosion in all.ToArray()) Destroy(explosion.gameObject);
    }

    private void Update()
    {
        if (RoomCamera.All.Count > 0 && !RoomCamera.Sees(transform.position, OffScreenMargin * CameraRoom.Tile))
        {
            Destroy(gameObject);
            return;
        }

        if (freezeExplosion) return;

        if (destroyDelay > 0)
        {
            destroyTimer -= Time.deltaTime;
            if (destroyTimer <= 0)
            {
                Destroy(gameObject);
            }
        }
    }

    public void SetDamageValue (int damage)
    {
        this.damage = damage;
    }

    public void SetDestroyDelay(float delay)
    {
        this.destroyDelay = delay;
        this.destroyTimer = delay;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (this.damage > 0)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                Megaman player = other.gameObject.GetComponent<Megaman>();
                player.HitSide(transform.position.x > player.transform.position.x);
                player.TakeDamage(this.damage);
            }
        }
    }

    public void FreezeExplosion(bool freeze)
    {
        // freeze/unfreeze the explosions on screen
        // NOTE: this will be called from the GameManager but could be used in other scripts
        if (freeze == freezeExplosion) return;   // freezing twice would save speed 0 as the original

        if (freeze)
        {
            freezeExplosion = true;
            animatorSpeed = animator.speed;
            animator.speed = 0;
        }
        else
        {
            freezeExplosion = false;
            animator.speed = animatorSpeed;
        }
    }
}
