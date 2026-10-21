using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackInputHandler : MonoBehaviour
{
    [Tooltip("Button to click when back is pressed and no interaction is active")]
    public Button fallbackButton;

    // The menus' Cancel: the player who paused, or any device (MenuInput)
    private void OnEnable() => MenuInput.CancelPerformed += OnCancelPressed;

    private void OnDisable() => MenuInput.CancelPerformed -= OnCancelPressed;

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