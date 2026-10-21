using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mega Man's weapons: owns one instance of each weapon's prefab (its fire mode), forwards the shoot button
/// to the equipped one, and handles switching (icon, armour colours, energy bar) and energy refills.
/// The weapons themselves decide how they fire (see WeaponBase).
/// </summary>
[RequireComponent(typeof(Megaman))]
public class MegamanWeapons : MonoBehaviour, IWeaponUser
{
    [Tooltip("Weapons in menu / switching order. Locked ones (WeaponData > Is Enabled off) are skipped.")]
    public List<WeaponData> weapons = new();
    [Tooltip("Equipped when a stage starts. Empty = the first weapon.")]
    [SerializeField] private WeaponData startWeapon;
    [SerializeField] private GameObject weaponSwitchIcon;

    public WeaponData Current { get; private set; }
    public WeaponBase CurrentWeapon { get; private set; }
    public bool IsShooting => CurrentWeapon != null && CurrentWeapon.IsShooting;
    public IReadOnlyList<WeaponData> All => weapons;

    private Megaman mm;
    private MegamanStats Stats => mm.Stats;
    private readonly Dictionary<WeaponData, WeaponBase> instances = new();
    private bool wasHeld;

    private float iconTimer;
    private SpriteRenderer iconRenderer;

    private static readonly int ArmorMainColor = Shader.PropertyToID("_ArmorMainColor");
    private static readonly int ArmorSecondaryColor = Shader.PropertyToID("_ArmorSecondaryColor");
    private static readonly int Outline = Shader.PropertyToID("_Outline");
    private static readonly Color DefaultOutline = new Color32(24, 24, 24, 255);
    private MaterialPropertyBlock block;

    private void Awake()
    {
        mm = GetComponent<Megaman>();
        block = new MaterialPropertyBlock();
        if (weaponSwitchIcon != null) iconRenderer = weaponSwitchIcon.GetComponent<SpriteRenderer>();
    }

    public void Begin()
    {
        foreach (var data in weapons) Spawn(data);
        SetWeapon(startWeapon != null && weapons.Contains(startWeapon) ? startWeapon : FirstEnabled());
        if (weaponSwitchIcon != null) weaponSwitchIcon.SetActive(false);
    }

    private WeaponBase Spawn(WeaponData data)
    {
        if (data == null) return null;
        if (instances.TryGetValue(data, out var existing)) return existing;
        if (data.weaponPrefab == null)
        {
            Debug.LogError($"Weapon '{data.DisplayName}' has no weapon prefab.", data);
            return null;
        }

        var go = Instantiate(data.weaponPrefab, transform);
        go.name = $"Weapon - {data.DisplayName}";
        if (!go.TryGetComponent(out WeaponBase weapon))
        {
            Debug.LogError($"The weapon prefab of '{data.DisplayName}' has no fire mode (WeaponBase) component.", data.weaponPrefab);
            Destroy(go);
            return null;
        }
        weapon.Setup(data, this);
        instances[data] = weapon;
        return weapon;
    }

    private WeaponData FirstEnabled()
    {
        foreach (var w in weapons) if (w != null && w.isEnabled) return w;
        return weapons.Count > 0 ? weapons[0] : null;
    }

    #region Switching
    /// <summary>Used by the weapons menu.</summary>
    public void SwitchWeapon(WeaponData data)
    {
        mm.Ladder.EndClimbing();
        SetWeapon(data);
    }

    public void SwitchToNext() => Cycle(+1);
    public void SwitchToPrevious() => Cycle(-1);

    private void Cycle(int step)
    {
        if (weapons.Count == 0) return;
        int index = Mathf.Max(0, weapons.IndexOf(Current));
        for (int i = 0; i < weapons.Count; i++)
        {
            index = (index + step + weapons.Count) % weapons.Count;
            if (weapons[index] != null && weapons[index].isEnabled) break;
        }
        if (weapons[index] == Current) return;
        SetWeapon(weapons[index]);
        ShowSwitchIcon();
    }

    private void SetWeapon(WeaponData data)
    {
        if (data == null) return;
        var weapon = Spawn(data);
        if (weapon == null) return;

        // Nothing from the previous weapon (charge sound, pending shot, beam) carries over
        if (CurrentWeapon != null) CurrentWeapon.OnUnequip();

        Current = data;
        CurrentWeapon = weapon;
        CurrentWeapon.OnEquip();

        UIEnergyBar.For(mm.PlayerIndex)?.SetEnergyBar(data.weaponBarSprite);
        UIEnergyBar.For(mm.PlayerIndex)?.SetVisibility(!data.usesHealthAsEnergy);
        RefreshEnergyBar();
        SetArmorColors(data.primaryColor, data.secondaryColor, DefaultOutline);
    }

    /// <summary>The HUD energy bar: the weapon's energy, or health for the Mega Buster.</summary>
    public void RefreshEnergyBar()
    {
        if (Current == null) return;
        UIEnergyBar.For(mm.PlayerIndex)?.SetValue(Current.usesHealthAsEnergy ? mm.Health.Current / (float)Stats.maxHealth : Current.EnergyFraction);
    }

    private void ShowSwitchIcon()
    {
        if (weaponSwitchIcon == null) return;
        if (iconRenderer != null) iconRenderer.sprite = Current.weaponIcon;
        weaponSwitchIcon.SetActive(true);
        iconTimer = 0f;
    }

