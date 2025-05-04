using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Linq;

public class VolumeSettingHandler : MonoBehaviour, ISettingHandler
{
    [SettingTypeFilter(SettingType.MasterVolumeKey, SettingType.MusicVolumeKey, SettingType.SFXVolumeKey, SettingType.VoiceVolumeKey)]
    public SettingType settingType;
    public Slider slider;
    public TextMeshProUGUI valueText;

    public Color minVolumeColor = new(1f, 0.2f, 0.2f);
    public Color maxVolumeColor = new(0.2f, 0.75f, 1f);

    public float holdThreshold = 0.5f;
    public float moveCooldown = 0.15f;
    public float holdIncrement = 0.02f;
    public float initialIncrement = 0.01f;

    private bool isNavigating = false;
    private bool isHolding = false;
    private float holdStartTime = 0f;
    private float lastMoveTime = 0f;

    private PlayerInputActions inputActions;

    public SettingType SettingType => settingType;

    #if UNITY_EDITOR
    private void OnValidate()
    {
        var allowed = new SettingType[]
        {
            SettingType.MasterVolumeKey,
            SettingType.MusicVolumeKey,
            SettingType.SFXVolumeKey,
            SettingType.VoiceVolumeKey
        };

        if (!System.Array.Exists(allowed, t => t == settingType))
        {
            Debug.LogWarning($"{nameof(VolumeSettingHandler)}: Invalid SettingType '{settingType}' assigned. Resetting to default.");
            settingType = allowed[0];
        }
    }
    #endif

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.UI.Navigate.started += OnNavigateStarted;
        inputActions.UI.Navigate.performed += OnNavigatePerformed;
        inputActions.UI.Navigate.canceled += OnNavigateCanceled;
    }

    private void OnDisable()
    {
        inputActions.UI.Navigate.started -= OnNavigateStarted;
        inputActions.UI.Navigate.performed -= OnNavigatePerformed;
        inputActions.UI.Navigate.canceled -= OnNavigateCanceled;
        if (inputActions != null)
            inputActions.Disable();
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
        }    
    }

    private void Start()
    {
        slider.onValueChanged.AddListener(OnValueChanged);
        ApplyFromSaved();
    }

    private void Update()
    {
        if (isNavigating)
        {
            holdStartTime += Time.unscaledDeltaTime;

            if (holdStartTime >= holdThreshold && !isHolding)
            {
                isHolding = true;
                lastMoveTime = 0f;
            }

            if (isHolding && Time.unscaledTime - lastMoveTime > moveCooldown)
            {
                MoveSlider(holdIncrement);
                lastMoveTime = Time.unscaledTime;
            }
        }
    }

    public void OnValueChanged(float value)
    {
        valueText.text = (value * 100).ToString("00");
        UpdateTextColor(valueText, value);
        AudioManager.Instance.SetVolume(settingType, value);
        Save();
    }

    public void ApplyFromSaved()
    {
        float saved = PlayerPrefs.GetFloat(SettingsKeys.Get(settingType), 0.5f);
        slider.value = saved;
        OnValueChanged(saved);
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(SettingsKeys.Get(settingType), slider.value);
        PlayerPrefs.Save();
    }

    public void Apply(int index) { }
    public void Apply(bool value) { }
    public void RefreshUI() => OnValueChanged(slider.value);

    private void UpdateTextColor(TextMeshProUGUI text, float volume)
    {
        int volumePercentage = Mathf.RoundToInt(volume * 100f);
        text.color = volumePercentage switch
        {
            100 => maxVolumeColor,
            0 => minVolumeColor,
            _ => Color.white
        };
    }

    private void MoveSlider(float incrementAmount)
    {
        var selectedSlider = GetCurrentlySelectedSlider();
        if (selectedSlider == null || selectedSlider != slider)
            return;

        Vector2 navigation = inputActions.UI.Navigate.ReadValue<Vector2>();

        if (navigation.x > 0.5f)
        {
            selectedSlider.value = Mathf.Clamp(selectedSlider.value + incrementAmount, selectedSlider.minValue, selectedSlider.maxValue);
        }
        else if (navigation.x < -0.5f)
        {
            selectedSlider.value = Mathf.Clamp(selectedSlider.value - incrementAmount, selectedSlider.minValue, selectedSlider.maxValue);
        }

        valueText.text = (selectedSlider.value * 100).ToString("00");
        UpdateTextColor(valueText, selectedSlider.value);
    }

    private Slider GetCurrentlySelectedSlider()
    {
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        return selectedObject?.GetComponent<Slider>();
    }

    private void OnNavigateStarted(InputAction.CallbackContext context)
    {
        isNavigating = true;
        holdStartTime = 0f;
        isHolding = false;
    }

    private void OnNavigateCanceled(InputAction.CallbackContext context)
    {
        isNavigating = false;
        isHolding = false;
        holdStartTime = 0f;
    }

    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        if (!isHolding)
        {
            MoveSlider(initialIncrement);
            lastMoveTime = Time.unscaledTime;
        }
    }
}