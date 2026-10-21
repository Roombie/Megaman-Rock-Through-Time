using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Live weapon energy, kept outside the WeaponData assets (they're definitions only). Lasts across scenes
/// and respawns for the whole session and starts full every time the game starts. Use WeaponData.CurrentEnergy.
/// </summary>
public static class WeaponEnergy
{
    private static readonly Dictionary<WeaponData, int> energy = new();

    /// <summary>A weapon's energy changed (weapon, new value).</summary>
    public static event Action<WeaponData, int> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        energy.Clear();
        Changed = null;
    }

    public static int Get(WeaponData weapon)
    {
        if (weapon == null) return 0;
        return energy.TryGetValue(weapon, out int value) ? value : weapon.maxEnergy;
    }

    public static void Set(WeaponData weapon, int value)
    {
        if (weapon == null) return;
        value = Mathf.Clamp(value, 0, weapon.maxEnergy);
        if (energy.TryGetValue(weapon, out int old) && old == value) return;
        energy[weapon] = value;
        Changed?.Invoke(weapon, value);
    }

    public static void Refill(WeaponData weapon) => Set(weapon, weapon != null ? weapon.maxEnergy : 0);
}
