// Assets/Scripts/Weapons/WeaponData.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    public float frameDuration;
    public ChargeColorFrame[] frames;
}

[System.Serializable]
public struct ChargeLevel
{
    public float timeRequired; // Time required to reach this charge level
    public int damage;         // Damage associated with this charge level
    public GameObject projectilePrefab; // The prefab to instantiate at this charge level
    public AudioClip weaponClip;
    public float bulletSpeed;
    public ChargeColorAnimation colorAnimation;
}


[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public bool isEnabled = true;
    public GameObject weaponPrefab;
    public Sprite weaponIcon;
    public Sprite weaponIconNotSelected;
    public Sprite weaponBarSprite;
    public AudioClip weaponClip;
    public Color primaryColor;
    public Color secondaryColor;
    public List<ChargeLevel> chargeLevels;
    [HideInInspector] public ChargeColorAnimation[] chargeColorAnimations;
    public float shootDelay;
    public int maxEnergy;
    public int currentEnergy;
    public int energyCost;
    public bool limitBulletsOnScreen = true; // Control whether to limit bullets or not
    public int maxBulletsOnScreen = 3; // Max bullets allowed on screen

    // Damage
    public int baseDamage;

    // Homing
    public bool hasHoming;
    public float homingStrength;

    // Angle & Direction
    public bool allowsAngleAdjustment;
    public float minAngle;
    public float maxAngle;

    // Skin Variations
    public Sprite[] skinSprites;

    // Freeze Enemies
    public bool canFreeze;
    public float freezeDuration;

    // Splash Damage
    public bool hasSplashDamage;
    public float splashRadius;
    public int splashDamage;

    // Time Slow
    public bool canSlowTime;
    public float slowAmount;
    public float slowDuration;

    // Additional features can be added here
}
