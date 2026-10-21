using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// The menus' input (the UI map: Navigate, Submit, Cancel, the slider step modifiers), from whoever owns the
/// menus right now:
///   - a player (set by PauseManager when they pause): their own devices and rebinds. Only they drive the menus,
///     the Options screen opened from the pause menu included, and the scenes' UI input modules (uGUI navigation)
///     follow them too;
///   - nobody (Title screen, Options from the Title): any device, with player 1's rebinds.
/// Menu scripts read this instead of creating their own PlayerInputActions.
/// </summary>
public static class MenuInput
{
    /// <summary>The current owner's UI actions were performed. Subscribing sets the actions up.</summary>
    public static event Action<InputAction.CallbackContext> NavigatePerformed
    {
        add { Refresh(); navigatePerformed += value; }
        remove => navigatePerformed -= value;
    }
    public static event Action<InputAction.CallbackContext> SubmitPerformed
    {
        add { Refresh(); submitPerformed += value; }
        remove => submitPerformed -= value;
    }
    public static event Action<InputAction.CallbackContext> CancelPerformed
    {
        add { Refresh(); cancelPerformed += value; }
        remove => cancelPerformed -= value;
    }
    private static Action<InputAction.CallbackContext> navigatePerformed, submitPerformed, cancelPerformed;

    /// <summary>The player whose input drives the menus (null: any device).</summary>
    public static PlayerInput Owner { get; private set; }
    /// <summary>The owner's player index (0 = player 1, also with no owner).</summary>
    public static int OwnerIndex => Owner != null ? Mathf.Max(0, Owner.playerIndex) : 0;

    public static InputAction Navigate { get { Refresh(); return navigate; } }
    public static InputAction StepModifier10 { get { Refresh(); return step10; } }
    public static InputAction StepModifier25 { get { Refresh(); return step25; } }
    public static InputAction StepModifier50 { get { Refresh(); return step50; } }

    public static Vector2 ReadNavigate() => IsUsable(Navigate) ? navigate.ReadValue<Vector2>() : Vector2.zero;
    public static bool IsHeld(InputAction action) => IsUsable(action) && action.IsPressed();

    private static PlayerInputActions shared;          // any device, when nobody owns the menus
    private static PlayerInput boundOwner;             // the owner the actions below came from
    private static InputActionAsset bound;
    private static InputAction navigate, submit, cancel, step10, step25, step50;
    private static bool ownerUiWasEnabled;
    private static readonly Dictionary<InputSystemUIInputModule, InputActionAsset> moduleAssets = new();

    /// <summary>Gives the menus to that player (null: back to any device).</summary>
    public static void SetOwner(PlayerInput owner)
    {
        if (owner == Owner && bound != null) return;
        Owner = owner;
        Refresh();
        ApplyToUIModules();
    }

    /// <summary>Picks up rebinds made in the Options screen (for the any-device actions; players load their own).</summary>
    public static void ReloadBindings()
    {
        if (shared != null) InputBindingStore.Load(shared.asset, 0);
    }

    // Follows the owner (or its disappearance: a destroyed player falls back to any device)
    private static void Refresh()
    {
        if (Owner == null && !ReferenceEquals(Owner, null)) Owner = null;
        InputActionAsset wanted = Owner != null && Owner.actions != null ? Owner.actions : Shared().asset;
        if (wanted == bound && boundOwner == Owner) return;

        Unbind();
        boundOwner = Owner;
        bound = wanted;
        navigate = Find("Navigate");
        submit = Find("Submit");
        cancel = Find("Cancel");
        step10 = Find("StepModifier10");
        step25 = Find("StepModifier25");
        step50 = Find("StepModifier50");
        if (submit != null) submit.performed += OnSubmit;
        if (cancel != null) cancel.performed += OnCancel;
        if (navigate != null) navigate.performed += OnNavigate;

        InputActionMap ui = bound.FindActionMap("UI");
        if (Owner != null)
        {
            // Only the owner's devices: the any-device actions go quiet
            shared.UI.Disable();
            ownerUiWasEnabled = ui != null && ui.enabled;
            ui?.Enable();
        }
        else
        {
            shared.UI.Enable();
        }
    }

    private static void Unbind()
    {
        if (submit != null) submit.performed -= OnSubmit;
        if (cancel != null) cancel.performed -= OnCancel;
        if (navigate != null) navigate.performed -= OnNavigate;
        // The previous owner's UI map back as it was
        if (boundOwner != null && bound != null && !ownerUiWasEnabled) bound.FindActionMap("UI")?.Disable();
        bound = null;
        boundOwner = null;
        navigate = submit = cancel = step10 = step25 = step50 = null;
    }

    private static InputAction Find(string name) => bound.FindAction("UI/" + name, throwIfNotFound: false);
    private static bool IsUsable(InputAction action) => action != null && action.enabled;

    private static PlayerInputActions Shared()
    {
        if (shared != null) return shared;
        shared = new PlayerInputActions();
        InputBindingStore.Load(shared.asset, 0);
        return shared;
    }

    private static void OnSubmit(InputAction.CallbackContext context) => submitPerformed?.Invoke(context);
    private static void OnCancel(InputAction.CallbackContext context) => cancelPerformed?.Invoke(context);
    private static void OnNavigate(InputAction.CallbackContext context) => navigatePerformed?.Invoke(context);

    #region uGUI navigation
    // The scenes' UI input modules move the uGUI selection: while a player owns the menus they use that player's
    // actions, then go back to their own
    private static void ApplyToUIModules()
    {
        foreach (var module in UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None))
        {
            if (Owner != null && Owner.actions != null)
            {
                if (module.actionsAsset == Owner.actions) continue;
                if (!moduleAssets.ContainsKey(module)) moduleAssets[module] = module.actionsAsset;
                module.actionsAsset = Owner.actions;
            }
            else if (moduleAssets.TryGetValue(module, out InputActionAsset original))
            {
                module.actionsAsset = original;
            }
        }
        if (Owner == null) moduleAssets.Clear();
    }

    // A scene loaded while a player owns the menus (the Options screen over the paused game) follows them too
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Owner != null) ApplyToUIModules();
    }
    #endregion

    // With Enter Play Mode Options (no domain reload) statics survive between play sessions: start clean
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        navigatePerformed = submitPerformed = cancelPerformed = null;
        Owner = null;
        boundOwner = null;
        bound = null;
        navigate = submit = cancel = step10 = step25 = step50 = null;
        shared?.Dispose();
        shared = null;
        moduleAssets.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}
