using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueLinePresentation
{
    public string TextKey { get; }
    public DialogueActor Speaker { get; }
    public DialogueCharacterDefinition Character { get; }
    public string SpeakerNameTable => Character != null
        ? Character.DisplayNameTable
        : string.Empty;
    public string SpeakerNameKey => Character != null
        ? Character.DisplayNameKey
        : string.Empty;
    public Sprite Portrait { get; }
    public string SpeakerRole { get; }
    public string ExpressionId { get; }

    public DialogueLinePresentation(
        string textKey,
        DialogueActor speaker,
        DialogueCharacterDefinition character,
        string speakerRole,
        string expressionId)
    {
        TextKey = textKey?.Trim() ?? string.Empty;
        Speaker = speaker;
        Character = character != null ? character : speaker != null ? speaker.Character : null;
        SpeakerRole = speakerRole?.Trim() ?? string.Empty;
        ExpressionId = expressionId?.Trim() ?? string.Empty;
        Portrait = Character != null ? Character.GetPortrait(ExpressionId) : null;
    }

    public DialogueLinePresentation(
        string textKey,
        DialogueActor speaker,
        string speakerRole,
        string expressionId)
        : this(textKey, speaker, speaker != null ? speaker.Character : null, speakerRole, expressionId)
    {
    }
}

public sealed class DialogueChoicePresentation
{
    public string TextKey { get; }
    public int ChoiceIndex { get; }
    public bool Enabled { get; }
    public string UnavailableReasonKey { get; }

    public DialogueChoicePresentation(string textKey, int choiceIndex, bool enabled = true, string unavailableReasonKey = "")
    {
        TextKey = textKey?.Trim() ?? string.Empty;
        ChoiceIndex = choiceIndex;
        Enabled = enabled;
        UnavailableReasonKey = unavailableReasonKey?.Trim() ?? string.Empty;
    }
}

public sealed class DialogueActionRequest
{
    public DialogueActionType Type;
    public DialogueActor SecondaryActor;
    public Vector2 Vector;
    public float Duration;
    public string StringValue;
    public float NumberValue;
    public bool BoolValue;
}

public sealed class DialogueContext : IDialogueVariableSource
{
    private readonly Dictionary<string, DialogueActor> actors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DialogueCharacterDefinition> characters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> values =
        new(StringComparer.OrdinalIgnoreCase);

    public object UserData { get; set; }
    public IEnumerable<DialogueActor> Actors => actors.Values;

    public void Bind(string role, DialogueActor actor)
    {
        if (string.IsNullOrWhiteSpace(role) || actor == null)
            return;

        string normalizedRole = role.Trim();
        actors[normalizedRole] = actor;

        if (actor.Character != null)
            characters[normalizedRole] = actor.Character;
    }

    public void BindCharacter(string role, DialogueCharacterDefinition character)
    {
        if (string.IsNullOrWhiteSpace(role) || character == null)
            return;

        characters[role.Trim()] = character;
    }

    public DialogueActor Resolve(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        actors.TryGetValue(role.Trim(), out DialogueActor actor);
        return actor;
    }

    public DialogueCharacterDefinition ResolveCharacter(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        string normalizedRole = role.Trim();
        if (characters.TryGetValue(normalizedRole, out DialogueCharacterDefinition character))
            return character;

        return actors.TryGetValue(normalizedRole, out DialogueActor actor) && actor != null
            ? actor.Character
            : null;
    }

    public void SetValue(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        values[key.Trim()] = value;
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key) ||
            !values.TryGetValue(key.Trim(), out object boxed) ||
            boxed is not T typed)
            return false;

        value = typed;
        return true;
    }

    /// <summary>Exposes values set through SetValue to {name} placeholders in dialogue text.</summary>
    public bool TryGetVariable(string name, out string value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(name) || !values.TryGetValue(name.Trim(), out object boxed))
            return false;

        value = DialogueVariableText.From(boxed);
        return true;
    }
}

[Serializable]
public sealed class DialogueCastBinding
{
    [Tooltip("Role used by graph nodes, for example: guard, mayor, friend.")]
    public string role;

    [Tooltip("Presentation identity for this role. Can be assigned without a physical actor for signs, letters, narrators or off-screen speakers.")]
    public DialogueCharacterDefinition character;

    [Tooltip("Optional direct scene actor. Assign this only when the participant physically exists and can be moved/animated by dialogue actions.")]
    public DialogueActor actor;

    [Tooltip("Optional global DialogueActor ID fallback when a direct reference is not practical.")]
    public string actorId;

    public DialogueActor ResolveActor()
    {
        if (actor != null)
            return actor;

        return DialogueActor.FindById(actorId);
    }

    public DialogueCharacterDefinition ResolveCharacter()
    {
        if (character != null)
            return character;

        DialogueActor resolvedActor = ResolveActor();
        return resolvedActor != null ? resolvedActor.Character : null;
    }

    // Backwards-compatible alias for projects already calling Resolve().
    public DialogueActor Resolve() => ResolveActor();
}
