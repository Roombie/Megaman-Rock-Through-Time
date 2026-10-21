using UnityEngine;

/// <summary>
/// On a [SerializeReference] field (or list), shows a drop-down in the Inspector to pick which class to use,
/// e.g. the movement or hit effects of a BasicProjectile.
/// </summary>
public class SubclassSelectorAttribute : PropertyAttribute { }

/// <summary>Name and description shown in the SubclassSelector drop-down.</summary>
[System.AttributeUsage(System.AttributeTargets.Class)]
public class DisplayInMenuAttribute : System.Attribute
{
    public readonly string name;
    public readonly string tooltip;

    public DisplayInMenuAttribute(string name, string tooltip = null)
    {
        this.name = name;
        this.tooltip = tooltip;
    }
}
