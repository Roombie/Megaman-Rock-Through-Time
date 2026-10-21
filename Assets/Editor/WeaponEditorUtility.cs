using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Shared drawing, validation and asset creation for the weapon tools.</summary>
public static class WeaponEditorUtility
{
    public const string WeaponDataFolder = "Assets/ScriptableObjects/WeaponData";
    public const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    public const string MegamanPrefabPath = "Assets/Prefabs/Player/Megaman.prefab";

    #region Styles
    private static GUIStyle cardTitle, cardSubtitle, cardSubtitleRight;

    public static GUIStyle CardTitle => cardTitle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = Color.white } };
    public static GUIStyle CardSubtitle => cardSubtitle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.75f, 0.85f) } };
    public static GUIStyle CardSubtitleRight => cardSubtitleRight ??= new GUIStyle(CardSubtitle) { alignment = TextAnchor.MiddleRight };
    #endregion

    #region Drawing
    /// <summary>Draws a sprite (its rect in the atlas) scaled to fit, pixel art friendly.</summary>
    public static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            EditorGUI.DrawRect(rect, new Color(1, 1, 1, 0.05f));
            return;
        }
        var tex = sprite.texture;
        var r = sprite.textureRect;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        float scale = Mathf.Max(1, Mathf.Floor(Mathf.Min(rect.width / r.width, rect.height / r.height)));
        var size = new Vector2(r.width * scale, r.height * scale);
        var drawRect = new Rect(rect.center - size / 2f, size);
        GUI.DrawTextureWithTexCoords(drawRect, tex, uv);
    }

    /// <summary>Three swatches: main, secondary, outline (Mega Man's armour colours).</summary>
    public static void DrawPalette(Rect rect, Color main, Color secondary, Color outline)
    {
        float w = rect.width / 3f;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, w - 2, rect.height), Opaque(main));
        EditorGUI.DrawRect(new Rect(rect.x + w, rect.y, w - 2, rect.height), Opaque(secondary));
        EditorGUI.DrawRect(new Rect(rect.x + 2 * w, rect.y, w - 2, rect.height), Opaque(outline));
    }

    /// <summary>A little Mega Man made of his armour colours (helmet/arms = main, body = secondary).</summary>
    public static void DrawArmorFigure(Rect rect, Color main, Color secondary, Color outline)
    {
        float u = Mathf.Floor(Mathf.Min(rect.width / 8f, rect.height / 10f));
        var o = new Vector2(rect.center.x - 4 * u, rect.center.y - 5 * u);
        void Px(float x, float y, float w, float h, Color c) => EditorGUI.DrawRect(new Rect(o.x + x * u, o.y + y * u, w * u, h * u), Opaque(c));
        Px(1, 0, 6, 10, outline);          // silhouette
        Px(2, 1, 4, 3, main);              // helmet
        Px(3, 2, 2, 2, new Color(1f, 0.85f, 0.7f));  // face
        Px(2, 4, 4, 3, secondary);         // body
        Px(0.5f, 4, 1.5f, 2, main);        // arms
        Px(6, 4, 1.5f, 2, main);
        Px(2, 7, 1.5f, 3, main);           // legs
        Px(4.5f, 7, 1.5f, 3, main);
    }

    /// <summary>Vertical NES energy bar.</summary>
    public static void DrawEnergyBar(Rect rect, float fill, int segments)
    {
        EditorGUI.DrawRect(rect, Color.black);
        segments = Mathf.Clamp(segments, 1, 40);
        float step = (rect.height - 2) / segments;
        int filled = Mathf.RoundToInt(Mathf.Clamp01(fill) * segments);
        for (int i = 0; i < filled; i++)
        {
            float y = rect.yMax - 1 - (i + 1) * step;
            EditorGUI.DrawRect(new Rect(rect.x + 2, y + step * 0.25f, rect.width - 4, Mathf.Max(1, step * 0.5f)), new Color(0.99f, 0.89f, 0.66f));
        }
    }

    private static Color Opaque(Color c) => new(c.r, c.g, c.b, 1f);
    #endregion

    #region Fire modes
    public static IEnumerable<Type> FireModeTypes() =>
        TypeCache.GetTypesDerivedFrom<WeaponBase>().Where(t => !t.IsAbstract).OrderBy(t => t == typeof(SingleShotWeapon) ? 0 : t == typeof(ChargeShotWeapon) ? 1 : t == typeof(HoldBeamWeapon) ? 2 : 3).ThenBy(t => t.Name);

    public static string FireModeName(GameObject weaponPrefab)
    {
        if (weaponPrefab == null) return "No weapon prefab";
        var mode = weaponPrefab.GetComponent<WeaponBase>();
        return mode == null ? "Weapon prefab has no fire mode" : ObjectNames.NicifyVariableName(mode.GetType().Name);
    }
    #endregion

    #region Validation
    public static IEnumerable<(string, MessageType)> Validate(WeaponData data)
    {
        if (data.weaponPrefab == null)
        {
            yield return ("No weapon prefab: pick a fire mode below to create one.", MessageType.Warning);
        }
        else
        {
            var mode = data.weaponPrefab.GetComponent<WeaponBase>();
            if (mode == null) yield return ("The weapon prefab has no fire mode (a WeaponBase component).", MessageType.Error);
            switch (mode)
            {
                case SingleShotWeapon s when s.projectilePrefab == null:
                    yield return ("Single Shot: no projectile prefab.", MessageType.Error);
                    break;
                case SingleShotWeapon s when s.projectilePrefab.GetComponent<Projectile>() == null:
                    yield return ("Single Shot: the projectile prefab has no Projectile component.", MessageType.Error);
                    break;
                case ChargeShotWeapon c:
                    if (c.chargeLevels == null || c.chargeLevels.Count == 0)
                        yield return ("Charge Shot: no charge levels (level 0 is the normal shot).", MessageType.Error);
                    else
                    {
                        for (int i = 0; i < c.chargeLevels.Count; i++)
                        {
                            if (c.chargeLevels[i].projectilePrefab == null)
                                yield return ($"Charge Shot: level {i} has no projectile prefab.", MessageType.Error);
                            if (i > 0 && c.chargeLevels[i].timeRequired <= c.chargeLevels[i - 1].timeRequired)
                                yield return ($"Charge Shot: level {i} needs more time than level {i - 1}.", MessageType.Warning);
                        }
                    }
                    break;
                case HoldBeamWeapon b when b.beamPrefab == null:
                    yield return ("Hold Beam: no beam prefab.", MessageType.Error);
                    break;
            }
        }

        if (data.weaponIcon == null) yield return ("No icon (weapons menu and switch icon).", MessageType.Info);
        if (data.weaponBarSprite == null && !data.usesHealthAsEnergy) yield return ("No energy bar sprite for the HUD.", MessageType.Info);
        if (!data.usesHealthAsEnergy && data.energyCost > data.maxEnergy)
            yield return ("Energy cost is higher than max energy: it can never fire.", MessageType.Warning);

        var megaman = AssetDatabase.LoadAssetAtPath<GameObject>(MegamanPrefabPath);
        var weapons = megaman != null ? megaman.GetComponent<MegamanWeapons>() : null;
        if (weapons != null && !weapons.weapons.Contains(data))
            yield return ("Mega Man doesn't have this weapon yet (Mega Man prefab > Megaman Weapons > Weapons).", MessageType.Info);
    }
    #endregion

    #region Creation
    public static string SafeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "");
        return string.IsNullOrWhiteSpace(name) ? "NewWeapon" : name.Replace(" ", "").Replace(".", "");
    }

    /// <summary>Creates "&lt;Name&gt;Weapon.prefab" with the fire mode component.</summary>
    public static GameObject CreateWeaponPrefab(string weaponName, Type fireMode, Action<WeaponBase> configure = null)
    {
        EnsureFolder(WeaponPrefabFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{WeaponPrefabFolder}/{SafeName(weaponName)}Weapon.prefab");
        var go = new GameObject(Path.GetFileNameWithoutExtension(path));
        var mode = (WeaponBase)go.AddComponent(fireMode);
        configure?.Invoke(mode);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Creates "&lt;Name&gt;Shot.prefab": sprite, kinematic body, trigger collider, BasicProjectile.</summary>
    public static GameObject CreateProjectilePrefab(string weaponName, Sprite sprite, ProjectileMovement movement, IEnumerable<HitEffect> effects, bool pierce)
    {
        EnsureFolder(WeaponPrefabFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{WeaponPrefabFolder}/{SafeName(weaponName)}Shot.prefab");
        var go = new GameObject(Path.GetFileNameWithoutExtension(path));

        // Same setup as the Mega Buster shot
        var template = AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponPrefabFolder}/MegaBuster.prefab");
        if (template != null)
        {
            go.layer = template.layer;
            try { go.tag = template.tag; } catch (UnityException) { }
        }

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (template != null && template.TryGetComponent(out SpriteRenderer tr))
        {
            renderer.sharedMaterial = tr.sharedMaterial;
            renderer.sortingLayerID = tr.sortingLayerID;
            renderer.sortingOrder = tr.sortingOrder;
        }

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.None;

        var collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        if (sprite != null) collider.size = sprite.bounds.size;

        var projectile = go.AddComponent<BasicProjectile>();
        projectile.movement = movement;
        projectile.hitEffects = effects != null ? effects.ToList() : new List<HitEffect> { new DamageEffect() };
        projectile.pierce = pierce;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Adds the weapon to the Mega Man prefab's weapon list (if it isn't there).</summary>
    public static bool AddToMegaman(WeaponData data)
    {
        var root = PrefabUtility.LoadPrefabContents(MegamanPrefabPath);
        try
        {
            var weapons = root.GetComponent<MegamanWeapons>();
            if (weapons == null || weapons.weapons.Contains(data)) return false;
            weapons.weapons.Add(data);
            PrefabUtility.SaveAsPrefabAsset(root, MegamanPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    #endregion
}
