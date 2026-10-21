using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Scene view "Weapon Preview": draws where Mega Man's shots go from where he stands, in any scene, in Edit or Play
/// mode. The path, aim direction, numbered bounces, hits (with the damage they'd do, weakness tables included),
/// where and why it ends, splash / homing radii, and the Magnet Beam's reach. Uses the same movement and
/// collision code as the game. Show it from the Scene view's Overlays menu (or Tools > Mega Man > Weapon Preview).
/// </summary>
[Overlay(typeof(SceneView), Id, "Weapon Preview", true)]
public class WeaponPreviewOverlay : IMGUIOverlay
{
    public const string Id = "megaman-weapon-preview";

    public override void OnGUI()
    {
        var s = WeaponPreview.Settings;
        EditorGUI.BeginChangeCheck();

        s.enabled = EditorGUILayout.ToggleLeft("Show", s.enabled);
        using (new EditorGUI.DisabledScope(!s.enabled))
        {
            var megaman = WeaponPreview.FindMegaman();
            if (megaman == null)
            {
                EditorGUILayout.HelpBox("No Mega Man in the scene.", MessageType.None);
            }
            else
            {
                var weapons = WeaponPreview.WeaponsOf(megaman);
                if (weapons.Count == 0) EditorGUILayout.HelpBox("Mega Man has no weapons.", MessageType.None);
                else
                {
                    string[] names = weapons.ConvertAll(w => w != null ? w.DisplayName : "(missing)").ToArray();
                    if (Application.isPlaying) s.followEquipped = EditorGUILayout.ToggleLeft("Equipped weapon", s.followEquipped);
                    using (new EditorGUI.DisabledScope(Application.isPlaying && s.followEquipped))
                        s.weapon = EditorGUILayout.Popup(Mathf.Clamp(s.weapon, 0, names.Length - 1), names);

                    var data = WeaponPreview.SelectedWeapon(megaman);
                    var mode = data != null && data.weaponPrefab != null ? data.weaponPrefab.GetComponent<WeaponBase>() : null;
                    if (mode is ChargeShotWeapon charge && charge.chargeLevels.Count > 1)
                        s.chargeLevel = EditorGUILayout.IntSlider("Level", Mathf.Clamp(s.chargeLevel, 0, charge.chargeLevels.Count - 1), 0, charge.chargeLevels.Count - 1);
                }
                s.facing = (WeaponPreview.Facing)EditorGUILayout.EnumPopup(s.facing);
                s.showScreen = EditorGUILayout.ToggleLeft("Screen bounds", s.showScreen);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            s.Save();
            SceneView.RepaintAll();
        }
    }
}

[InitializeOnLoad]
public static class WeaponPreview
{
    public enum Facing { AsPlaced, Right, Left, Both }

    public class PreviewSettings
    {
        public bool enabled = true;
        public int weapon;
        public int chargeLevel;
        public Facing facing = Facing.AsPlaced;
        public bool followEquipped = true;
        public bool showScreen = true;

        public void Save() => EditorPrefs.SetString(Key, JsonUtility.ToJson(this));
    }

    private const string Key = "MegaMan.WeaponPreview";
    public static readonly PreviewSettings Settings;

    private static readonly Color PathColor = new(0.3f, 0.9f, 1f);
    private static readonly Color BounceColor = new(1f, 0.85f, 0.2f);
    private static readonly Color HitColor = new(1f, 0.35f, 0.3f);
    private static readonly Color EndColor = new(1f, 1f, 1f, 0.9f);

    static WeaponPreview()
    {
        Settings = JsonUtility.FromJson<PreviewSettings>(EditorPrefs.GetString(Key, "")) ?? new PreviewSettings();
        SceneView.duringSceneGui += Draw;
    }

    [MenuItem("Tools/Mega Man/Weapon Preview")]
    private static void ShowOverlay()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        if (view.TryGetOverlay(WeaponPreviewOverlay.Id, out Overlay overlay)) overlay.displayed = true;
        Settings.enabled = true;
        Settings.Save();
    }

    #region Lookups
    public static Megaman FindMegaman()
    {
        var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Megaman>() : null;
        return selected != null ? selected : Object.FindFirstObjectByType<Megaman>();
    }

    public static List<WeaponData> WeaponsOf(Megaman megaman)
    {
        var w = megaman.GetComponent<MegamanWeapons>();
        return w != null ? w.weapons : new List<WeaponData>();
    }

    public static WeaponData SelectedWeapon(Megaman megaman)
    {
        if (Application.isPlaying && Settings.followEquipped && megaman.Weapons != null && megaman.Weapons.Current != null)
            return megaman.Weapons.Current;
        var list = WeaponsOf(megaman);
        return list.Count > 0 ? list[Mathf.Clamp(Settings.weapon, 0, list.Count - 1)] : null;
    }
    #endregion

