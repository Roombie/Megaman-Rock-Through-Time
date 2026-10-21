using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Mega Man > Weakness Chart: every enemy prefab × every weapon in one table, like the classic damage
/// charts. Empty = the weapon's normal damage, a number = the damage it does to that enemy, 0 = immune.
/// Edits the enemies' Weaknesses lists directly.
/// </summary>
public class WeaknessChartWindow : EditorWindow
{
    private List<GameObject> enemies = new();
    private List<WeaponData> weapons = new();
    private Vector2 scroll;
    private string filter = "";

    private const float NameWidth = 170, CellWidth = 64, RowHeight = 22;

    [MenuItem("Tools/Mega Man/Weakness Chart")]
    public static void Open() => GetWindow<WeaknessChartWindow>("Weakness Chart").Refresh();

    private void OnEnable() => Refresh();
    private void OnProjectChange() => Refresh();

    private void Refresh()
    {
        enemies = AssetDatabase.FindAssets("t:Prefab")
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null && p.GetComponent<EnemyController>() != null)
            .OrderBy(p => p.name).ToList();
        weapons = AssetDatabase.FindAssets("t:WeaponData")
            .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(w => w != null).ToList();

        // Mega Man's order first
        var megaman = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponEditorUtility.MegamanPrefabPath);
        var order = megaman != null && megaman.TryGetComponent(out MegamanWeapons mw) ? mw.weapons : new List<WeaponData>();
        weapons = weapons.OrderBy(w => order.Contains(w) ? order.IndexOf(w) : 1000).ThenBy(w => w.name).ToList();
        Repaint();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            filter = EditorGUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.Label("empty = normal damage · 0 = immune", EditorStyles.miniLabel);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();
        }

        if (enemies.Count == 0 || weapons.Count == 0)
        {
            EditorGUILayout.HelpBox("No enemy prefabs (with EnemyController) or no WeaponData assets found.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // Header: weapon icons + names
        var header = GUILayoutUtility.GetRect(NameWidth + CellWidth * weapons.Count, 48);
        for (int c = 0; c < weapons.Count; c++)
        {
            var cell = new Rect(header.x + NameWidth + c * CellWidth, header.y, CellWidth - 2, header.height);
            EditorGUI.DrawRect(cell, new Color(0.1f, 0.1f, 0.12f));
            WeaponEditorUtility.DrawSprite(new Rect(cell.center.x - 12, cell.y + 2, 24, 24), weapons[c].weaponIcon);
            GUI.Label(new Rect(cell.x, cell.y + 28, cell.width, 18), weapons[c].DisplayName, EditorStyles.centeredGreyMiniLabel);
        }

        foreach (var prefab in enemies)
        {
            if (!string.IsNullOrEmpty(filter) && prefab.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            var enemy = prefab.GetComponent<EnemyController>();
            var row = GUILayoutUtility.GetRect(NameWidth + CellWidth * weapons.Count, RowHeight);

            if (GUI.Button(new Rect(row.x, row.y, NameWidth - 4, RowHeight - 2), $"{prefab.name}  ({enemy.maxHealth} HP)", EditorStyles.label))
                EditorGUIUtility.PingObject(prefab);

            for (int c = 0; c < weapons.Count; c++)
            {
                var cell = new Rect(row.x + NameWidth + c * CellWidth, row.y, CellWidth - 2, RowHeight - 2);
                int index = IndexOf(enemy, weapons[c]);
                string current = index >= 0 ? enemy.weaknesses[index].damage.ToString() : "";

                // Colour hint: immune / weak (kills in one hit) / custom
                if (index >= 0)
                {
                    int dmg = enemy.weaknesses[index].damage;
                    var tint = dmg == 0 ? new Color(0.5f, 0.5f, 0.55f, 0.35f)
                             : dmg >= enemy.maxHealth ? new Color(0.9f, 0.3f, 0.2f, 0.35f)
                             : new Color(0.3f, 0.6f, 0.9f, 0.3f);
                    EditorGUI.DrawRect(cell, tint);
                }

                EditorGUI.BeginChangeCheck();
                string edited = EditorGUI.DelayedTextField(cell, current);
                if (EditorGUI.EndChangeCheck()) Set(prefab, weapons[c], edited);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static int IndexOf(EnemyController enemy, WeaponData weapon)
    {
        if (enemy.weaknesses == null) return -1;
        for (int i = 0; i < enemy.weaknesses.Length; i++)
            if (enemy.weaknesses[i].weapon == weapon) return i;
        return -1;
    }

    private static void Set(GameObject prefab, WeaponData weapon, string value)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var enemy = root.GetComponent<EnemyController>();
            var list = (enemy.weaknesses ?? new WeaponDamage[0]).ToList();
            int index = list.FindIndex(w => w.weapon == weapon);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (index >= 0) list.RemoveAt(index);    // back to normal damage
            }
            else if (int.TryParse(value, out int damage))
            {
                var entry = new WeaponDamage { weapon = weapon, damage = Mathf.Max(0, damage) };
                if (index >= 0) list[index] = entry; else list.Add(entry);
            }
            else return;

            enemy.weaknesses = list.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
