using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drop-down for [SerializeReference, SubclassSelector] fields: pick which class goes in the field
/// (e.g. a projectile's movement or one of its hit effects), then edit its settings below.
/// </summary>
[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, Type[]> typeCache = new();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
            return EditorGUIUtility.singleLineHeight;
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.LabelField(position, label.text, "Use [SubclassSelector] with [SerializeReference]");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        // Type picker on the header line, right of the label
        var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var buttonRect = new Rect(header.x + EditorGUIUtility.labelWidth + 2, header.y,
                                  header.width - EditorGUIUtility.labelWidth - 2, header.height);

        Type current = property.managedReferenceValue?.GetType();
        var content = new GUIContent(current != null ? DisplayName(current) : "(None)", current != null ? Tooltip(current) : null);
        if (EditorGUI.DropdownButton(buttonRect, content, FocusType.Keyboard))
            ShowMenu(property, current);

        // The fields of the picked class (foldout)
        string title = current != null ? label.text : $"{label.text}";
        EditorGUI.PropertyField(position, property, new GUIContent(title, label.tooltip), true);

        EditorGUI.EndProperty();
    }

    private void ShowMenu(SerializedProperty property, Type current)
    {
        var baseType = BaseType(property);
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("(None)"), current == null, () => Assign(property, null));
        menu.AddSeparator("");
        foreach (var type in Candidates(baseType))
        {
            var t = type;
            menu.AddItem(new GUIContent(DisplayName(t)), t == current, () => Assign(property, t));
        }
        menu.ShowAsContext();
    }

    private static void Assign(SerializedProperty property, Type type)
    {
        var targets = property.serializedObject.targetObjects;
        foreach (var target in targets)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property.propertyPath);
            p.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            p.isExpanded = true;
            so.ApplyModifiedProperties();
        }
    }

    private static Type BaseType(SerializedProperty property)
    {
        // "Assembly-CSharp ProjectileMovement" (for list elements it's the element type)
        string[] parts = property.managedReferenceFieldTypename.Split(' ');
        if (parts.Length != 2) return typeof(object);
        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == parts[0]);
        return asm?.GetType(parts[1]) ?? typeof(object);
    }

    private static Type[] Candidates(Type baseType)
    {
        string key = baseType.AssemblyQualifiedName ?? baseType.Name;
        if (typeCache.TryGetValue(key, out var types)) return types;

        types = TypeCache.GetTypesDerivedFrom(baseType)
            .Where(t => !t.IsAbstract && !t.IsGenericType && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) != null
                        && t.GetCustomAttribute<SerializableAttribute>() != null)
            .OrderBy(DisplayName)
            .ToArray();
        typeCache[key] = types;
        return types;
    }

    private static string DisplayName(Type t) =>
        t.GetCustomAttribute<DisplayInMenuAttribute>()?.name ?? ObjectNames.NicifyVariableName(t.Name);

    private static string Tooltip(Type t) => t.GetCustomAttribute<DisplayInMenuAttribute>()?.tooltip;
}
