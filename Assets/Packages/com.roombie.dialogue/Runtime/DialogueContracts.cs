using System;
using System.Collections.Generic;

public interface IDialogueLocalizationProvider
{
    string Resolve(string key);
    event Action LocaleChanged;
}

public interface IDialogueVariableResolver
{
    bool TryResolve(string variableName, DialogueContext context, out object value);
}

public interface IDialogueConditionResolver
{
    bool Evaluate(string conditionKey, DialogueContext context);
}

public interface IDialogueBranchResolver
{
    int SelectBranch(IReadOnlyList<string> labels, DialogueContext context);
}

public interface IDialogueActionExecutor
{
    void Execute(DialogueActor actor, DialogueActionRequest request, DialogueContext context, Action onComplete);
    void CancelAll(DialogueContext context);
}

public interface IDialogueSignalSink
{
    void Raise(DialogueSignalContext signal);
}

/// <summary>
/// Optional UI boundary. Projects can ignore this and subscribe directly to DialogueRunner events.
/// </summary>
public interface IDialoguePresenter
{
    void PresentLine(DialogueLinePresentation line);
    void PresentChoices(IReadOnlyList<DialogueChoicePresentation> choices);
    void DialogueEnded();
}
