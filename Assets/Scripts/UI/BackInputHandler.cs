using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackInputHandler : MonoBehaviour
{
    [Tooltip("Button to click when back is pressed and no interaction is active")]
    public Button fallbackButton;

    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.UI.Cancel.performed += OnCancelPressed;
    }

    private void OnDisable()
    {
        inputActions.UI.Cancel.performed -= OnCancelPressed;
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions.Dispose();
    }

    private void OnCancelPressed(InputAction.CallbackContext context)
    {
        if (UIInteractionState.IsAnyInteractionActive()) return;
        // Spamming Esc: ignore it while any fade or page change is still running
        if (ScreenFader.Instance != null && ScreenFader.Instance.IsTransitioning) return;
        if (PageFader.AnyTransitioning) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsTransitioning) return;

        {
            if (fallbackButton != null)
            {
                ArrowSelector.suppressSoundOnNextExternalSelection = true;
                EventSystem.current.SetSelectedGameObject(fallbackButton.gameObject);
                fallbackButton.onClick.Invoke();
            }
        }
    }
}