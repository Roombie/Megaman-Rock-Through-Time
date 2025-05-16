using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CustomSlider : Slider
{
    public float stepSmall = 0.01f;       // tap
    public float stepBig = 0.02f;          // hold
    public float holdDelay = 0.5f;         // time to detect hold
    public float holdRepeatRate = 0.05f;   // time between fast moves

    private bool isHolding = false;
    private float holdTimer = 0f;
    private float repeatTimer = 0f;
    private int holdDirection = 0;
    private bool awaitingRelease = false;
    private PlayerInputActions inputActions;

    protected override void Awake()
    {
        base.Awake();
        inputActions = new PlayerInputActions();
        inputActions.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        inputActions?.Disable();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;
#endif
        inputActions?.Dispose();
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (!IsActive() || !IsInteractable())
        {
            base.OnMove(eventData);
            return;
        }

        if (awaitingRelease) return; // Prevent double tap / input lock

        if (eventData.moveDir == MoveDirection.Left)
        {
            StartHold(-1);
        }
        else if (eventData.moveDir == MoveDirection.Right)
        {
            StartHold(1);
        }
        else
        {
            base.OnMove(eventData);
        }

        awaitingRelease = true;
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        awaitingRelease = false;
    }

    private void StartHold(int direction)
    {
        isHolding = true;
        holdDirection = direction;
        holdTimer = 0f;
        repeatTimer = 0f;

        // Tap movement
        MoveSlider(stepSmall * direction);
    }

    private new void Update()
    {
        if (!isHolding) return;

        holdTimer += Time.unscaledDeltaTime;

        if (holdTimer >= holdDelay)
        {
            repeatTimer += Time.unscaledDeltaTime;

            if (repeatTimer >= holdRepeatRate)
            {
                MoveSlider(stepBig * holdDirection);
                repeatTimer = 0f;
            }
        }

        if (!IsPressingDirection())
        {
            isHolding = false;
            holdDirection = 0;
            holdTimer = 0f;
            repeatTimer = 0f;
            awaitingRelease = false;
        }
    }

    private bool IsPressingDirection()
    {
        if (EventSystem.current?.currentSelectedGameObject != gameObject)
            return false;

        Vector2 moveInput = inputActions.UI.Navigate.ReadValue<Vector2>();

        if (holdDirection == -1)
            return moveInput.x < -0.5f;
        else if (holdDirection == 1)
            return moveInput.x > 0.5f;

        return false;
    }

    private void MoveSlider(float step)
    {
        value = Mathf.Clamp(value + step, minValue, maxValue);
    }
}
