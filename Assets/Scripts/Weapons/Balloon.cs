using System.Collections;
using UnityEngine;

public class Balloon : MonoBehaviour
{
    public float riseSpeed = 1.0f;
    public float sinkAmount = 0.1f;
    public float lifeTime = 5.0f;
    public float flashDuration = 1.5f;
    public GameObject explosionPrefab;
    public float flashDelay = 0.0833f;
    public float explosionLifetime = 1.0f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private float timer;
    private bool isFlashing = false;
    private bool isSteppedOn = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        timer = lifeTime;
    }

    void Update()
    {
        animator.SetBool("IsPlayerOnBalloon", isSteppedOn);
        timer -= Time.deltaTime;

        if (timer <= flashDuration && !isFlashing)
        {
            StartCoroutine(FlashBeforeDestroy());
        }

        if (timer <= 0)
        {
            Explode();
        }

        rb.linearVelocity = new Vector2(0, riseSpeed);
    }

    private IEnumerator FlashBeforeDestroy()
    {
        isFlashing = true;
        for (int i = 0; i < 10; i++)
        {
            spriteRenderer.enabled = false;
            yield return new WaitForSeconds(flashDelay);
            spriteRenderer.enabled = true;
            yield return new WaitForSeconds(flashDelay);
        }
    }

    // Mega Man stands on it through his own collision (MegamanMover), not physics contacts
    private void FixedUpdate()
    {
        isSteppedOn = false;
        foreach (Megaman player in Players.All)
            if (player.Mover.GroundCollider != null && player.Mover.GroundCollider.attachedRigidbody == rb)
                isSteppedOn = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Explode();
        }
    }

    void Explode()
    {
        Instantiate(explosionPrefab, transform.position, Quaternion.identity)
            .GetComponent<Explosion>()?.SetDestroyDelay(explosionLifetime);
        Destroy(gameObject);
    }
}