    private static void Draw(SceneView view)
    {
        if (!Settings.enabled || Event.current.type != EventType.Repaint) return;
        if (!view.TryGetOverlay(WeaponPreviewOverlay.Id, out Overlay overlay) || !overlay.displayed) return;

        var megaman = FindMegaman();
        if (megaman == null) return;
        var stats = megaman.Stats;
        var data = SelectedWeapon(megaman);
        if (stats == null || data == null || data.weaponPrefab == null) return;
        var mode = data.weaponPrefab.GetComponent<WeaponBase>();
        if (mode == null) return;

        if (!Application.isPlaying) Physics2D.SyncTransforms();
        Rect screen = ScreenRect();
        if (Settings.showScreen) DrawScreen(screen);

        bool placedRight = megaman.transform.localScale.x >= 0f;
        if (Application.isPlaying) placedRight = megaman.FacingRight;
        switch (Settings.facing)
        {
            case Facing.AsPlaced: DrawShot(megaman, stats, data, mode, placedRight, screen); break;
            case Facing.Right: DrawShot(megaman, stats, data, mode, true, screen); break;
            case Facing.Left: DrawShot(megaman, stats, data, mode, false, screen); break;
            default:
                DrawShot(megaman, stats, data, mode, true, screen);
                DrawShot(megaman, stats, data, mode, false, screen);
                break;
        }
    }

    private static Vector2 Muzzle(Megaman megaman, MegamanStats stats, bool right)
    {
        Vector2 offset = stats.bulletShootOffset;
        if (!right) offset.x = -offset.x;
        return (Vector2)megaman.transform.position + offset + (right ? Vector2.right : Vector2.left) * stats.shootRayLength;
    }

    private static void DrawShot(Megaman megaman, MegamanStats stats, WeaponData data, WeaponBase mode, bool right, Rect screen)
    {
        Vector2 muzzle = Muzzle(megaman, stats, right);
        Vector2 dir = right ? Vector2.right : Vector2.left;

        // Muzzle + aim arrow
        Handles.color = PathColor;
        Handles.DrawSolidDisc(muzzle, Vector3.forward, 0.08f);
        Handles.DrawAAPolyLine(4f, muzzle, muzzle + dir * 1.2f);
        Handles.DrawAAConvexPolygon(muzzle + dir * 1.45f, muzzle + dir * 1.15f + Vector2.up * 0.15f, muzzle + dir * 1.15f + Vector2.down * 0.15f);

        switch (mode)
        {
            case HoldBeamWeapon beam:
                DrawBeam(beam, data, muzzle, dir);
                return;
            case SingleShotWeapon single:
                DrawProjectile(single.projectilePrefab, data, megaman, single.damage, single.speed, 0, right, muzzle, screen);
                return;
            case ChargeShotWeapon charge when charge.chargeLevels.Count > 0:
                int level = Mathf.Clamp(Settings.chargeLevel, 0, charge.chargeLevels.Count - 1);
                var l = charge.chargeLevels[level];
                DrawProjectile(l.projectilePrefab, data, megaman, l.damage, l.bulletSpeed, level, right, muzzle, screen);
                return;
            default:
                Label(muzzle + Vector2.up * 0.6f, $"{mode.GetType().Name}: no preview", EndColor);
                return;
        }
    }