    public void UpdateSwitchIcon()
    {
        if (weaponSwitchIcon == null || !weaponSwitchIcon.activeSelf) return;

        // Keep the icon readable when Mega Man is flipped
        Vector3 iconScale = weaponSwitchIcon.transform.localScale;
        iconScale.x = mm.FacingRight ? 1 : -1;
        weaponSwitchIcon.transform.localScale = iconScale;

        iconTimer += Time.deltaTime;
        if (iconTimer >= Stats.iconDisplayTime) weaponSwitchIcon.SetActive(false);
    }
    #endregion

    #region Shooting
    public void UseWeapon()
    {
        bool held = mm.ShootPressed;
        var input = new WeaponInput { held = held, pressed = held && !wasHeld, released = !held && wasHeld };
        wasHeld = held;
        if (CurrentWeapon != null) CurrentWeapon.Tick(input);
    }

    /// <summary>Stops everything the current weapon is doing: charge sound, pending shot, beam.</summary>
    public void CancelWeaponAction()
    {
        if (CurrentWeapon != null) CurrentWeapon.Cancel();
    }

    public void OnDamaged()
    {
        if (CurrentWeapon != null) CurrentWeapon.OnUserHurt();
    }
    #endregion

    #region IWeaponUser
    public bool FacingRight => mm.FacingRight;
    public bool CanFire => !mm.Slide.IsSliding;
    public bool IsInvincible => mm.Health.IsInvincible;
    public bool AllowCharge => Stats.chargerEnabled;

    public Vector2 MuzzlePosition
    {
        get
        {
            Vector2 offset = Stats.bulletShootOffset;
            if (!FacingRight) offset.x = -offset.x;
            return (Vector2)transform.position + offset + (FacingRight ? Vector2.right : Vector2.left) * Stats.shootRayLength;
        }
    }

    public void SetArmorColors(Color main, Color secondary, Color outline)
    {
        mm.SpriteRenderer.GetPropertyBlock(block);
        block.SetColor(ArmorMainColor, main);
        block.SetColor(ArmorSecondaryColor, secondary);
        block.SetColor(Outline, outline);
        mm.SpriteRenderer.SetPropertyBlock(block);
    }

    public void ResetArmorColors()
    {
        if (Current != null) SetArmorColors(Current.primaryColor, Current.secondaryColor, Color.black);
    }

    public void OnEnergyChanged(WeaponData weapon)
    {
        if (weapon == Current) RefreshEnergyBar();
    }
    #endregion

    #region Energy refills
    public void RestoreFullEnergy(AudioEvent itemSound)
    {
        if (Current != null && !Current.usesHealthAsEnergy && !Current.IsFull)
            StartCoroutine(IncrementEnergy(Current, Current.maxEnergy, itemSound, true));
    }

    public void RestoreEnergy(int amount, AudioEvent itemSound, bool freezeEverything = true)
    {
        if (Current != null && !Current.usesHealthAsEnergy && !Current.IsFull)
            StartCoroutine(IncrementEnergy(Current, amount, itemSound, freezeEverything));
    }

    private IEnumerator IncrementEnergy(WeaponData data, int amount, AudioEvent itemSound, bool freezeEverything)
    {
        int energyToRestore = Mathf.Clamp(amount, 0, data.maxEnergy - data.CurrentEnergy);
        if (freezeEverything) GameManager.Instance.FreezeEverything(true);

        while (energyToRestore > 0)
        {
            PlayRefillSound(itemSound);
            data.CurrentEnergy++;
            OnEnergyChanged(data);
            energyToRestore--;

            // Real time: tanks used from the paused weapons menu refill while timeScale is 0
            yield return new WaitForSecondsRealtime(Stats.refillInterval);
        }

        StopRefillSound();
        GameManager.Instance.FreezeEverything(false);
    }

    /// <summary>W-Tank: fills every weapon (not the Mega Buster) one point at a time, all together.</summary>
    public void RestoreAllWeapons(AudioEvent itemSound)
    {
        StartCoroutine(IncrementAllWeapons(itemSound));
    }

    private IEnumerator IncrementAllWeapons(AudioEvent itemSound)
    {
        while (true)
        {
            bool filling = false;
            foreach (var data in weapons)
            {
                if (data == null || data.usesHealthAsEnergy || data.IsFull) continue;
                data.CurrentEnergy++;
                filling = true;
            }
            if (!filling) break;

            PlayRefillSound(itemSound);
            RefreshEnergyBar();
            yield return new WaitForSecondsRealtime(Stats.refillInterval);
        }
        StopRefillSound();
    }

    // The fill sound loops while anything is refilling; it stops when the last refill ends (or Mega Man goes away)
    private AudioHandle refillSound;
    private AudioEvent refillEvent;

    private void PlayRefillSound(AudioEvent itemSound)
    {
        if (refillSound.IsPlayingEvent(itemSound)) return;
        refillSound = Audio.Play(itemSound);
        refillEvent = itemSound;
    }

    private void StopRefillSound()
    {
        // pooled sources are reused: only stop it if it's still our fill sound
        if (refillSound.IsPlayingEvent(refillEvent)) refillSound.Stop();
        refillSound = AudioHandle.Invalid;
    }

    private void OnEnable() => WeaponEnergy.Changed += OnEnergyChanged;

    private void OnDisable()
    {
        WeaponEnergy.Changed -= OnEnergyChanged;
        StopRefillSound();
    }

    // Energy changed from anywhere (pickups, tanks, the WeaponData Inspector in Play mode...)
    private void OnEnergyChanged(WeaponData weapon, int energy) => OnEnergyChanged(weapon);
    #endregion
}
