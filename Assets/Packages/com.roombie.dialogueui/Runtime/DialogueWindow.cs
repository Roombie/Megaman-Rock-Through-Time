using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueWindow : MonoBehaviour
{
    public static DialogueWindow Instance { get; private set; }

    [SerializeField] private DialogueRunner runner;
    [SerializeField] private MonoBehaviour textResolverBehaviour;
    [Tooltip("Optional IDialogueVariableSource for {name} placeholders. Checked after the running dialogue's DialogueContext values and before the scene DialogueVariableStore.")]
    [SerializeField] private MonoBehaviour variableSourceBehaviour;
    [Header("Window")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TextAnimator dialogueTextAnimator;
    [SerializeField] private Button continueButton;
    [Header("Speaker")]
    [SerializeField] private GameObject speakerRoot;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private Image portraitImage;
    [Header("Choices")]
    [SerializeField] private GameObject choicesRoot;
    [SerializeField] private Transform choicesContent;
    [SerializeField] private DialogueChoiceButton choiceButtonPrefab;

    private readonly List<DialogueChoiceButton> choices = new List<DialogueChoiceButton>();
    private readonly List<IDialogueVariableNotifier> notifiers = new List<IDialogueVariableNotifier>();
    private bool lineRefreshPending;
    private IDialogueTextBinding lineBinding;
    private IDialogueTextBinding speakerBinding;
    private Coroutine selectionRoutine;
    private InterpolatingTextResolver textResolver;
    private IDialogueVariableSource variableSource;
    private IDialogueTextResolver Resolver => textResolverBehaviour as IDialogueTextResolver;

    /// <summary>Variable lookup used for {name} placeholders: dialogue context, then the assigned source, then DialogueVariableStore.</summary>
    public IDialogueVariableSource Variables => variableSource ??= new CompositeDialogueVariableSource(
        () => runner != null ? runner.CurrentContext : null,
        () => variableSourceBehaviour as IDialogueVariableSource,
        () => DialogueVariableStore.Instance);

    /// <summary>Text resolver used for lines, speaker names and choices, with {name} interpolation applied.</summary>
    public IDialogueTextResolver Text => textResolver ??= new InterpolatingTextResolver(Resolver, Variables);

    public event Action Opened;
    public event Action Closed;
    public bool IsOpen => root != null && root.activeSelf;
    public DialogueRunner Runner => runner;
    public TextAnimator TextAnimator => dialogueTextAnimator;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Debug.LogError("More than one DialogueWindow exists.", this); enabled = false; return; }
        Instance = this;
        if (runner == null) runner = GetComponent<DialogueRunner>();
        if (dialogueTextAnimator == null && dialogueText != null) dialogueTextAnimator = dialogueText.GetComponentInParent<TextAnimator>(true);
        if (dialogueTextAnimator != null) dialogueTextAnimator.RevealCompleted += HandleRevealCompleted;
        if (continueButton != null) continueButton.onClick.AddListener(Advance);
        BindRunner();
        if (root != null) root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
        if (dialogueTextAnimator != null) dialogueTextAnimator.RevealCompleted -= HandleRevealCompleted;
        UnbindRunner(); UnwatchVariables(); ClearBindings(); ClearChoices();
        if (Instance == this) Instance = null;
    }

    public void Open(DialogueContainer dialogue, DialogueContext context)
    {
        if (dialogue == null || runner == null || IsOpen) return;
        if (root != null) root.SetActive(true);
        ClearSpeaker();
        WatchVariables();
        Opened?.Invoke();
        runner.StartDialogue(dialogue, context);
    }

    /// <summary>
    /// Re-evaluates {name} placeholders in the visible text. The speaker name and choices update at once.
    /// The line updates now when fully revealed, otherwise when its typewriter finishes, so the reveal never restarts.
    /// Called automatically when a watched IDialogueVariableNotifier fires; call it yourself for sources without events.
    /// </summary>
    public void RefreshVariables()
    {
        if (!IsOpen) return;
        if (speakerBinding is IDialogueRefreshableBinding speaker && speaker.Refresh()) HandleSpeakerChanged(speakerBinding.Value);
        for (int i = 0; i < choices.Count; i++) if (choices[i] != null) choices[i].Refresh();
        if (lineBinding is not IDialogueRefreshableBinding) return;
        if (dialogueTextAnimator != null && dialogueTextAnimator.IsRevealing) { lineRefreshPending = true; return; }
        RefreshLineNow();
    }

    private void RefreshLineNow()
    {
        lineRefreshPending = false;
        if (lineBinding is IDialogueRefreshableBinding line && line.Refresh()) SetLine(lineBinding.Value, false);
    }
    private void HandleRevealCompleted() { if (lineRefreshPending && IsOpen) RefreshLineNow(); }
    private void HandleVariableChanged(string _) => RefreshVariables();

    private void WatchVariables()
    {
        UnwatchVariables();
        AddNotifier(variableSourceBehaviour as IDialogueVariableNotifier);
        AddNotifier(DialogueVariableStore.Instance);
    }
    private void AddNotifier(IDialogueVariableNotifier notifier)
    {
        if (notifier == null || notifiers.Contains(notifier)) return;
        notifier.Changed += HandleVariableChanged;
        notifiers.Add(notifier);
    }
    private void UnwatchVariables()
    {
        for (int i = 0; i < notifiers.Count; i++) notifiers[i].Changed -= HandleVariableChanged;
        notifiers.Clear();
    }

    public void Close()
    {
        if (!IsOpen) return;
        runner?.CancelDialogue();
        ClosePresentation();
    }

    public void Advance()
    {
        if (!IsOpen || runner == null || runner.IsWaitingForChoice) return;
        if (dialogueTextAnimator != null && dialogueTextAnimator.IsRevealing) { dialogueTextAnimator.SkipReveal(); return; }
        runner.Advance();
    }

    private void BindRunner()
    {
        if (runner == null) return;
        runner.OnShowDialogue += ShowDialogue;
        runner.OnShowChoices += ShowChoices;
        runner.OnDialogueEnd += HandleEnded;
    }
    private void UnbindRunner()
    {
        if (runner == null) return;
        runner.OnShowDialogue -= ShowDialogue;
        runner.OnShowChoices -= ShowChoices;
        runner.OnDialogueEnd -= HandleEnded;
    }

    private void ShowDialogue(DialogueLinePresentation line)
    {
        ClearChoices();
        BindLine(line != null ? line.TextKey : string.Empty);
        BindSpeaker(line);
        if (choicesRoot != null) choicesRoot.SetActive(false);
        if (continueButton != null) { continueButton.gameObject.SetActive(true); SelectNextFrame(continueButton.gameObject); }
    }

    private void ShowChoices(IReadOnlyList<DialogueChoicePresentation> values)
    {
        DisposeBinding(ref lineBinding);
        ClearSpeaker();
        if (continueButton != null) continueButton.gameObject.SetActive(false);
        ClearChoices();
        if (choicesRoot != null) choicesRoot.SetActive(true);
        if (choicesContent == null || choiceButtonPrefab == null || values == null) return;
        for (int i = 0; i < values.Count; i++)
        {
            int index = i;
            DialogueChoiceButton item = Instantiate(choiceButtonPrefab, choicesContent);
            item.Bind(values[i], Text, () => runner.SelectChoice(values[index].ChoiceIndex));
            choices.Add(item);
        }
        foreach (DialogueChoiceButton item in choices)
            if (item != null && item.Button.IsActive() && item.Button.IsInteractable()) { SelectNextFrame(item.gameObject); break; }
    }

    private void BindLine(string key)
    {
        DisposeBinding(ref lineBinding);
        lineRefreshPending = false;
        lineBinding = Text.Bind(string.Empty, key);
        SetLine(lineBinding != null ? lineBinding.Value : key, true);
        if (lineBinding != null) lineBinding.Changed += HandleLineChanged;
    }
    private void HandleLineChanged(string value) => SetLine(value, true);
    private void SetLine(string value, bool animate)
    {
        if (dialogueTextAnimator != null) dialogueTextAnimator.SetText(value ?? string.Empty, animate);
        else if (dialogueText != null) dialogueText.text = value ?? string.Empty;
    }

    private void BindSpeaker(DialogueLinePresentation line)
    {
        ClearSpeaker();
        if (line == null) return;
        speakerBinding = Text.Bind(line.SpeakerNameTable, line.SpeakerNameKey);
        string fallback = line.SpeakerNameKey ?? string.Empty;
        string value = speakerBinding != null ? speakerBinding.Value : fallback;
        bool hasName = !string.IsNullOrWhiteSpace(value);
        bool hasPortrait = line.Portrait != null && portraitImage != null;
        if (speakerRoot != null) speakerRoot.SetActive(hasName || hasPortrait);
        if (speakerLabel != null) { speakerLabel.gameObject.SetActive(hasName); speakerLabel.text = value; }
        if (speakerBinding != null) speakerBinding.Changed += HandleSpeakerChanged;
        if (portraitImage != null) { portraitImage.gameObject.SetActive(hasPortrait); portraitImage.sprite = line.Portrait; }
    }
    private void HandleSpeakerChanged(string value)
    {
        bool hasName = !string.IsNullOrWhiteSpace(value);
        if (speakerLabel != null) { speakerLabel.gameObject.SetActive(hasName); speakerLabel.text = value ?? string.Empty; }
        if (speakerRoot != null) speakerRoot.SetActive(hasName || (portraitImage != null && portraitImage.sprite != null));
    }
    private void ClearSpeaker()
    {
        DisposeBinding(ref speakerBinding);
        if (speakerRoot != null) speakerRoot.SetActive(false);
        if (speakerLabel != null) speakerLabel.text = string.Empty;
        if (portraitImage != null) { portraitImage.sprite = null; portraitImage.gameObject.SetActive(false); }
    }

    private void HandleEnded() => ClosePresentation();
    private void ClosePresentation()
    {
        if (!IsOpen) return;
        UnwatchVariables(); lineRefreshPending = false;
        ClearBindings(); ClearChoices();
        if (selectionRoutine != null) { StopCoroutine(selectionRoutine); selectionRoutine = null; }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (root != null) root.SetActive(false);
        Closed?.Invoke();
    }
    private void ClearBindings() { DisposeBinding(ref lineBinding); ClearSpeaker(); }
    private void DisposeBinding(ref IDialogueTextBinding binding)
    {
        if (binding == null) return;
        if (ReferenceEquals(binding, lineBinding)) binding.Changed -= HandleLineChanged;
        if (ReferenceEquals(binding, speakerBinding)) binding.Changed -= HandleSpeakerChanged;
        binding.Dispose(); binding = null;
    }
    private void ClearChoices()
    {
        for (int i = choices.Count - 1; i >= 0; i--) if (choices[i] != null) Destroy(choices[i].gameObject);
        choices.Clear(); if (choicesRoot != null) choicesRoot.SetActive(false);
    }
    private void SelectNextFrame(GameObject target)
    {
        if (selectionRoutine != null) StopCoroutine(selectionRoutine);
        selectionRoutine = StartCoroutine(SelectRoutine(target));
    }
    private IEnumerator SelectRoutine(GameObject target)
    {
        yield return null; selectionRoutine = null;
        if (!IsOpen || target == null || !target.activeInHierarchy || EventSystem.current == null) yield break;
        EventSystem.current.SetSelectedGameObject(null); EventSystem.current.SetSelectedGameObject(target);
    }
}
