using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-agnostic dialogue participant. It owns identity, presentation references and
/// temporary dialogue control, but does not know about any game's AI systems.
/// </summary>
[DisallowMultipleComponent]
public class DialogueActor : MonoBehaviour
{
    private static readonly Dictionary<string, DialogueActor> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    [Header("Identity")]
    [SerializeField] private string actorId;
    [SerializeField] private DialogueCharacterDefinition character;

    [Header("Action Targets")]
    [SerializeField] private Transform motionRoot;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer facingRenderer;

    [Header("Optional Facing Animator Parameters")]
    [SerializeField] private string facingXParameter;
    [SerializeField] private string facingYParameter;

    [Header("Dialogue Control (optional)")]
    [Tooltip("Behaviours temporarily disabled while this actor participates in dialogue.")]
    [SerializeField] private Behaviour[] behavioursToSuspend = Array.Empty<Behaviour>();
    [SerializeField] private Rigidbody2D bodyToStop;

    private bool[] suspendedBehaviourStates;
    private int dialogueControlDepth;
    private readonly List<Coroutine> activeRoutines = new();
    private bool hasShakeRest;
    private Vector3 shakeRest;

    public string ActorId => actorId;
    public DialogueCharacterDefinition Character => character;
    public Transform MotionRoot => motionRoot != null ? motionRoot : transform;
    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public Animator Animator => animator;

    protected virtual void Awake()
    {
        if (motionRoot == null) motionRoot = transform;
        if (visualRoot == null) visualRoot = transform;
    }

    protected virtual void OnEnable() => Register();
    protected virtual void OnDisable() => Unregister();

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        actorId = actorId?.Trim();
    }
