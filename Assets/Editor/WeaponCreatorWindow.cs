using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Mega Man > Weapon Creator: makes a complete weapon in one go (WeaponData + weapon prefab with its fire
/// mode + projectile prefab) and gives it to Mega Man. Everything can be tweaked afterwards in the WeaponData.
/// </summary>
public class WeaponCreatorWindow : EditorWindow
{
    private enum FireMode { SingleShot, ChargeShot, HoldBeam }

    [SerializeField] private string weaponName = "New Weapon";
    [SerializeField] private FireMode fireMode = FireMode.SingleShot;

    [SerializeField] private Sprite icon;
    [SerializeField] private Sprite iconNotSelected;
    [SerializeField] private Sprite energyBar;
    [SerializeField] private Color primaryColor = new(0.85f, 0.25f, 0.1f, 1f);
    [SerializeField] private Color secondaryColor = new(0.99f, 0.75f, 0.35f, 1f);
    [SerializeField] private int maxEnergy = 28;
    [SerializeField] private int energyCost = 1;
    [SerializeField] private AudioEvent fireSound;

    // Projectile (single / charge)
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private int damage = 2;
    [SerializeField] private float speed = 15f;
    [SerializeField] private float fireCooldown = 0.1f;
    [SerializeField] private bool autoFire;
    [SerializeField] private bool pierce;
    [SerializeField] private int maxOnScreen = 3;
    [SerializeReference, SubclassSelector] private ProjectileMovement movement = new StraightMovement();
    [SerializeReference, SubclassSelector] private List<HitEffect> hitEffects = new() { new DamageEffect() };

    // Beam
    [SerializeField] private GameObject beamPrefab;
    [SerializeField] private int tilesPerEnergy = 2;

    [SerializeField] private bool giveToMegaman = true;

    private SerializedObject so;
    private Vector2 scroll;

    [MenuItem("Tools/Mega Man/Weapon Creator")]
    public static void Open() => GetWindow<WeaponCreatorWindow>("Weapon Creator").minSize = new Vector2(380, 520);

    private void OnEnable()
    {
        so = new SerializedObject(this);
        if (beamPrefab == null) beamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponEditorUtility.WeaponPrefabFolder}/MagnetBeamShot.prefab");
    }

    private void OnGUI()
    {
        so.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawPreview();
        EditorGUILayout.Space(6);

        Section("Weapon");
        Prop(nameof(weaponName), "Name");
        Prop(nameof(fireMode), "Fire Mode");
        EditorGUILayout.HelpBox(fireMode switch
        {
            FireMode.SingleShot => "Each press fires one projectile (or keeps firing while held, with Auto Fire). Most special weapons.",
            FireMode.ChargeShot => "Mega Buster style: press for a normal shot, hold to charge. Add more charge levels in the WeaponData afterwards.",
            _ => "Magnet Beam style: a beam that grows while the button is held.",
        }, MessageType.None);

        Section("Look");
        Prop(nameof(icon));
        Prop(nameof(iconNotSelected), "Icon (not equipped)");
        Prop(nameof(energyBar), "Energy Bar Sprite");
        Prop(nameof(primaryColor), "Armour Main");
        Prop(nameof(secondaryColor), "Armour Secondary");
        Prop(nameof(fireSound));

        Section("Energy");
        Prop(nameof(maxEnergy));
        Prop(nameof(energyCost), fireMode == FireMode.HoldBeam ? "Energy Cost (if Tiles Per Energy = 0)" : "Energy Cost");
        Prop(nameof(maxOnScreen), "Max On Screen");

        if (fireMode == FireMode.HoldBeam)
        {
            Section("Beam");
            Prop(nameof(beamPrefab));
            Prop(nameof(tilesPerEnergy));
        }
        else
        {
            Section("Projectile");
            Prop(nameof(projectileSprite), "Sprite");
            Prop(nameof(damage));
            Prop(nameof(speed));
            if (fireMode == FireMode.SingleShot)
            {
                Prop(nameof(fireCooldown));
                Prop(nameof(autoFire));
            }
            Prop(nameof(pierce));
            Prop(nameof(movement));
            Prop(nameof(hitEffects));
        }

        Section("Mega Man");
        Prop(nameof(giveToMegaman), "Add To Mega Man's Weapons");

        so.ApplyModifiedProperties();

        EditorGUILayout.Space(10);
        string problem = Problem();
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        using (new EditorGUI.DisabledScope(problem != null))
        {
            if (GUILayout.Button("Create Weapon", GUILayout.Height(32))) Create();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawPreview()
    {
        var rect = EditorGUILayout.GetControlRect(false, 72);
        EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.1f));
        WeaponEditorUtility.DrawArmorFigure(new Rect(rect.x + 6, rect.y + 6, 50, 60), primaryColor, secondaryColor, new Color32(24, 24, 24, 255));
        WeaponEditorUtility.DrawSprite(new Rect(rect.x + 62, rect.y + 12, 48, 48), icon);
        EditorGUI.LabelField(new Rect(rect.x + 118, rect.y + 10, rect.width - 160, 20), weaponName.ToUpperInvariant(), WeaponEditorUtility.CardTitle);
        EditorGUI.LabelField(new Rect(rect.x + 118, rect.y + 30, rect.width - 160, 16), ObjectNames.NicifyVariableName(fireMode.ToString()), WeaponEditorUtility.CardSubtitle);
        WeaponEditorUtility.DrawSprite(new Rect(rect.x + 118, rect.y + 48, 20, 20), projectileSprite);
        WeaponEditorUtility.DrawEnergyBar(new Rect(rect.xMax - 22, rect.y + 6, 12, 60), 1f, maxEnergy);
    }

    private static void Section(string title)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void Prop(string name, string label = null)
    {
        var p = so.FindProperty(name);
        if (label == null) EditorGUILayout.PropertyField(p, true);
        else EditorGUILayout.PropertyField(p, new GUIContent(label), true);
    }

    private string Problem()
    {
        if (string.IsNullOrWhiteSpace(weaponName)) return "Give the weapon a name.";
        string dataPath = $"{WeaponEditorUtility.WeaponDataFolder}/{WeaponEditorUtility.SafeName(weaponName)}.asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponData>(dataPath) != null) return $"There's already a weapon called '{weaponName}'.";
        if (fireMode == FireMode.HoldBeam && beamPrefab == null) return "Pick the beam prefab.";
        if (fireMode != FireMode.HoldBeam && movement == null) return "Pick how the projectile moves.";
        return null;
    }

    private void Create()
    {
        // Projectile
        GameObject projectile = null;
        if (fireMode != FireMode.HoldBeam)
        {
            var effects = new List<HitEffect>();
            foreach (var e in hitEffects) if (e != null) effects.Add(Clone(e));
            projectile = WeaponEditorUtility.CreateProjectilePrefab(weaponName, projectileSprite, Clone(movement), effects, pierce);
        }

        // Weapon prefab (fire mode)
        GameObject weaponPrefab = fireMode switch
        {
            FireMode.SingleShot => WeaponEditorUtility.CreateWeaponPrefab(weaponName, typeof(SingleShotWeapon), m =>
            {
                var s = (SingleShotWeapon)m;
                s.projectilePrefab = projectile; s.damage = damage; s.speed = speed; s.fireCooldown = fireCooldown; s.autoFire = autoFire;
            }),
            FireMode.ChargeShot => WeaponEditorUtility.CreateWeaponPrefab(weaponName, typeof(ChargeShotWeapon), m =>
            {
                ((ChargeShotWeapon)m).chargeLevels = new List<ChargeLevel>
                {
                    new() { timeRequired = 0f, damage = damage, projectilePrefab = projectile, bulletSpeed = speed, weaponClip = fireSound },
                };
            }),
            _ => WeaponEditorUtility.CreateWeaponPrefab(weaponName, typeof(HoldBeamWeapon), m =>
            {
                var b = (HoldBeamWeapon)m;
                b.beamPrefab = beamPrefab; b.tilesPerEnergy = tilesPerEnergy;
            }),
        };

        // Data
        WeaponEditorUtility.EnsureFolder(WeaponEditorUtility.WeaponDataFolder);
        var data = CreateInstance<WeaponData>();
        data.weaponName = weaponName;
        data.weaponIcon = icon;
        data.weaponIconNotSelected = iconNotSelected;
        data.weaponBarSprite = energyBar;
        data.primaryColor = primaryColor;
        data.secondaryColor = secondaryColor;
        data.weaponPrefab = weaponPrefab;
        data.weaponClip = fireSound;
        data.maxEnergy = Mathf.Max(1, maxEnergy);
        data.energyCost = Mathf.Max(0, energyCost);
        data.maxBulletsOnScreen = Mathf.Max(1, maxOnScreen);
        string path = $"{WeaponEditorUtility.WeaponDataFolder}/{WeaponEditorUtility.SafeName(weaponName)}.asset";
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();

        if (giveToMegaman) WeaponEditorUtility.AddToMegaman(data);

        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);
        Debug.Log($"Weapon '{weaponName}' created: {path}", data);
    }

    // The window's instances must not be shared with the prefab
    private static T Clone<T>(T source) where T : class =>
        source == null ? null : (T)JsonUtility.FromJson(JsonUtility.ToJson(source), source.GetType());
}
