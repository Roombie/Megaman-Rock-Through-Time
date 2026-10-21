using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A local player's commands, read from their PlayerInput (their own devices and their own copy of the actions,
/// with that player's saved rebinds). Added to Mega Man at runtime if the prefab doesn't have it.
/// Reads the Player map's actions by name: Move, Jump, Shoot, Slide, NextWeapon, PreviousWeapon, and the menu
/// buttons WeaponsMenu and OptionsMenu (pause) for PauseManager.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class LocalPlayerInput : MonoBehaviour, IPlayerInputSource
{
    /// <summary>The local players in the game (enabled), in the order they came in.</summary>
    public static readonly List<LocalPlayerInput> All = new();

    private PlayerInput playerInput;
    private InputActionAsset boundTo;     // the actions the lookups below came from
    private InputAction move, jump, shoot, slide, nextWeapon, previousWeapon, weaponsMenu, optionsMenu;

    public int PlayerIndex => playerInput != null ? Mathf.Max(0, playerInput.playerIndex) : 0;
    public PlayerInput PlayerInput => playerInput;

    private void Awake() => playerInput = GetComponent<PlayerInput>();

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable() => All.Remove(this);

    // With Enter Play Mode Options (no domain reload) statics survive between play sessions
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    /// <summary>Menu buttons (not gameplay): pressed this frame on this player's devices.</summary>
    public bool PausePressed => Bind() && WasPressed(optionsMenu);
    public bool WeaponsMenuPressed => Bind() && WasPressed(weaponsMenu);
    public bool NextWeaponPressed => Bind() && WasPressed(nextWeapon);
    public bool PreviousWeaponPressed => Bind() && WasPressed(previousWeapon);

    private static bool WasPressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();

    // Looks the actions up again if this player's actions changed. False: no actions to read.
    private bool Bind()
    {
        if (playerInput == null || playerInput.actions == null) return false;
        if (boundTo != playerInput.actions) FindActions();
        return true;
    }

    // After PlayerInput has set up this player's actions (and devices)
    private void Start() => InputBindingStore.Load(playerInput.actions, PlayerIndex);

    public PlayerCommands ReadCommands()
    {
        if (!Bind()) return default;

        var commands = new PlayerCommands { Move = move != null ? move.ReadValue<Vector2>() : Vector2.zero };
        if (IsHeld(jump)) commands.Held |= PlayerButtons.Jump;
        if (IsHeld(shoot)) commands.Held |= PlayerButtons.Shoot;
        if (IsHeld(slide)) commands.Held |= PlayerButtons.Slide;
        if (IsHeld(nextWeapon)) commands.Held |= PlayerButtons.NextWeapon;
        if (IsHeld(previousWeapon)) commands.Held |= PlayerButtons.PreviousWeapon;
        return commands;
    }

    private static bool IsHeld(InputAction action) => action != null && action.enabled && action.IsPressed();

    private void FindActions()
    {
        boundTo = playerInput.actions;
        move = Find("Move");
        jump = Find("Jump");
        shoot = Find("Shoot");
        slide = Find("Slide");
        nextWeapon = Find("NextWeapon");
        previousWeapon = Find("PreviousWeapon");
        weaponsMenu = Find("WeaponsMenu");
        optionsMenu = Find("OptionsMenu");
    }

    private InputAction Find(string name) => boundTo.FindAction("Player/" + name, throwIfNotFound: false);
}
