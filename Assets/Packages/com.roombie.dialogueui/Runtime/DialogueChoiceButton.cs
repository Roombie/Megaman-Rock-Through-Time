using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class DialogueChoiceButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    private Button button;
    private IDialogueTextBinding textBinding;
    private Action onClick;

    public Button Button => button != null ? button : button = GetComponent<Button>();

    private void Awake() { button = GetComponent<Button>(); button.onClick.AddListener(HandleClick); }
    private void OnDestroy() { Clear(); if (button != null) button.onClick.RemoveListener(HandleClick); }

    public void Bind(DialogueChoicePresentation choice, IDialogueTextResolver resolver, Action callback)
    {
        Clear();
        onClick = callback;
        Button.interactable = choice == null || choice.Enabled;
        string key = choice != null ? choice.TextKey : string.Empty;
        textBinding = resolver?.Bind(string.Empty, key);
        if (label != null) label.text = textBinding != null ? textBinding.Value : key;
        if (textBinding != null) textBinding.Changed += SetLabel;
    }

    /// <summary>Re-evaluates {name} placeholders in the label. Choices have no typewriter, so the change applies at once.</summary>
    public void Refresh()
    {
        if (textBinding is IDialogueRefreshableBinding refreshable && refreshable.Refresh()) SetLabel(textBinding.Value);
    }

    private void SetLabel(string value) { if (label != null) label.text = value ?? string.Empty; }
    private void HandleClick() => onClick?.Invoke();
    private void Clear()
    {
        if (textBinding != null) { textBinding.Changed -= SetLabel; textBinding.Dispose(); }
        textBinding = null; onClick = null;
    }
}