#endif

    public static DialogueActor FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        Registry.TryGetValue(id.Trim(), out DialogueActor actor);
        return actor;
    }

    public Sprite GetPortrait(string expressionId) =>
        character != null ? character.GetPortrait(expressionId) : null;

    public virtual void AcquireDialogueControl()
    {
        dialogueControlDepth++;
        if (dialogueControlDepth != 1)
            return;

        suspendedBehaviourStates = new bool[behavioursToSuspend.Length];
        for (int i = 0; i < behavioursToSuspend.Length; i++)
        {
            Behaviour behaviour = behavioursToSuspend[i];
            if (behaviour == null) continue;
            suspendedBehaviourStates[i] = behaviour.enabled;
            behaviour.enabled = false;
        }

        if (bodyToStop != null)
            bodyToStop.linearVelocity = Vector2.zero;
    }

    public virtual void ReleaseDialogueControl()
    {
        if (dialogueControlDepth <= 0)
            return;

        dialogueControlDepth--;
        if (dialogueControlDepth != 0)
            return;

        for (int i = 0; i < activeRoutines.Count; i++)
            if (activeRoutines[i] != null)
                StopCoroutine(activeRoutines[i]);
        activeRoutines.Clear();

        if (hasShakeRest && VisualRoot != null)
            VisualRoot.localPosition = shakeRest;
        hasShakeRest = false;

        for (int i = 0; i < behavioursToSuspend.Length; i++)
        {
            Behaviour behaviour = behavioursToSuspend[i];
            if (behaviour == null) continue;
            bool wasEnabled = suspendedBehaviourStates != null &&
                              i < suspendedBehaviourStates.Length &&
                              suspendedBehaviourStates[i];
            behaviour.enabled = wasEnabled;
        }

        suspendedBehaviourStates = null;
    }

    /// <summary>
    /// Generic fallback executor with no third-party dependency. Projects may override it
    /// through IDialogueActionExecutor (AnimalWood does so with PrimeTween).
    /// </summary>
    public virtual void ExecuteDefaultAction(DialogueActionRequest request, Action onComplete)
    {
        if (request == null)
        {
            onComplete?.Invoke();
            return;
        }

        switch (request.Type)
        {
            case DialogueActionType.Shake:
                Track(StartCoroutine(ShakeRoutine(request, onComplete)));
                break;
            case DialogueActionType.MoveBy:
            {
                Vector3 start = MotionRoot.position;
                Vector3 end = start + new Vector3(request.Vector.x, request.Vector.y, 0f);
                Track(StartCoroutine(MoveRoutine(end, request.Duration, onComplete)));
                break;
            }
            case DialogueActionType.MoveTo:
            {
                Vector3 current = MotionRoot.position;
                Vector3 end = new(request.Vector.x, request.Vector.y, current.z);
                Track(StartCoroutine(MoveRoutine(end, request.Duration, onComplete)));
                break;
            }
            case DialogueActionType.MoveToActor:
            {
                if (request.SecondaryActor == null) { onComplete?.Invoke(); break; }
                Vector3 target = request.SecondaryActor.MotionRoot.position;
                Vector3 end = new(target.x + request.Vector.x, target.y + request.Vector.y, MotionRoot.position.z);
                Track(StartCoroutine(MoveRoutine(end, request.Duration, onComplete)));
                break;
            }
            case DialogueActionType.Teleport:
            {
                Vector3 current = MotionRoot.position;
                MotionRoot.position = new Vector3(request.Vector.x, request.Vector.y, current.z);
                onComplete?.Invoke();
                break;
            }
            case DialogueActionType.FaceDirection:
                Face(request.Vector);
                onComplete?.Invoke();
                break;
            case DialogueActionType.FaceActor:
                if (request.SecondaryActor != null)
                {
                    Vector3 delta = request.SecondaryActor.MotionRoot.position - MotionRoot.position;
                    Face(new Vector2(delta.x, delta.y));
                }
                onComplete?.Invoke();
                break;
            case DialogueActionType.PlayAnimation:
                if (animator != null && !string.IsNullOrWhiteSpace(request.StringValue))
                    animator.Play(request.StringValue.Trim());
                CompleteAfter(request.Duration, onComplete);
                break;
            case DialogueActionType.AnimatorTrigger:
                if (animator != null && !string.IsNullOrWhiteSpace(request.StringValue))
                    animator.SetTrigger(request.StringValue.Trim());
                CompleteAfter(request.Duration, onComplete);
                break;
            case DialogueActionType.AnimatorBool:
                if (animator != null && !string.IsNullOrWhiteSpace(request.StringValue))
                    animator.SetBool(request.StringValue.Trim(), request.BoolValue);
                onComplete?.Invoke();
                break;
            case DialogueActionType.AnimatorFloat:
                if (animator != null && !string.IsNullOrWhiteSpace(request.StringValue))
                    animator.SetFloat(request.StringValue.Trim(), request.NumberValue);
                onComplete?.Invoke();
                break;
            case DialogueActionType.SetVisible:
                if (VisualRoot != null)
                    VisualRoot.gameObject.SetActive(request.BoolValue);
                onComplete?.Invoke();
                break;
            default:
                onComplete?.Invoke();
                break;
        }
    }

    public void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.000001f)
            return;

        direction.Normalize();

        if (facingRenderer != null && Mathf.Abs(direction.x) > 0.001f)
            facingRenderer.flipX = direction.x < 0f;

        if (animator != null)
        {
            if (!string.IsNullOrWhiteSpace(facingXParameter))
                animator.SetFloat(facingXParameter, direction.x);
            if (!string.IsNullOrWhiteSpace(facingYParameter))
                animator.SetFloat(facingYParameter, direction.y);
        }
    }

    private void CompleteAfter(float duration, Action onComplete)
    {
        if (onComplete == null) return;
        if (duration <= 0.0001f) { onComplete(); return; }
        Track(StartCoroutine(DelayRoutine(duration, onComplete)));
    }

    private IEnumerator DelayRoutine(float duration, Action onComplete)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
        onComplete?.Invoke();
    }

    private IEnumerator MoveRoutine(Vector3 destination, float duration, Action onComplete)
    {
        Transform target = MotionRoot;
        if (target == null) { onComplete?.Invoke(); yield break; }

        duration = Mathf.Max(0f, duration);
        if (duration <= 0.0001f)
        {
            target.position = destination;
            onComplete?.Invoke();
            yield break;
        }

        Vector3 start = target.position;
        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.position = Vector3.LerpUnclamped(start, destination, t);
            yield return null;
        }

        if (target != null) target.position = destination;
        onComplete?.Invoke();
    }

    private IEnumerator ShakeRoutine(DialogueActionRequest request, Action onComplete)
    {
        Transform target = VisualRoot;
        if (target == null) { onComplete?.Invoke(); yield break; }

        Vector3 rest = target.localPosition;
        shakeRest = rest;
        hasShakeRest = true;

        Vector2 strength = request.Vector;
        if (strength.sqrMagnitude <= 0.000001f)
            strength = new Vector2(0.08f, 0.04f);

        float duration = Mathf.Max(0.01f, request.Duration);
        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float falloff = 1f - Mathf.Clamp01(elapsed / duration);
            target.localPosition = rest + new Vector3(
                UnityEngine.Random.Range(-Mathf.Abs(strength.x), Mathf.Abs(strength.x)) * falloff,
                UnityEngine.Random.Range(-Mathf.Abs(strength.y), Mathf.Abs(strength.y)) * falloff,
                0f);
            yield return null;
        }

        if (target != null) target.localPosition = rest;
        hasShakeRest = false;
        onComplete?.Invoke();
    }

    private void Track(Coroutine routine)
    {
        if (routine != null)
            activeRoutines.Add(routine);
    }

    private void Register()
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return;

        string key = actorId.Trim();
        if (Registry.TryGetValue(key, out DialogueActor existing) && existing != null && existing != this)
            Debug.LogWarning($"[DialogueActor] Actor ID '{key}' was already registered by '{existing.name}'.", this);

        Registry[key] = this;
    }

    private void Unregister()
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return;

        string key = actorId.Trim();
        if (Registry.TryGetValue(key, out DialogueActor existing) && existing == this)
            Registry.Remove(key);
    }
}
