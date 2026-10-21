using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class DialogueNodeData
{
    public string guid = Guid.NewGuid().ToString();
    public string nodeId;
    public Vector2 position;

    public DialogueNodeType nodeType;

    // Dialogue node
    public string textId;
    public string speakerRole;
    public string expressionId;

    // Choice node
    public List<string> choiceIds = new();
    public List<string> choicePortIds = new();

    // Condition node
    public string conditionKey;

    // Branch node
    public List<string> branchLabels = new();
    public List<string> branchPortIds = new();

    // Action node
    public string targetActorRole;
    public DialogueActionType actionType;
    public string secondaryActorRole;
    public Vector2 actionVector;
    public float actionDuration = 0.35f;
    public string actionString;
    public float actionNumber;
    public bool actionBool = true;
    public bool waitForAction = true;

    // Wait node
    public float waitDuration = 0.5f;

    // Signal node
    public string signalId;
    public string signalPayload;

    // Sub-dialogue node
    public DialogueContainer subDialogue;
}
