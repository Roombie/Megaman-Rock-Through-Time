using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Weapon Lab helper (in the WeaponLab scene): infinite energy, an info panel (weapon, energy, charge level,
/// dummy totals) and shortcuts. R = reset the dummies, F1 = hide/show the panel.
/// Pair it with the Scene view's Weapon Preview to compare the predicted path with the real one.
/// </summary>
public class WeaponLab : MonoBehaviour
{
    [Tooltip("Keep every weapon's energy full.")]
    public bool infiniteEnergy = true;
    [Tooltip("Keep Mega Man's health full.")]
    public bool invincibleMegaman;
    public bool showPanel = true;

    private Megaman megaman;
    private TrainingDummy[] dummies;
    private GUIStyle style;

    private void Start()
    {
        megaman = Players.First;
        dummies = FindObjectsByType<TrainingDummy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void Update()
    {
        if (megaman == null) megaman = Players.First;   // after a respawn (the lab is for player 1)

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame) ResetDummies();
            if (keyboard.f1Key.wasPressedThisFrame) showPanel = !showPanel;
        }

        if (megaman == null) return;
        if (infiniteEnergy)
            foreach (var w in megaman.GetWeapons())
                if (w != null && !w.usesHealthAsEnergy && !w.IsFull) w.CurrentEnergy = w.maxEnergy;
        if (invincibleMegaman && megaman.CurrentHealth < megaman.MaxHealth)
            megaman.RestoreHealth(megaman.MaxHealth, null, false);
    }

    public void ResetDummies()
    {
        foreach (var d in dummies)
        {
            if (d == null) continue;
            d.gameObject.SetActive(true);
            d.ResetStats();
        }
    }

    private void OnGUI()
    {
        if (!showPanel) return;
        style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12, richText = true };

        string text = "<b>WEAPON LAB</b>   R reset dummies · F1 hide\n";
        if (megaman != null && megaman.Weapons != null && megaman.Weapons.Current != null)
        {
            var data = megaman.Weapons.Current;
            var weapon = megaman.Weapons.CurrentWeapon;
            text += $"Weapon: <b>{data.DisplayName}</b> ({(weapon != null ? weapon.GetType().Name : "-")})\n";
            text += data.usesHealthAsEnergy ? "Energy: health\n" : $"Energy: {data.CurrentEnergy}/{data.maxEnergy}  cost {data.energyCost}\n";
            if (weapon is ChargeShotWeapon charge) text += $"Charge level: {charge.CurrentLevel}\n";
        }
        int total = 0;
        float bestDps = 0f;
        foreach (var d in dummies)
        {
            if (d == null) continue;
            total += d.TotalDamage;
            bestDps = Mathf.Max(bestDps, d.DamagePerSecond);
        }
        text += $"Damage dealt: {total}   Best burst: {bestDps:0.0}/s";

        GUI.Box(new Rect(10, Screen.height - 96, 330, 86), text, style);
    }
}
