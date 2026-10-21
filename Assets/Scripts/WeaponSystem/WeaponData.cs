using UnityEngine;

/// <summary>
/// A weapon's definition: name, icons, palette, energy and which weapon prefab fires it.
/// The firing behaviour lives in the weapon prefab (a fire mode component: SingleShotWeapon, ChargeShotWeapon,
/// HoldBeamWeapon... or your own WeaponBase). Live energy is not stored here (see CurrentEnergy).
/// Make one with Tools > Mega Man > Weapon Creator, or Create > Weapons > WeaponData.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponName;
    [Tooltip("Unlocked: shown in the weapons menu and reachable with the switch buttons.")]
    public bool isEnabled = true;
    public Sprite weaponIcon;
    [Tooltip("Icon when it isn't the equipped weapon (optional).")]
    public Sprite weaponIconNotSelected;
    [Tooltip("Energy bar sprite in the HUD.")]
    public Sprite weaponBarSprite;

    [Header("Palette")]
    [Tooltip("Mega Man's main armour colour (helmet, arms, legs).")]
    public Color primaryColor = new(0f, 0.44f, 0.925f, 1f);
    [Tooltip("Mega Man's secondary armour colour (body).")]
    public Color secondaryColor = new(0f, 0.91f, 0.847f, 1f);

    [Header("Behaviour")]
    [Tooltip("Prefab with the fire mode component (SingleShotWeapon, ChargeShotWeapon, HoldBeamWeapon...).")]
    public GameObject weaponPrefab;
    [Tooltip("Default fire sound (a fire mode or charge level can override it).")]
    public AudioEvent weaponClip;

    [Header("Energy")]
    [Min(1)] public int maxEnergy = 28;
    [Tooltip("Energy used per shot.")]
    [Min(0)] public int energyCost = 1;
    [Tooltip("Mega Buster style: no energy of its own, its bar shows Mega Man's health.")]
    public bool usesHealthAsEnergy;

    [Header("Projectiles")]
    public bool limitBulletsOnScreen = true;
    [Min(1)] public int maxBulletsOnScreen = 3;

    /// <summary>Live energy for this session (not saved in the asset).</summary>
    public int CurrentEnergy
    {
        get => WeaponEnergy.Get(this);
        set => WeaponEnergy.Set(this, value);
    }

    public float EnergyFraction => maxEnergy > 0 ? CurrentEnergy / (float)maxEnergy : 0f;
    public bool IsFull => CurrentEnergy >= maxEnergy;
    public string DisplayName => string.IsNullOrEmpty(weaponName) ? name : weaponName;
}
