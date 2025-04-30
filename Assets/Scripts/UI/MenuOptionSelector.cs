using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.InputSystem.Samples.RebindUI;
using System.Collections.Generic;

[System.Serializable]
public class DisplayModeOption
{
    public string Key;
    public string DisplayName;
    public string ColorHex;
    public GamepadIconsExample.ControlScheme Scheme;
}

public class MenuOptionSelector : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public TextMeshProUGUI optionText;
    public GameObject leftArrow, rightArrow;
    public SettingType settingKey;

    public string[] optionKeys;
    public int currentIndex;
    private bool isSelecting = false;
    private PlayerInputActions inputActions;
    private OptionsMenu optionsMenu;
    private static MenuOptionSelector currentlySelecting = null;
    private ArrowSelector arrowSelector;
    private List<DisplayModeOption> displayOptions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        optionsMenu = FindFirstObjectByType<OptionsMenu>();
        arrowSelector = FindFirstObjectByType<ArrowSelector>();
    }

    void OnEnable()
    {
        inputActions.UI.Navigate.performed += OnNavigate;
        inputActions.UI.Submit.performed += OnSubmit;
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.UI.Navigate.performed -= OnNavigate;
        inputActions.UI.Submit.performed -= OnSubmit;
        inputActions.Disable();
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
            // inputActions.Dispose(); // Uncomment if it's necessary
        }
    }

    void Start()
    {
        if (optionsMenu == null)
        {
            Debug.LogError("OptionsMenu not found in scene!");
            return;
        }

        LoadOptions();
        LoadFromOptionsMenu();
        StartCoroutine(DelayedUpdateText());
    }

    private IEnumerator DelayedUpdateText()
    {
        yield return new WaitForEndOfFrame();
        UpdateOptionText();
    }

    private void LoadOptions()
    {
        switch (settingKey)
        {
            case SettingType.GraphicsQuality:
                optionKeys = optionsMenu.GetGraphicsQualityOptions();
                break;
            case SettingType.Resolution:
                optionKeys = optionsMenu.GetResolutionOptions();
                break;
            case SettingType.Language:
                optionKeys = optionsMenu.GetLanguageOptions();
                break;
            case SettingType.DisplayMode:
                displayOptions = new List<DisplayModeOption>
                {
                    new() { Key = "Auto", DisplayName = "Auto", ColorHex = "#AAAAAA", Scheme = GamepadIconsExample.ControlScheme.Auto },
                    new() { Key = "Xbox", DisplayName = "Xbox", ColorHex = "#58D854", Scheme = GamepadIconsExample.ControlScheme.Xbox },
                    new() { Key = "PlayStation", DisplayName = "PlayStation", ColorHex = "#0088D8", Scheme = GamepadIconsExample.ControlScheme.PlayStation },
                    new() { Key = "Nintendo", DisplayName = "Nintendo", ColorHex = "#E43B44", Scheme = GamepadIconsExample.ControlScheme.Nintendo },
                    new() { Key = "Custom", DisplayName = "Custom", ColorHex = "#FFD700", Scheme = GamepadIconsExample.ControlScheme.Custom },
                };
                break;
            default:
                Debug.LogError($"Unknown settingKey: {settingKey} for {optionText.text}");
                return;
        }

        if (settingKey != SettingType.DisplayMode && (optionKeys == null || optionKeys.Length == 0))
        {
            Debug.LogError($"optionKeys is empty for {settingKey} in {optionText.text}");
            return;
        }
    }

    private void OnNavigate(InputAction.CallbackContext context)
    {
        if (!isSelecting) return;

        Vector2 input = context.ReadValue<Vector2>();
        if (input.x < 0) ChangeOption(-1);
        else if (input.x > 0) ChangeOption(1);
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (EventSystem.current.currentSelectedGameObject != gameObject) return;

        if (!isSelecting)
        {
            if (currentlySelecting != null && currentlySelecting != this) return;

            isSelecting = true;
            currentlySelecting = this;
            EventSystem.current.SetSelectedGameObject(gameObject);
            EventSystem.current.sendNavigationEvents = false;

            leftArrow.SetActive(true);
            rightArrow.SetActive(true);

            if (arrowSelector != null)
                arrowSelector.isSelectingOption = true;
        }
        else
        {
            isSelecting = false;
            currentlySelecting = null;
            EventSystem.current.sendNavigationEvents = true;

            leftArrow.SetActive(false);
            rightArrow.SetActive(false);

            SaveToOptionsMenu();
            UpdateOptionText();

            if (arrowSelector != null)
            {
                arrowSelector.isSelectingOption = false;
                arrowSelector.MoveIndicator(arrowSelector.lastSelected);
            }
        }
    }

    private void ChangeOption(int change)
    {
        if (settingKey == SettingType.DisplayMode)
        {
            if (displayOptions == null || displayOptions.Count == 0) return;
            currentIndex = Mathf.Clamp(currentIndex + change, 0, displayOptions.Count - 1);
            UpdateOptionText();
        }
        else
        {
            if (optionKeys == null || optionKeys.Length == 0) return;
            currentIndex = Mathf.Clamp(currentIndex + change, 0, optionKeys.Length - 1);
            optionText.text = optionKeys[currentIndex];
        }
    }

    private DisplayModeOption GetCurrentDisplayModeOption()
    {
        if (displayOptions == null || currentIndex < 0 || currentIndex >= displayOptions.Count)
            return null;
        return displayOptions[currentIndex];
    }

    public void UpdateOptionText()
    {
        if (settingKey != SettingType.DisplayMode && (optionKeys == null || optionKeys.Length == 0))
        {
            Debug.LogError($"[UpdateOptionText] optionKeys is empty for {settingKey}");
            return;
        }

        if (settingKey == SettingType.DisplayMode)
        {
            if (currentIndex < 0 || currentIndex >= displayOptions.Count)
                currentIndex = 0;
        }
        else
        {
            if (currentIndex < 0 || currentIndex >= optionKeys.Length)
                currentIndex = 0;
        }

        if (settingKey == SettingType.DisplayMode)
        {
            var current = GetCurrentDisplayModeOption();
            if (current == null) return;

            var iconsExample = FindFirstObjectByType<GamepadIconsExample>();

            if (iconsExample != null && iconsExample.GetCurrentScheme() != iconsExample.userScheme && !isSelecting)
            {
                var auto = displayOptions.Find(x => x.Scheme == GamepadIconsExample.ControlScheme.Auto);
                optionText.text = $"<color={auto.ColorHex}>{auto.DisplayName}</color>";
            }
            else
            {
                optionText.text = $"<color={current.ColorHex}>{current.DisplayName}</color>";
            }
        }
        else
        {
            optionText.text = optionKeys[currentIndex];
        }

        optionText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
    }

    private void UpdateGamepadIcons()
    {
        if (settingKey != SettingType.DisplayMode) return;

        var iconsExample = FindFirstObjectByType<GamepadIconsExample>();
        var selected = GetCurrentDisplayModeOption();
        if (iconsExample == null || selected == null) return;

        switch (selected.Scheme)
        {
            case GamepadIconsExample.ControlScheme.Auto:
                iconsExample.SetAutoScheme();
                break;
            case GamepadIconsExample.ControlScheme.Custom:
                iconsExample.SetCustomSprites();
                break;
            default:
                iconsExample.SetControlScheme(selected.Scheme);
                break;
        }

        iconsExample.RefreshAllIcons();
    }

    private void LoadFromOptionsMenu()
    {
        if (optionsMenu == null) return;

        switch (settingKey)
        {
            case SettingType.GraphicsQuality:
                currentIndex = optionsMenu.GetGraphicsQuality();
                break;
            case SettingType.Resolution:
                currentIndex = optionsMenu.GetResolutionIndex();
                break;
            case SettingType.Language:
                currentIndex = optionsMenu.GetLanguageIndex();
                break;
            case SettingType.DisplayMode:
                currentIndex = PlayerPrefs.GetInt(SettingsKeys.DisplayModeKey, 0);
                break;
        }

        if (currentIndex < 0 || currentIndex >= optionKeys.Length)
        {
            currentIndex = 0;
        }
    }

    private void SaveToOptionsMenu()
    {
        if (optionsMenu == null) return;

        switch (settingKey)
        {
            case SettingType.GraphicsQuality:
                optionsMenu.SetGraphicsQuality(currentIndex);
                break;
            case SettingType.Resolution:
                optionsMenu.SetResolution(currentIndex);
                break;
            case SettingType.Language:
                optionsMenu.SetLanguage(currentIndex);
                break;
            case SettingType.DisplayMode:
                PlayerPrefs.SetInt(SettingsKeys.DisplayModeKey, currentIndex);
                PlayerPrefs.Save();
                UpdateGamepadIcons();
                break;
        }

        UpdateOptionText();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!isSelecting) return;
        leftArrow.SetActive(true);
        rightArrow.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!isSelecting)
        {
            leftArrow.SetActive(false);
            rightArrow.SetActive(false);
        }
    }
}