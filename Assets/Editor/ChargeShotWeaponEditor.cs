using UnityEditor;
using UnityEngine;

/// <summary>
/// Charge Shot Inspector: a timeline of the charge levels and a live preview of each level's armour flashing.
/// </summary>
[CustomEditor(typeof(ChargeShotWeapon))]
public class ChargeShotWeaponEditor : Editor
{
    public override bool RequiresConstantRepaint() => true;   // animates the colour previews

    public override void OnInspectorGUI()
    {
        var weapon = (ChargeShotWeapon)target;
        DrawTimeline(weapon);
        EditorGUILayout.Space(4);
        DrawPreviews(weapon);
        EditorGUILayout.Space(6);
        DrawDefaultInspector();
    }

    private static void DrawTimeline(ChargeShotWeapon weapon)
    {
        var levels = weapon.chargeLevels;
        if (levels == null || levels.Count == 0) return;

        EditorGUILayout.LabelField("Charge timeline (hold time)", EditorStyles.boldLabel);
        var rect = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.12f));

        float max = Mathf.Max(0.5f, levels[levels.Count - 1].timeRequired * 1.2f);
        for (int i = 0; i < levels.Count; i++)
        {
            float start = levels[i].timeRequired;
            float end = i + 1 < levels.Count ? levels[i + 1].timeRequired : max;
            float x0 = rect.x + rect.width * Mathf.Clamp01(start / max);
            float x1 = rect.x + rect.width * Mathf.Clamp01(end / max);
            var segment = new Rect(x0, rect.y + 4, Mathf.Max(2, x1 - x0 - 1), rect.height - 8);
            var anim = levels[i].colorAnimation;
            Color c = anim.frames != null && anim.frames.Length > 0 ? anim.frames[0].outline : new Color(0.3f, 0.5f, 0.9f);
            c.a = 1f;
            EditorGUI.DrawRect(segment, Color.Lerp(c, Color.black, 0.35f));
            GUI.Label(segment, $" L{i}  {start:0.##}s  dmg {levels[i].damage}", EditorStyles.whiteMiniLabel);
        }
    }

    private static void DrawPreviews(ChargeShotWeapon weapon)
    {
        var levels = weapon.chargeLevels;
        if (levels == null || levels.Count == 0) return;

        // Mega Man's base colours: from the WeaponData that uses this prefab (if it's found)
        Color main = new(0f, 0.44f, 0.925f), secondary = new(0f, 0.91f, 0.847f), outline = new Color32(24, 24, 24, 255);

        EditorGUILayout.LabelField("Armour while charged", EditorStyles.boldLabel);
        var row = EditorGUILayout.GetControlRect(false, 64);
        float w = Mathf.Min(70, row.width / levels.Count);
        double t = EditorApplication.timeSinceStartup;
        for (int i = 0; i < levels.Count; i++)
        {
            var cell = new Rect(row.x + i * w, row.y, w - 4, row.height);
            EditorGUI.DrawRect(cell, new Color(0.12f, 0.12f, 0.14f));

            Color m = main, s = secondary, o = outline;
            var anim = levels[i].colorAnimation;
            if (i > 0 && anim.frames != null && anim.frames.Length > 0 && anim.frameDuration > 0f)
            {
                int frame = (int)(t / anim.frameDuration) % anim.frames.Length;
                m = anim.frames[frame].primary;
                s = anim.frames[frame].secondary;
                o = anim.frames[frame].outline;
            }
            WeaponEditorUtility.DrawArmorFigure(new Rect(cell.x, cell.y + 2, cell.width, cell.height - 16), m, s, o);
            GUI.Label(new Rect(cell.x, cell.yMax - 14, cell.width, 14), $"Level {i}", EditorStyles.centeredGreyMiniLabel);
        }
    }
}
