using System.Collections;
using UnityEngine;

/// <summary>Health, damage (knockback + NES blink), death, health refills and the low-health smoke.</summary>
[RequireComponent(typeof(Megaman))]
public class MegamanHealth : MonoBehaviour
{
    [Tooltip("Spawned where Mega Man was when he dies.")]
    [SerializeField] private GameObject deathExplosion;
    [SerializeField] private AudioEvent damageSound;
    [Tooltip("Smoke shown while health is low.")]
    [SerializeField] private ParticleSystem lowHealthSmoke;

    public int Current { get; private set; }
    public bool IsTakingDamage { get; private set; }
    public bool IsInvincible { get; private set; }
    public bool IsDead { get; private set; }

    private Megaman mm;
    private MegamanStats Stats => mm.Stats;

    private bool hitSideRight;
    private Coroutine flashRoutine;
    private bool isBlinking;
    private bool blinkHidden;

    private void Awake() => mm = GetComponent<Megaman>();

    public void ResetHealth()
    {
        Current = Stats.maxHealth;
        IsDead = false;
    }

    #region Damage
    /// <summary>Which side the hit came from (sets the knockback direction).</summary>
    public void HitSide(bool rightSide) => hitSideRight = rightSide;

    public void SetInvincible(bool invincible) => IsInvincible = invincible;

    public void TakeDamage(int damage)
    {
        if (IsInvincible || IsDead || damage <= 0) return;

        Current = Mathf.Clamp(Current - damage, 0, Stats.maxHealth);
        float health = Current / (float)Stats.maxHealth;
        UIHealthBar.Instance.SetValue(health);
        mm.Weapons.RefreshEnergyBar();   // the Mega Buster's bar is health

        if (Current <= 0) Defeat();
        else StartDamageAnimation();
    }

    private void StartDamageAnimation()
    {
        // The Hit animation plays while IsTakingDamage; its last frame calls StopDamageAnimation
        if (IsTakingDamage) return;

        mm.Slide.Cancel();
        mm.Weapons.OnDamaged();
        Audio.Play(damageSound);
        IsTakingDamage = true;
        IsInvincible = true;
        mm.FreezeInput(true);
        mm.Ladder.EndClimbing();

        Vector2 push = Stats.hitPushForce;
        if (hitSideRight) push.x = -push.x;
        mm.Velocity = push;
    }

    // Animation Event at the end of the Hit animation (passed on by MegamanVisual, where the Animator is)
    public void StopDamageAnimation()
    {
        IsTakingDamage = false;
        mm.FreezeInput(false);
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashAfterDamage());
    }

    private IEnumerator FlashAfterDamage()
    {
        isBlinking = true;
        var wait = new WaitForSeconds(Stats.blinkInterval);
        for (int i = 0; i < Stats.blinkCount; i++)
        {
            // Classic NES blink: hide/show the sprite. Applied in LateUpdate, because the Animator
            // (Megaman_Hit animates SpriteRenderer.enabled, Write Defaults on) resets it every frame.
            blinkHidden = true;
            yield return wait;
            blinkHidden = false;
            yield return wait;
        }
        StopBlink();
        flashRoutine = null;
        IsInvincible = false;
    }

    private void StopBlink()
    {
        isBlinking = false;
        blinkHidden = false;
        mm.SpriteRenderer.enabled = true;
    }

    private void LateUpdate()
    {
        // Runs after the Animator has written its values for this frame
        if (isBlinking) mm.SpriteRenderer.enabled = !blinkHidden;
    }
    #endregion

    #region Death
    private void Defeat()
    {
        IsDead = true;
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Never die halfway through a blink with the sprite hidden
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        StopBlink();
        mm.Weapons.CancelWeaponAction();   // charge sound, pending shot, magnet beam

        mm.FreezeInput(true);
        mm.FreezePlayer(true);
        yield return new WaitForSeconds(Stats.delayBeforeDeath);

        if (deathExplosion != null) Instantiate(deathExplosion, transform.position, Quaternion.identity);
        Destroy(gameObject);
        GameManager.Instance.LoseLife();
    }
    #endregion

    #region Refills
    public void RestoreFull(AudioEvent itemSound)
    {
        if (Current < Stats.maxHealth) StartCoroutine(IncrementHealth(Stats.maxHealth - Current, itemSound, true));
    }

    public void Restore(int amount, AudioEvent itemSound, bool freezeEverything = true)
    {
        if (Current != Stats.maxHealth) StartCoroutine(IncrementHealth(amount, itemSound, freezeEverything));
    }

    private IEnumerator IncrementHealth(int amount, AudioEvent itemSound, bool freezeEverything)
    {
        int healthToRestore = Mathf.Clamp(amount, 0, Stats.maxHealth - Current);
        if (freezeEverything) GameManager.Instance.FreezeEverything(true);

        while (healthToRestore > 0)
        {
            if (!refillSound.IsPlayingEvent(itemSound))
            {
                refillSound = Audio.Play(itemSound);
                refillEvent = itemSound;
            }
            Current++;
            UIHealthBar.Instance.SetValue(Current / (float)Stats.maxHealth);
            healthToRestore--;

            // Real time: tanks used from the paused weapons menu refill while timeScale is 0
            yield return new WaitForSecondsRealtime(Stats.refillInterval);
        }

        StopRefillSound();
        GameManager.Instance.FreezeEverything(false);
    }

    // The fill sound loops while refilling: always stop it, also if Mega Man goes away mid-refill
    private AudioHandle refillSound;
    private AudioEvent refillEvent;

    private void StopRefillSound()
    {
        // pooled sources are reused: only stop it if it's still our fill sound
        if (refillSound.IsPlayingEvent(refillEvent)) refillSound.Stop();
        refillSound = AudioHandle.Invalid;
    }

    private void OnDisable() => StopRefillSound();
    #endregion

    public void UpdateLowHealthSmoke()
    {
        if (lowHealthSmoke == null) return;
        bool low = Current <= Stats.lowHealthSmoke && Current > 0;
        if (low && !lowHealthSmoke.isPlaying) lowHealthSmoke.Play();
        else if (!low && lowHealthSmoke.isPlaying) lowHealthSmoke.Stop();
    }
}
