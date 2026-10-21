using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ChargeColorFrame
{
    public Color primary;
    public Color secondary;
    public Color outline;
}

[System.Serializable]
public struct ChargeColorAnimation
{
    [Tooltip("Seconds per frame.")]
    public float frameDuration;
    public ChargeColorFrame[] frames;
}

[System.Serializable]
public struct ChargeLevel
{
    [Tooltip("Seconds the button must be held to reach this level (level 0 = the normal shot).")]
    public float timeRequired;
    public int damage;
    public GameObject projectilePrefab;
    [Tooltip("Sound of this level's shot.")]
    public AudioEvent weaponClip;
    public float bulletSpeed;
    [Tooltip("Mega Man's armour flashing while this level is charged.")]
    public ChargeColorAnimation colorAnimation;
}

/// <summary>
/// Mega Buster style: press fires the level 0 shot; holding charges through the levels (armour flashes,
/// charge sound) and releasing fires the charged shot.
/// </summary>
public class ChargeShotWeapon : WeaponBase
{
    [Tooltip("Level 0 is the normal shot; the others are reached by holding the button.")]
    public List<ChargeLevel> chargeLevels = new();
    [Tooltip("Played once when the first charge level is reached.")]
    public AudioEvent chargingSound;
    [Tooltip("Lose the charge when Mega Man gets hit.")]
    public bool interruptOnHurt;
    [Tooltip("Seconds Mega Man keeps the shooting pose after a shot.")]
    [Min(0f)] public float poseDuration = 0.25f;

    private bool ready;              // button released since the last shot: the next press fires
    private float chargeTime;
    private int level;
    private bool chargeSoundPlayed;
    private AudioHandle chargeSound;
    private float poseUntil;

    public override bool IsShooting => Time.time < poseUntil;
    public int CurrentLevel => level;

    public override void Tick(WeaponInput input)
    {
        bool held = input.held;

        // Charging while held after a shot
        if (held && !ready && User.AllowCharge && chargeLevels.Count > 1)
        {
            chargeTime += Time.deltaTime;
            level = 0;
            for (int i = 0; i < chargeLevels.Count; i++)
                if (chargeTime >= chargeLevels[i].timeRequired) level = i;

            if (level > 0 && !chargeSoundPlayed)
            {
                chargeSound = Audio.Play(chargingSound);
                chargeSoundPlayed = true;
            }

            if (level > 0 && ShowChargeColors(chargeLevels[level].colorAnimation)) return;
        }

        // Press: the normal shot (waits while Mega Man can't fire, e.g. sliding)
        if (held && ready && User.CanFire)
        {
            if (HasRoomOnScreen && CanAfford(Data.energyCost))
            {
                ready = false;
                Shoot(level);
                Spend(Data.energyCost);
            }
        }

        // Release: the charged shot
        if (!held && ready)
        {
            if (level > 0)
            {
                if (!User.CanFire) return;   // fired as soon as it can (e.g. when the slide ends)
                StopChargeSound();
                Shoot(level);
            }
            ResetCharge();
        }

        if (!held && !ready) ready = true;
    }

    private void Shoot(int shotLevel)
    {
        if (chargeLevels.Count == 0) return;
        var data = chargeLevels[Mathf.Clamp(shotLevel, 0, chargeLevels.Count - 1)];
        Fire(data.projectilePrefab, data.damage, data.bulletSpeed, shotLevel);
        PlaySound(data.weaponClip != null ? data.weaponClip : Data.weaponClip);
        poseUntil = Time.time + poseDuration;
        level = 0;
        chargeTime = 0f;
        User.ResetArmorColors();
    }

    private bool ShowChargeColors(ChargeColorAnimation animation)
    {
        if (animation.frames == null || animation.frames.Length == 0 || animation.frameDuration <= 0f) return false;
        float elapsed = Time.time % (animation.frames.Length * animation.frameDuration);
        var frame = animation.frames[Mathf.Min(animation.frames.Length - 1, Mathf.FloorToInt(elapsed / animation.frameDuration))];
        User.SetArmorColors(frame.primary, frame.secondary, frame.outline);
        return true;
    }

    private void ResetCharge()
    {
        chargeTime = 0f;
        level = 0;
        chargeSoundPlayed = false;
    }

    private void StopChargeSound()
    {
        // Pooled sources are reused: only stop the handle if it's still the charge sound
        if (chargeSound.IsPlayingEvent(chargingSound)) chargeSound.Stop();
        chargeSound = AudioHandle.Invalid;
    }

    public override void Cancel()
    {
        bool wasCharged = level > 0 || chargeTime > 0f;
        StopChargeSound();
        ResetCharge();
        poseUntil = 0f;
        if (wasCharged && User != null) User.ResetArmorColors();
    }

    public override void OnUserHurt()
    {
        if (interruptOnHurt) Cancel();
    }

    private void OnDisable() => StopChargeSound();
}
