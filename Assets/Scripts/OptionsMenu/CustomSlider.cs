using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CustomSlider : Slider
{
    public float stepSmall = 0.01f;
    public float stepBig = 0.02f;
    public float holdDelay = 0.5f; // time to detect hold
    public float holdRepeatRate = 0.05f; // time between fast moves

    private bool isHolding = false;
    private float holdTimer = 0f;
    private float repeatTimer = 0f;
    private int holdDirection = 0;
    private bool moveOncePending = false;
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
        if (inputActions != null)
            inputActions.Disable();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    #if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;
    #endif
        if (inputActions != null)
            inputActions.Dispose();
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (!IsActive() || !IsInteractable())
        {
            base.OnMove(eventData);
            return;
        }

        if (eventData.moveDir == MoveDirection.Left)
        {
            isHolding = true;
            holdDirection = -1;
            moveOncePending = true;
            holdTimer = 0f;
            repeatTimer = 0f;
        }
        else if (eventData.moveDir == MoveDirection.Right)
        {
            isHolding = true;
            holdDirection = 1;
            moveOncePending = true;
            holdTimer = 0f;
            repeatTimer = 0f;
        }
        else
        {
            base.OnMove(eventData);
        }
    }

    private void MoveSlider(float step)
    {
        value = Mathf.Clamp(value + step, minValue, maxValue);
    }

    private new void Update()
    {
        if (isHolding)
        {
            holdTimer += Time.unscaledDeltaTime;

            if (moveOncePending)
            {
                // Primer toque: mueve 0.01
                MoveSlider(stepSmall * holdDirection);
                moveOncePending = false;
            }
            else if (holdTimer >= holdDelay)
            {
                // Ya pasó holdDelay, ahora empieza movimiento rápido
                repeatTimer += Time.unscaledDeltaTime;

                if (repeatTimer >= holdRepeatRate)
                {
                    MoveSlider(stepBig * holdDirection);
                    repeatTimer = 0f;
                }
            }

            if (!IsPressingDirection())
            {
                // Soltaste el botón
                isHolding = false;
                holdDirection = 0;
                holdTimer = 0f;
                repeatTimer = 0f;
                moveOncePending = false;
            }
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
}