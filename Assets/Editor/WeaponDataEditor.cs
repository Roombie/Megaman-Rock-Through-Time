using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// WeaponData Inspector: a preview card (icon, name, palette, energy bar), the weapon's fire mode edited right
/// here, and warnings for anything missing.
/// </summary>
[CustomEditor(typeof(WeaponData))]
public class WeaponDataEditor : Editor
{
    private Editor fireModeEditor;
    private bool showFireMode = true;

    private void OnDisable()
    {
        if (fireModeEditor != null) DestroyImmediate(fireModeEditor);
    }

    public override void OnInspectorGUI()
    {
        var data = (WeaponData)target;
        serializedObject.Update();

        DrawCard(data);
        EditorGUILayout.Space(4);
        DrawWarnings(data);

        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        DrawFireMode(data);

        if (Application.isPlaying) DrawRuntime(data);
    }

    #region Card
    private static void DrawCard(WeaponData data)
    {
        var rect = EditorGUILayout.GetControlRect(false, 64);
        EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.1f));

        // Icon (pixel art, nearest)
        var iconRect = new Rect(rect.x + 8, rect.y + 8, 48, 48);
        WeaponEditorUtility.DrawSprite(iconRect, data.weaponIcon);

        // Name
        var nameRect = new Rect(iconRect.xMax + 10, rect.y + 6, rect.width - 200, 20);
        EditorGUI.LabelField(nameRect, data.DisplayName.ToUpperInvariant(), WeaponEditorUtility.CardTitle);
        string mode = WeaponEditorUtility.FireModeName(data.weaponPrefab);
        EditorGUI.LabelField(new Rect(nameRect.x, nameRect.yMax, nameRect.width, 16), mode, WeaponEditorUtility.CardSubtitle);

        // Palette: Mega Man's armour with this weapon
        var paletteRect = new Rect(nameRect.x, rect.y + 44, 60, 14);
        WeaponEditorUtility.DrawPalette(paletteRect, data.primaryColor, data.secondaryColor, new Color32(24, 24, 24, 255));

        // Energy bar (vertical, NES style)
        int energy = Application.isPlaying ? data.CurrentEnergy : data.maxEnergy;
        var barRect = new Rect(rect.xMax - 24, rect.y + 6, 12, 52);
        WeaponEditorUtility.DrawEnergyBar(barRect, data.usesHealthAsEnergy ? 1f : energy / (float)Mathf.Max(1, data.maxEnergy), data.maxEnergy);
        if (data.usesHealthAsEnergy)
            EditorGUI.LabelField(new Rect(barRect.x - 70, rect.yMax - 18, 66, 16), "uses health", WeaponEditorUtility.CardSubtitleRight);
        else
            EditorGUI.LabelField(new Rect(barRect.x - 70, rect.yMax - 18, 66, 16), $"{data.energyCost}/shot", WeaponEditorUtility.CardSubtitleRight);
    }
    #endregion

    #region Warnings
    private static void DrawWarnings(WeaponData data)
    {
        foreach (var (message, type) in WeaponEditorUtility.Validate(data))
            EditorGUILayout.HelpBox(message, type);
    }
    #endregion

    #region Fire mode
    private void DrawFireMode(WeaponData data)
    {
        var fireMode = data.weaponPrefab != null ? data.weaponPrefab.GetComponent<WeaponBase>() : null;

        using (new EditorGUILayout.HorizontalScope())
        {
            showFireMode = EditorGUILayout.Foldout(showFireMode, fireMode != null ? $"Fire mode: {ObjectNames.NicifyVariableName(fireMode.GetType().Name)}" : "Fire mode", true, EditorStyles.foldoutHeader);
            if (data.weaponPrefab != null && GUILayout.Button("Open Prefab", EditorStyles.miniButton, GUILayout.Width(90)))
                AssetDatabase.OpenAsset(data.weaponPrefab);
        }
        if (!showFireMode) return;

        if (fireMode == null)
        {
            EditorGUILayout.HelpBox("Pick how this weapon fires. This creates its weapon prefab next to the other weapons.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var type in WeaponEditorUtility.FireModeTypes())
                {
                    if (GUILayout.Button(ObjectNames.NicifyVariableName(type.Name).Replace(" Weapon", "")))
                    {
                        data.weaponPrefab = WeaponEditorUtility.CreateWeaponPrefab(data.DisplayName, type);
                        EditorUtility.SetDirty(data);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            return;
        }

        // The fire mode's own Inspector, embedded (edits the prefab)
        if (fireModeEditor == null || fireModeEditor.target != fireMode)
        {
            if (fireModeEditor != null) DestroyImmediate(fireModeEditor);
            fireModeEditor = CreateEditor(fireMode);
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            fireModeEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(fireMode);
                PrefabUtility.SavePrefabAsset(data.weaponPrefab);
            }
        }
    }
    #endregion

    #region Play mode
    private static void DrawRuntime(WeaponData data)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Energy {data.CurrentEnergy}/{data.maxEnergy}");
            if (GUILayout.Button("Refill")) data.CurrentEnergy = data.maxEnergy;
            if (GUILayout.Button("Empty")) data.CurrentEnergy = 0;
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
    #endregion
}
