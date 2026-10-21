using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueNode : Node
{
    public string GUID;
    public DialogueNodeType NodeType;

    private Label previewLabel;
    private TextField textIdField;
    private TextField speakerRoleField;
    private TextField expressionIdField;
    private TextField conditionKeyField;
    private TextField signalIdField;
    private TextField signalPayloadField;
    private FloatField waitDurationField;
    private ObjectField subDialogueField;

    private TextField targetActorRoleField;
    private EnumField actionTypeField;
    private VisualElement actionSettingsContainer;
    private TextField secondaryActorRoleField;
    private Vector2Field actionVectorField;
    private FloatField actionDurationField;
    private TextField actionStringField;
    private FloatField actionNumberField;
    private Toggle actionBoolField;
    private Toggle waitForActionField;

    private string pendingTextId = "";
    private string pendingSpeakerRole = "";
    private string pendingExpressionId = "";
    private string pendingConditionKey = "";
    private string pendingSignalId = "";
    private string pendingSignalPayload = "";
    private float pendingWaitDuration = 0.5f;
    private DialogueContainer pendingSubDialogue;

    private string pendingTargetActorRole = "self";
    private DialogueActionType pendingActionType = DialogueActionType.Shake;
    private string pendingSecondaryActorRole = "";
    private Vector2 pendingActionVector = new(0.08f, 0.04f);
    private float pendingActionDuration = 0.35f;
    private string pendingActionString = "";
    private float pendingActionNumber;
    private bool pendingActionBool = true;
    private bool pendingWaitForAction = true;

    private string CurrentLanguage => DialogueEditorServices.LanguageCode();

    public DialogueNode()
    {
        RegisterCallback<DetachFromPanelEvent>(_ => UnsubscribeLocale());
    }

    public void Initialize(DialogueNodeType nodeType)
    {
        NodeType = nodeType;
        title = GetNodeTitle(nodeType);

        switch (nodeType)
        {
            case DialogueNodeType.Start:
                SetTitleColor(new Color(0.15f, 0.15f, 0.15f));
                AddPort(Direction.Output, Port.Capacity.Single, "Next", outputContainer);
                break;
            case DialogueNodeType.End:
                SetTitleColor(new Color(0.15f, 0.15f, 0.15f));
                AddPort(Direction.Input, Port.Capacity.Multi, "Previous", inputContainer);
                break;
            case DialogueNodeType.Dialogue:
                SetTitleColor(new Color(0.18f, 0.50f, 0.18f));
                AddFlowPorts();
                break;
            case DialogueNodeType.Choice:
                SetTitleColor(new Color(0.20f, 0.38f, 0.60f));
                AddPort(Direction.Input, Port.Capacity.Multi, "Input", inputContainer);
                break;
            case DialogueNodeType.Condition:
                SetTitleColor(new Color(0.65f, 0.35f, 0.10f));
                AddPort(Direction.Input, Port.Capacity.Multi, "Input", inputContainer);
                AddPort(Direction.Output, Port.Capacity.Single, "True", outputContainer);
                AddPort(Direction.Output, Port.Capacity.Single, "False", outputContainer);
                break;
            case DialogueNodeType.Branch:
                SetTitleColor(new Color(0.50f, 0.18f, 0.50f));
                AddPort(Direction.Input, Port.Capacity.Multi, "Input", inputContainer);
                break;
            case DialogueNodeType.Action:
                SetTitleColor(new Color(0.65f, 0.28f, 0.18f));
                AddFlowPorts();
                break;
            case DialogueNodeType.Wait:
                SetTitleColor(new Color(0.20f, 0.45f, 0.50f));
                AddFlowPorts();
                break;
            case DialogueNodeType.Signal:
                SetTitleColor(new Color(0.55f, 0.45f, 0.12f));
                AddFlowPorts();
                break;
            case DialogueNodeType.SubDialogue:
                SetTitleColor(new Color(0.35f, 0.25f, 0.62f));
                AddFlowPorts();
                break;
        }

        RefreshPorts();
        RefreshExpandedState();
        schedule.Execute(BuildNodeUI);
    }

    private void AddFlowPorts()
    {
        AddPort(Direction.Input, Port.Capacity.Multi, "Previous", inputContainer);
        AddPort(Direction.Output, Port.Capacity.Single, "Next", outputContainer);
    }

    private void BuildNodeUI()
    {
        switch (NodeType)
        {
            case DialogueNodeType.Dialogue:
                BuildDialogueUI();
                break;
            case DialogueNodeType.Choice:
                titleButtonContainer.Add(new Button(() => AddChoicePort("choice_id")) { text = "+ Choice" });
                break;
            case DialogueNodeType.Condition:
                conditionKeyField = BuildTextField("Flag / Variable", pendingConditionKey);
                break;
            case DialogueNodeType.Branch:
                titleButtonContainer.Add(new Button(() => AddBranchPort("Branch")) { text = "+ Branch" });
                break;
            case DialogueNodeType.Action:
                BuildActionUI();
                break;
            case DialogueNodeType.Wait:
                waitDurationField = BuildFloatField("Duration (seconds)", pendingWaitDuration);
                break;
            case DialogueNodeType.Signal:
                signalIdField = BuildTextField("Signal ID", pendingSignalId);
                signalPayloadField = BuildTextField("Payload (optional)", pendingSignalPayload);
                break;
            case DialogueNodeType.SubDialogue:
                subDialogueField = BuildObjectField<DialogueContainer>("Dialogue Graph", pendingSubDialogue);
                break;
        }

        SubscribeLocale();
        RefreshExpandedState();
    }

    private void BuildDialogueUI()
    {
        speakerRoleField = BuildTextField("Speaker Role", pendingSpeakerRole);
        expressionIdField = BuildTextField("Expression", pendingExpressionId);

        textIdField = new TextField { multiline = false };
        textIdField.SetValueWithoutNotify(pendingTextId);
        textIdField.RegisterValueChangedCallback(_ => UpdatePreview());
        mainContainer.Add(Wrap("Text ID", textIdField));

        previewLabel = new Label("—")
        {
            style =
            {
                marginTop = 4,
                paddingLeft = 4,
                paddingRight = 4,
                whiteSpace = WhiteSpace.Normal,
                backgroundColor = new Color(0.18f, 0.50f, 0.18f, 0.35f)
            }
        };
        mainContainer.Add(previewLabel);
        UpdatePreview();
    }

    private void BuildActionUI()
    {
        targetActorRoleField = BuildTextField("Target Actor Role", pendingTargetActorRole);

        actionTypeField = new EnumField(pendingActionType);
        actionTypeField.RegisterValueChangedCallback(evt =>
        {
            CaptureActionValues();
            pendingActionType = (DialogueActionType)evt.newValue;
            BuildActionSettings();
        });
        mainContainer.Add(Wrap("Action", actionTypeField));

        actionSettingsContainer = new VisualElement();
        actionSettingsContainer.style.flexDirection = FlexDirection.Column;
        mainContainer.Add(actionSettingsContainer);

        waitForActionField = new Toggle("Wait for completion") { value = pendingWaitForAction };
        mainContainer.Add(waitForActionField);

        BuildActionSettings();
    }

    private void BuildActionSettings()
    {
        if (actionSettingsContainer == null)
            return;

        CaptureActionValues();
        actionSettingsContainer.Clear();
        secondaryActorRoleField = null;
        actionVectorField = null;
        actionDurationField = null;
        actionStringField = null;
        actionNumberField = null;
        actionBoolField = null;

        DialogueActionType type = GetActionType();
        switch (type)
        {
            case DialogueActionType.Shake:
                actionVectorField = BuildVector2Field("Strength", pendingActionVector, actionSettingsContainer);
                actionDurationField = BuildFloatField("Duration", pendingActionDuration, actionSettingsContainer);
                break;
            case DialogueActionType.MoveBy:
                actionVectorField = BuildVector2Field("Offset", pendingActionVector, actionSettingsContainer);
                actionDurationField = BuildFloatField("Duration", pendingActionDuration, actionSettingsContainer);
                break;
            case DialogueActionType.MoveTo:
                actionVectorField = BuildVector2Field("World Position", pendingActionVector, actionSettingsContainer);
                actionDurationField = BuildFloatField("Duration", pendingActionDuration, actionSettingsContainer);
                break;
            case DialogueActionType.MoveToActor:
                secondaryActorRoleField = BuildTextField("Destination Actor Role", pendingSecondaryActorRole, actionSettingsContainer);
                actionVectorField = BuildVector2Field("Offset", pendingActionVector, actionSettingsContainer);
                actionDurationField = BuildFloatField("Duration", pendingActionDuration, actionSettingsContainer);
                break;
            case DialogueActionType.Teleport:
                actionVectorField = BuildVector2Field("World Position", pendingActionVector, actionSettingsContainer);
                break;
            case DialogueActionType.FaceDirection:
                actionVectorField = BuildVector2Field("Direction", pendingActionVector, actionSettingsContainer);
                break;
            case DialogueActionType.FaceActor:
                secondaryActorRoleField = BuildTextField("Face Actor Role", pendingSecondaryActorRole, actionSettingsContainer);
                break;
            case DialogueActionType.PlayAnimation:
                actionStringField = BuildTextField("Animator State", pendingActionString, actionSettingsContainer);
                actionDurationField = BuildFloatField("Wait Duration", pendingActionDuration, actionSettingsContainer);
                break;
            case DialogueActionType.AnimatorTrigger:
                actionStringField = BuildTextField("Trigger Parameter", pendingActionString, actionSettingsContainer);
                actionDurationField = BuildFloatField("Wait Duration", pendingActionDuration, actionSettingsContainer);
                break;
            case DialogueActionType.AnimatorBool:
                actionStringField = BuildTextField("Bool Parameter", pendingActionString, actionSettingsContainer);
                actionBoolField = BuildToggle("Value", pendingActionBool, actionSettingsContainer);
                break;
            case DialogueActionType.AnimatorFloat:
                actionStringField = BuildTextField("Float Parameter", pendingActionString, actionSettingsContainer);
                actionNumberField = BuildFloatField("Value", pendingActionNumber, actionSettingsContainer);
                break;
            case DialogueActionType.SetVisible:
                actionBoolField = BuildToggle("Visible", pendingActionBool, actionSettingsContainer);
                break;
        }
    }

    private void CaptureActionValues()
    {
        if (actionTypeField != null)
            pendingActionType = (DialogueActionType)actionTypeField.value;
        if (targetActorRoleField != null)
            pendingTargetActorRole = targetActorRoleField.value;
        if (secondaryActorRoleField != null)
            pendingSecondaryActorRole = secondaryActorRoleField.value;
        if (actionVectorField != null)
            pendingActionVector = actionVectorField.value;
        if (actionDurationField != null)
            pendingActionDuration = actionDurationField.value;
        if (actionStringField != null)
            pendingActionString = actionStringField.value;
        if (actionNumberField != null)
            pendingActionNumber = actionNumberField.value;
        if (actionBoolField != null)
            pendingActionBool = actionBoolField.value;
        if (waitForActionField != null)
            pendingWaitForAction = waitForActionField.value;
    }

    private TextField BuildTextField(string label, string value, VisualElement parent = null)
    {
        var field = new TextField { multiline = false };
        field.SetValueWithoutNotify(value ?? string.Empty);
        (parent ?? mainContainer).Add(Wrap(label, field));
        return field;
    }

    private FloatField BuildFloatField(string label, float value, VisualElement parent = null)
    {
        var field = new FloatField();
        field.SetValueWithoutNotify(value);
        (parent ?? mainContainer).Add(Wrap(label, field));
        return field;
    }

    private Vector2Field BuildVector2Field(string label, Vector2 value, VisualElement parent = null)
    {
        var field = new Vector2Field();
        field.SetValueWithoutNotify(value);
        (parent ?? mainContainer).Add(Wrap(label, field));
        return field;
    }

    private Toggle BuildToggle(string label, bool value, VisualElement parent = null)
    {
        var field = new Toggle();
        field.SetValueWithoutNotify(value);
        (parent ?? mainContainer).Add(Wrap(label, field));
        return field;
    }

    private ObjectField BuildObjectField<T>(string label, T value) where T : UnityEngine.Object
    {
        var field = new ObjectField { objectType = typeof(T), allowSceneObjects = false };
        field.SetValueWithoutNotify(value);
        mainContainer.Add(Wrap(label, field));
        return field;
    }

    private static VisualElement Wrap(string label, VisualElement field)
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Column;
        container.style.marginBottom = 4;
        container.Add(MakeBoldLabel(label));
        container.Add(field);
        return container;
    }

    private static Label MakeBoldLabel(string text) =>
        new(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } };

    public string GetTextId() => textIdField?.value ?? pendingTextId;
    public string GetSpeakerRole() => speakerRoleField?.value ?? pendingSpeakerRole;
    public string GetExpressionId() => expressionIdField?.value ?? pendingExpressionId;
    public string GetConditionKey() => conditionKeyField?.value ?? pendingConditionKey;
    public string GetSignalId() => signalIdField?.value ?? pendingSignalId;
    public string GetSignalPayload() => signalPayloadField?.value ?? pendingSignalPayload;
    public float GetWaitDuration() => waitDurationField?.value ?? pendingWaitDuration;
    public DialogueContainer GetSubDialogue() => subDialogueField?.value as DialogueContainer ?? pendingSubDialogue;

    public string GetTargetActorRole()
    {
        CaptureActionValues();
        return pendingTargetActorRole;
    }
    public DialogueActionType GetActionType()
    {
        if (actionTypeField != null)
            pendingActionType = (DialogueActionType)actionTypeField.value;
        return pendingActionType;
    }
    public string GetSecondaryActorRole() { CaptureActionValues(); return pendingSecondaryActorRole; }
    public Vector2 GetActionVector() { CaptureActionValues(); return pendingActionVector; }
    public float GetActionDuration() { CaptureActionValues(); return pendingActionDuration; }
    public string GetActionString() { CaptureActionValues(); return pendingActionString; }
    public float GetActionNumber() { CaptureActionValues(); return pendingActionNumber; }
    public bool GetActionBool() { CaptureActionValues(); return pendingActionBool; }
    public bool GetWaitForAction() { CaptureActionValues(); return pendingWaitForAction; }

    public void SetTextId(string value)
    {
        pendingTextId = value ?? string.Empty;
        if (textIdField != null)
        {
            textIdField.SetValueWithoutNotify(pendingTextId);
            UpdatePreview();
        }
    }
    public void SetSpeakerRole(string value) => SetOrPend(value, speakerRoleField, ref pendingSpeakerRole);
    public void SetExpressionId(string value) => SetOrPend(value, expressionIdField, ref pendingExpressionId);
    public void SetConditionKey(string value) => SetOrPend(value, conditionKeyField, ref pendingConditionKey);
    public void SetSignalId(string value) => SetOrPend(value, signalIdField, ref pendingSignalId);
    public void SetSignalPayload(string value) => SetOrPend(value, signalPayloadField, ref pendingSignalPayload);
    public void SetWaitDuration(float value)
    {
        pendingWaitDuration = value;
        waitDurationField?.SetValueWithoutNotify(value);
    }
    public void SetSubDialogue(DialogueContainer value)
    {
        pendingSubDialogue = value;
        subDialogueField?.SetValueWithoutNotify(value);
    }

    public void SetActionData(
        string targetRole,
        DialogueActionType type,
        string secondaryRole,
        Vector2 vector,
        float duration,
        string stringValue,
        float numberValue,
        bool boolValue,
        bool waitForCompletion)
    {
        pendingTargetActorRole = targetRole ?? string.Empty;
        pendingActionType = type;
        pendingSecondaryActorRole = secondaryRole ?? string.Empty;
        pendingActionVector = vector;
        pendingActionDuration = duration;
        pendingActionString = stringValue ?? string.Empty;
        pendingActionNumber = numberValue;
        pendingActionBool = boolValue;
        pendingWaitForAction = waitForCompletion;

        targetActorRoleField?.SetValueWithoutNotify(pendingTargetActorRole);
        actionTypeField?.SetValueWithoutNotify(pendingActionType);
        waitForActionField?.SetValueWithoutNotify(pendingWaitForAction);
        BuildActionSettings();
    }

    private static void SetOrPend(string value, TextField field, ref string pending)
    {
        pending = value ?? string.Empty;
        field?.SetValueWithoutNotify(pending);
    }

    public List<string> GetChoiceIds() => GetDynamicTextFields(outputContainer);
    public List<string> GetChoicePortIds() => GetDynamicPortIds(outputContainer);
    public List<string> GetBranchLabels() => GetDynamicTextFields(outputContainer);
    public List<string> GetBranchPortIds() => GetDynamicPortIds(outputContainer);

    private static List<string> GetDynamicTextFields(VisualElement container) =>
        container.Children().OfType<Port>()
            .Select(p => p.contentContainer.Q<TextField>())
            .Where(f => f != null)
            .Select(f => f.value)
            .ToList();

    private static List<string> GetDynamicPortIds(VisualElement container) =>
        container.Children().OfType<Port>()
            .Select(p => p.userData as string ?? p.portName)
            .ToList();

    public void AddChoicePort(string choiceId, string savedPortId = null)
    {
        string portId = string.IsNullOrWhiteSpace(savedPortId)
            ? "choice:" + Guid.NewGuid().ToString("N")
            : savedPortId;

        Port port = MakePort(Direction.Output, Port.Capacity.Single);
        port.portName = string.Empty;
        port.userData = portId;

        var idField = new TextField { value = choiceId };
        var preview = new Label(DialogueEditorServices.Text(choiceId?.Trim(), CurrentLanguage))
        {
            style = { marginLeft = 4, whiteSpace = WhiteSpace.Normal }
        };
        idField.RegisterValueChangedCallback(evt =>
            preview.text = DialogueEditorServices.Text(evt.newValue.Trim(), CurrentLanguage));

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        row.Add(idField);
        row.Add(preview);
        row.Add(new Button(() => RemoveOutputPort(port)) { text = "X" });
        port.contentContainer.Add(row);

        outputContainer.Add(port);
        RefreshPorts();
        RefreshExpandedState();
    }

    public void AddBranchPort(string label, string savedPortId = null)
    {
        string portId = string.IsNullOrWhiteSpace(savedPortId)
            ? "branch:" + Guid.NewGuid().ToString("N")
            : savedPortId;

        Port port = MakePort(Direction.Output, Port.Capacity.Single);
        port.portName = string.Empty;
        port.userData = portId;

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        row.Add(new TextField { value = label });
        row.Add(new Button(() => RemoveOutputPort(port)) { text = "X" });
        port.contentContainer.Add(row);

        outputContainer.Add(port);
        RefreshPorts();
        RefreshExpandedState();
    }

    private void AddPort(Direction direction, Port.Capacity capacity, string portName, VisualElement container)
    {
        Port port = InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(string));
        port.portName = portName;
        container.Add(port);
    }

    private Port MakePort(Direction direction, Port.Capacity capacity) =>
        InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(string));

    private void RemoveOutputPort(Port port)
    {
        GraphView graphView = this.GetFirstAncestorOfType<GraphView>();
        if (graphView != null)
        {
            foreach (Edge edge in port.connections.ToList())
                graphView.RemoveElement(edge);
        }

        outputContainer.Remove(port);
        RefreshPorts();
        RefreshExpandedState();
    }

    private void SetTitleColor(Color color) => titleContainer.style.backgroundColor = color;

    private static string GetNodeTitle(DialogueNodeType type) => type switch
    {
        DialogueNodeType.Start => "START",
        DialogueNodeType.End => "END",
        DialogueNodeType.Dialogue => "Dialogue",
        DialogueNodeType.Choice => "Choice",
        DialogueNodeType.Condition => "Condition",
        DialogueNodeType.Branch => "Branch",
        DialogueNodeType.Action => "Action",
        DialogueNodeType.Wait => "Wait",
        DialogueNodeType.Signal => "Signal",
        DialogueNodeType.SubDialogue => "Sub Dialogue",
        _ => type.ToString()
    };

    private void SubscribeLocale()
    {
#if UNITY_LOCALIZATION
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
#endif
    }

    private void UnsubscribeLocale()
    {
#if UNITY_LOCALIZATION
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
#endif
    }

#if UNITY_LOCALIZATION
    private void OnLocaleChanged(UnityEngine.Localization.Locale _) => ForcePreviewUpdate();
#endif

    public void ForcePreviewUpdate()
    {
        UpdatePreview();
        if (NodeType == DialogueNodeType.Choice)
        {
            foreach (Port port in outputContainer.Children().OfType<Port>())
            {
                TextField field = port.contentContainer.Q<TextField>();
                Label preview = port.contentContainer.Q<Label>();
                if (field == null || preview == null)
                    continue;
                preview.text = DialogueEditorServices.Text(field.value.Trim(), CurrentLanguage);
            }
        }
    }

    private void UpdatePreview()
    {
        if (previewLabel == null || textIdField == null)
            return;

        string id = textIdField.value.Trim();
        previewLabel.text = string.IsNullOrEmpty(id)
            ? "—"
            : DialogueEditorServices.Text(id, CurrentLanguage);
    }
}