    private static void DrawProjectile(GameObject prefab, WeaponData data, Megaman megaman, int damage, float speed, int level, bool right, Vector2 muzzle, Rect screen)
    {
        if (prefab == null) { Label(muzzle + Vector2.up * 0.6f, "No projectile prefab", HitColor); return; }
        var projectile = prefab.GetComponent<BasicProjectile>();
        if (projectile == null)
        {
            // Not a BasicProjectile: straight line at its speed
            Handles.color = PathColor;
            Handles.DrawDottedLine(muzzle, muzzle + (right ? Vector2.right : Vector2.left) * screen.width, 4f);
            Label(muzzle + Vector2.up * 0.6f, $"{prefab.name}: custom projectile (straight line shown)", EndColor);
            return;
        }

        var shot = new ShotInfo { weapon = data, owner = megaman.gameObject, damage = damage, speed = speed, level = level, facingRight = right };
        var r = WeaponTrajectory.Simulate(projectile, shot, muzzle, screen);

        // Path (width = the projectile's collision size)
        Handles.color = PathColor;
        Handles.DrawAAPolyLine(3f, r.points.ConvertAll(p => (Vector3)p).ToArray());
        Handles.color = new Color(PathColor.r, PathColor.g, PathColor.b, 0.25f);
        for (int i = 0; i < r.points.Count; i += 6) Handles.DrawWireDisc(r.points[i], Vector3.forward, r.radius);

        // Bounces, numbered
        for (int i = 0; i < r.bounces.Count; i++)
        {
            var b = r.bounces[i];
            Handles.color = BounceColor;
            Handles.DrawSolidDisc(b.position, Vector3.forward, 0.12f);
            Handles.DrawAAPolyLine(2f, b.position, b.position + b.normal * 0.5f);
            Label(b.position + b.normal * 0.7f, (i + 1).ToString(), BounceColor);
        }

        // Hits
        foreach (var h in r.hits)
        {
            Handles.color = HitColor;
            Handles.DrawAAPolyLine(3f, h.position + new Vector2(-0.2f, -0.2f), h.position + new Vector2(0.2f, 0.2f));
            Handles.DrawAAPolyLine(3f, h.position + new Vector2(-0.2f, 0.2f), h.position + new Vector2(0.2f, -0.2f));
            Label(h.position + Vector2.up * 0.5f, h.damage > 0 ? $"-{h.damage}" : "immune", HitColor);
        }

        // Splash / homing radii
        foreach (var effect in projectile.hitEffects)
            if (effect is SplashDamageEffect splash)
            {
                Handles.color = new Color(HitColor.r, HitColor.g, HitColor.b, 0.6f);
                Handles.DrawWireDisc(r.end, Vector3.forward, splash.radius);
            }
        if (projectile.movement is HomingMovement homing)
        {
            Handles.color = new Color(PathColor.r, PathColor.g, PathColor.b, 0.4f);
            Handles.DrawWireDisc(muzzle, Vector3.forward, homing.range);
        }

        // End
        Handles.color = EndColor;
        Handles.DrawWireDisc(r.end, Vector3.forward, 0.18f);
        string bounces = projectile.bounces > 0 ? $" · bounces {r.bounces.Count}/{projectile.bounces}" : "";
        Label(r.end + Vector2.down * 0.5f, $"{r.endReason} · {r.time:0.00}s{bounces}", EndColor);
    }

    private static void DrawBeam(HoldBeamWeapon weapon, WeaponData data, Vector2 muzzle, Vector2 dir)
    {
        var beam = weapon.beamPrefab != null ? weapon.beamPrefab.GetComponent<MagnetBeam>() : null;
        if (beam == null) { Label(muzzle + Vector2.up * 0.6f, "No beam prefab", HitColor); return; }

        float tile = Mathf.Max(0.05f, beam.TileLength);
        float length = beam.MaxBeamLength;
        string reason = "max length";
        var wall = Physics2D.Raycast(muzzle, dir, length, beam.WallLayer);
        if (wall.collider != null)
        {
            length = Mathf.Floor(wall.distance / tile + 0.001f) * tile;
            reason = $"wall ({wall.collider.name})";
        }
        int tiles = Mathf.FloorToInt(length / tile + 0.001f);
        int energy = weapon.tilesPerEnergy > 0 ? Mathf.CeilToInt(tiles / (float)weapon.tilesPerEnergy) : data.energyCost;

        Handles.color = PathColor;
        Vector2 end = muzzle + dir * length;
        Handles.DrawAAPolyLine(5f, muzzle, end);
        for (int i = 1; i <= tiles; i++)
        {
            Vector2 p = muzzle + dir * (i * tile);
            Handles.DrawAAPolyLine(2f, p + Vector2.up * 0.15f, p + Vector2.down * 0.15f);
        }
        Label(end + Vector2.down * 0.5f, $"{tiles} tiles · {energy} energy · stops at {reason}", EndColor);
    }

    #region Helpers
    private static Rect ScreenRect()
    {
        var cam = Camera.main;
        Vector2 size = new(16f, 15f);   // 256x240 at 16 px per unit
        if (cam != null && cam.TryGetComponent(out PixelPerfectCamera ppc) && ppc.assetsPPU > 0)
            size = new Vector2(ppc.refResolutionX, ppc.refResolutionY) / ppc.assetsPPU;
        Vector2 center = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        return new Rect(center - size / 2f, size);
    }

    private static void DrawScreen(Rect r)
    {
        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        Handles.DrawDottedLines(new Vector3[]
        {
            new(r.xMin, r.yMin), new(r.xMax, r.yMin), new(r.xMax, r.yMin), new(r.xMax, r.yMax),
            new(r.xMax, r.yMax), new(r.xMin, r.yMax), new(r.xMin, r.yMax), new(r.xMin, r.yMin),
        }, 4f);
    }

    private static GUIStyle labelStyle;

    private static void Label(Vector2 position, string text, Color color)
    {
        labelStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        labelStyle.normal.textColor = color;
        Handles.Label(position, text, labelStyle);
    }
    #endregion
}
