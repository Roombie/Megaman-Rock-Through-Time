using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-agnostic dialogue character data.
/// DisplayNameKey is resolved by the presentation/localization layer.
/// </summary>
[CreateAssetMenu(menuName = "Roombie/Dialogue/Character Definition")]
public class DialogueCharacterDefinition : ScriptableObject
{
    [Serializable]
    private sealed class PortraitExpression
    {
        public string id;
        public Sprite portrait;
    }

    [SerializeField] private string displayNameTable;
    [SerializeField] private string displayNameKey;
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private List<PortraitExpression> expressions = new();

    public string DisplayNameTable => displayNameTable?.Trim() ?? string.Empty;
    public string DisplayNameKey => displayNameKey?.Trim() ?? string.Empty;

    public Sprite GetPortrait(string expressionId)
    {
        if (!string.IsNullOrWhiteSpace(expressionId))
        {
            for (int i = 0; i < expressions.Count; i++)
            {
                PortraitExpression expression = expressions[i];
                if (expression != null &&
                    string.Equals(expression.id, expressionId, StringComparison.OrdinalIgnoreCase))
                {
                    return expression.portrait != null ? expression.portrait : defaultPortrait;
                }
            }
        }

        return defaultPortrait;
    }
}
