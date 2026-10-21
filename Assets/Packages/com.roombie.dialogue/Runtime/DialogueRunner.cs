using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Project-agnostic graph player. It emits semantic presentation data (text keys, speaker,
/// choices) and delegates game-specific conditions/actions/signals through interfaces.
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    private sealed class DialogueFrame
    {
        public DialogueContainer Container;
        public string ReturnNodeGuid;
    }

    [Header("Optional default graph")]
    [SerializeField] private DialogueContainer dialogueContainer;

    [Header("Optional service components")]
    [Tooltip("MonoBehaviour implementing IDialogueConditionResolver.")]
    [SerializeField] private MonoBehaviour conditionResolverBehaviour;
    [Tooltip("MonoBehaviour implementing IDialogueBranchResolver.")]
    [SerializeField] private MonoBehaviour branchResolverBehaviour;
    [Tooltip("MonoBehaviour implementing IDialogueActionExecutor. If omitted, DialogueActor's generic fallback is used.")]
    [SerializeField] private MonoBehaviour actionExecutorBehaviour;
    [Tooltip("MonoBehaviour implementing IDialogueSignalSink.")]
    [SerializeField] private MonoBehaviour signalSinkBehaviour;

    public Action<DialogueLinePresentation> OnShowDialogue;
    public Action<IReadOnlyList<DialogueChoicePresentation>> OnShowChoices;
    public Action<DialogueSignalContext> OnSignal;
    public Action OnDialogueEnd;

    // Compatibility hooks: useful for tiny projects that do not want service components.
    public Func<string, bool> OnCheckCondition;
    public Func<List<string>, int> OnSelectBranch;

    private readonly Stack<DialogueFrame> callStack = new();
    private readonly HashSet<DialogueActor> controlledActors = new();

    private DialogueNodeData current;
    private Dictionary<string, DialogueNodeData> nodeMap;
    private Dictionary<string, List<NodeLinkData>> linkMap;
    private DialogueContext context;
    private Coroutine waitRoutine;
    private bool waitingForChoice;
    private bool waitingForAction;
    private bool waitingForWait;

    private IDialogueConditionResolver ConditionResolver => conditionResolverBehaviour as IDialogueConditionResolver;
    private IDialogueBranchResolver BranchResolver => branchResolverBehaviour as IDialogueBranchResolver;
    private IDialogueActionExecutor ActionExecutor => actionExecutorBehaviour as IDialogueActionExecutor;
    private IDialogueSignalSink SignalSink => signalSinkBehaviour as IDialogueSignalSink;

    public bool IsRunning { get; private set; }
    public bool IsWaitingForChoice => waitingForChoice;
    public bool IsBusy => waitingForChoice || waitingForAction || waitingForWait;
    public DialogueContainer CurrentContainer => dialogueContainer;
    public DialogueContext CurrentContext => context;

    public void SetActionExecutor(MonoBehaviour behaviour) => actionExecutorBehaviour = behaviour;
    public void SetConditionResolver(MonoBehaviour behaviour) => conditionResolverBehaviour = behaviour;
    public void SetBranchResolver(MonoBehaviour behaviour) => branchResolverBehaviour = behaviour;
    public void SetSignalSink(MonoBehaviour behaviour) => signalSinkBehaviour = behaviour;

    public void StartDialogue() => StartDialogue(dialogueContainer, null);
    public void StartDialogue(DialogueContainer container) => StartDialogue(container, null);

    public void StartDialogue(DialogueContainer container, DialogueContext dialogueContext)
    {
        if (container == null)
        {
            Debug.LogError("[DialogueRunner] No DialogueContainer assigned.", this);
            return;
        }

        CancelPendingWait();
        ActionExecutor?.CancelAll(context);
        callStack.Clear();
        ReleaseDialogueActors();

        context = dialogueContext ?? new DialogueContext();
        AcquireDialogueActors();
        waitingForChoice = false;
        waitingForAction = false;
        waitingForWait = false;
        IsRunning = true;

        if (!SwitchContainer(container))
        {
            StopDialogue(false);
            return;
        }

        DialogueNodeData startNode = FindStartNode();
        if (startNode == null)
        {
            Debug.LogError($"[DialogueRunner] Dialogue '{container.name}' has no Start node.", this);
            StopDialogue(false);
            return;
        }

        GoTo(startNode);
    }

    public void Advance()
    {
        if (!IsRunning || IsBusy || current == null || current.nodeType != DialogueNodeType.Dialogue)
            return;

        FollowFirst(current);
    }

    public void SelectChoice(int index)
    {
        if (!IsRunning || !waitingForChoice || current == null)
            return;

        List<NodeLinkData> links = GetLinks(current.guid);
        if (index < 0 || index >= current.choiceIds.Count)
        {
            Debug.LogWarning($"[DialogueRunner] Choice index {index} out of range.", this);
            return;
        }

        NodeLinkData selected = null;
        if (current.choicePortIds != null && index < current.choicePortIds.Count)
        {
            string portId = current.choicePortIds[index];
            selected = links.FirstOrDefault(l => l.portName == portId);
        }

        if (selected == null && index < links.Count)
            selected = links[index];

        waitingForChoice = false;
        if (selected != null)
            GoTo(GetNode(selected.targetNodeGuid));
        else
            FinishCurrentGraph();
    }

    public void CancelDialogue() => StopDialogue(true);

    private void GoTo(DialogueNodeData node)
    {
        if (!IsRunning)
            return;

        if (node == null)
        {
            Debug.LogWarning("[DialogueRunner] Reached a null dialogue node.", this);
            StopDialogue(true);
            return;
        }

        current = node;

        switch (node.nodeType)
        {
            case DialogueNodeType.Start:
                FollowFirst(node);
                break;
            case DialogueNodeType.End:
                FinishCurrentGraph();
                break;
            case DialogueNodeType.Dialogue:
                ShowDialogue(node);
                break;
            case DialogueNodeType.Choice:
                waitingForChoice = true;
                OnShowChoices?.Invoke(
                    node.choiceIds.Select((key, index) =>
                        new DialogueChoicePresentation(key, index)).ToList());
                break;
            case DialogueNodeType.Condition:
                FollowTrueFalse(node, EvaluateCondition(node.conditionKey));
                break;
            case DialogueNodeType.Branch:
                FollowBranch(node);
                break;
            case DialogueNodeType.Action:
                ExecuteAction(node);
                break;
            case DialogueNodeType.Wait:
                BeginWait(node);
                break;
            case DialogueNodeType.Signal:
                RaiseSignal(node);
                FollowFirst(node);
                break;
            case DialogueNodeType.SubDialogue:
                EnterSubDialogue(node);
                break;
            default:
                FollowFirst(node);
                break;
        }
    }

    private void ShowDialogue(DialogueNodeData node)
    {
        DialogueActor speaker = context?.Resolve(node.speakerRole);
        DialogueCharacterDefinition character = context?.ResolveCharacter(node.speakerRole);
        OnShowDialogue?.Invoke(new DialogueLinePresentation(
            node.textId,
            speaker,
            character,
            node.speakerRole,
            node.expressionId));
    }

    private bool EvaluateCondition(string key)
    {
        if (ConditionResolver != null)
            return ConditionResolver.Evaluate(key, context);

        if (OnCheckCondition != null)
            return OnCheckCondition(key);

        return DialogueFlagStore.Instance != null && DialogueFlagStore.Instance.GetFlag(key);
    }

    private void FollowBranch(DialogueNodeData node)
    {
        List<NodeLinkData> links = GetLinks(node.guid);
        int chosen = BranchResolver != null
            ? BranchResolver.SelectBranch(node.branchLabels, context)
            : OnSelectBranch?.Invoke(node.branchLabels) ?? 0;

        if (chosen < 0 || chosen >= node.branchLabels.Count)
        {
            Debug.LogWarning(
                $"[DialogueRunner] Branch node selected index {chosen}, but only {node.branchLabels.Count} branch(es) exist.",
                this);
            StopDialogue(true);
            return;
        }

        NodeLinkData selected = null;
        if (node.branchPortIds != null && chosen < node.branchPortIds.Count)
        {
            string portId = node.branchPortIds[chosen];
            selected = links.FirstOrDefault(l => l.portName == portId);
        }

        if (selected == null && chosen < links.Count)
            selected = links[chosen];

        if (selected != null)
            GoTo(GetNode(selected.targetNodeGuid));
        else
            FinishCurrentGraph();
    }

    private void ExecuteAction(DialogueNodeData node)
    {
        DialogueActor actor = context?.Resolve(node.targetActorRole);
        if (actor == null)
        {
            Debug.LogWarning(
                $"[DialogueRunner] Action '{node.actionType}' could not resolve actor role '{node.targetActorRole}'. Skipping.",
                this);
            FollowFirst(node);
            return;
        }

        var request = new DialogueActionRequest
        {
            Type = node.actionType,
            SecondaryActor = context?.Resolve(node.secondaryActorRole),
            Vector = node.actionVector,
            Duration = Mathf.Max(0f, node.actionDuration),
            StringValue = node.actionString,
            NumberValue = node.actionNumber,
            BoolValue = node.actionBool,
        };

        Action done = null;
        if (node.waitForAction)
        {
            waitingForAction = true;
            string nodeGuid = node.guid;
            done = () =>
            {
                if (!IsRunning || current == null || current.guid != nodeGuid)
                    return;

                waitingForAction = false;
                FollowFirst(current);
            };
        }

        if (ActionExecutor != null)
            ActionExecutor.Execute(actor, request, context, done);
        else
            actor.ExecuteDefaultAction(request, done);

        if (!node.waitForAction)
            FollowFirst(node);
    }

    private void BeginWait(DialogueNodeData node)
    {
        CancelPendingWait();
        waitingForWait = true;
        waitRoutine = StartCoroutine(WaitThenContinue(node.guid, Mathf.Max(0f, node.waitDuration)));
    }

    private IEnumerator WaitThenContinue(string nodeGuid, float duration)
    {
        if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
        else yield return null;

        waitRoutine = null;
        if (!IsRunning || current == null || current.guid != nodeGuid)
            yield break;

        waitingForWait = false;
        FollowFirst(current);
    }

    private void RaiseSignal(DialogueNodeData node)
    {
        var signal = new DialogueSignalContext(node.signalId, node.signalPayload, context);
        OnSignal?.Invoke(signal);
        SignalSink?.Raise(signal);
        DialogueSignalBus.Raise(node.signalId, node.signalPayload, context);
    }

    private void EnterSubDialogue(DialogueNodeData node)
    {
        if (node.subDialogue == null)
        {
            Debug.LogWarning("[DialogueRunner] SubDialogue node has no graph assigned. Skipping.", this);
            FollowFirst(node);
            return;
        }

        NodeLinkData returnLink = GetLinks(node.guid).FirstOrDefault();
        callStack.Push(new DialogueFrame
        {
            Container = dialogueContainer,
            ReturnNodeGuid = returnLink?.targetNodeGuid,
        });

        if (!SwitchContainer(node.subDialogue))
        {
            StopDialogue(true);
            return;
        }

        DialogueNodeData start = FindStartNode();
        if (start == null)
        {
            Debug.LogError($"[DialogueRunner] Sub-dialogue '{node.subDialogue.name}' has no Start node.", this);
            StopDialogue(true);
            return;
        }

        GoTo(start);
    }

    private void FinishCurrentGraph()
    {
        if (callStack.Count == 0)
        {
            StopDialogue(true);
            return;
        }

        DialogueFrame frame = callStack.Pop();
        if (!SwitchContainer(frame.Container))
        {
            StopDialogue(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(frame.ReturnNodeGuid))
        {
            FinishCurrentGraph();
            return;
        }

        GoTo(GetNode(frame.ReturnNodeGuid));
    }

    private void FollowFirst(DialogueNodeData node)
    {
        NodeLinkData link = GetLinks(node.guid).FirstOrDefault();
        if (link == null) { FinishCurrentGraph(); return; }
        GoTo(GetNode(link.targetNodeGuid));
    }

    private void FollowTrueFalse(DialogueNodeData node, bool result)
    {
        List<NodeLinkData> links = GetLinks(node.guid);
        NodeLinkData link = result
            ? links.FirstOrDefault(l => l.portName == "True")
            : links.FirstOrDefault(l => l.portName == "False");

        if (link != null) GoTo(GetNode(link.targetNodeGuid));
        else FinishCurrentGraph();
    }

    private bool SwitchContainer(DialogueContainer container)
    {
        if (container == null) return false;
        dialogueContainer = container;
        BuildMaps();
        return true;
    }

    private DialogueNodeData FindStartNode() =>
        dialogueContainer != null
            ? dialogueContainer.DialogueNodeData.FirstOrDefault(n => n.nodeType == DialogueNodeType.Start)
            : null;

    private void StopDialogue(bool invokeEnd)
    {
        bool wasRunning = IsRunning;
        IsRunning = false;
        waitingForChoice = false;
        waitingForAction = false;
        waitingForWait = false;
        current = null;
        callStack.Clear();
        CancelPendingWait();
        ActionExecutor?.CancelAll(context);
        ReleaseDialogueActors();

        if (invokeEnd && wasRunning)
            OnDialogueEnd?.Invoke();
    }

    private void AcquireDialogueActors()
    {
        if (context == null) return;
        foreach (DialogueActor actor in context.Actors)
        {
            if (actor == null || !controlledActors.Add(actor)) continue;
            actor.AcquireDialogueControl();
        }
    }

    private void ReleaseDialogueActors()
    {
        foreach (DialogueActor actor in controlledActors)
            if (actor != null)
                actor.ReleaseDialogueControl();
        controlledActors.Clear();
    }

    private void CancelPendingWait()
    {
        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }
        waitingForWait = false;
    }

    private void BuildMaps()
    {
        nodeMap = dialogueContainer.DialogueNodeData
            .Where(n => n != null && !string.IsNullOrWhiteSpace(n.guid))
            .GroupBy(n => n.guid)
            .ToDictionary(g => g.Key, g => g.First());

        linkMap = new Dictionary<string, List<NodeLinkData>>();
        foreach (NodeLinkData link in dialogueContainer.NodeLinks)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.baseNodeGuid)) continue;
            if (!linkMap.TryGetValue(link.baseNodeGuid, out List<NodeLinkData> links))
            {
                links = new List<NodeLinkData>();
                linkMap[link.baseNodeGuid] = links;
            }
            links.Add(link);
        }
    }

    private List<NodeLinkData> GetLinks(string guid) =>
        linkMap != null && guid != null && linkMap.TryGetValue(guid, out List<NodeLinkData> links)
            ? links
            : new List<NodeLinkData>();

    private DialogueNodeData GetNode(string guid) =>
        nodeMap != null && guid != null && nodeMap.TryGetValue(guid, out DialogueNodeData node)
            ? node
            : null;
}
