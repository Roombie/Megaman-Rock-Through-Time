using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

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

    private bool isNavigating;
    private bool isHolding;
    private float holdStartTime;
    private float lastMoveTime;

    private PlayerInputActions inputActions;

    public SettingType SettingType => settingType;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!System.Enum.IsDefined(typeof(SettingType), settingType))
        {
            Debug.LogWarning($"[VolumeSettingHandler] Invalid SettingType: {settingType}. Resetting.");
            settingType = SettingType.MasterVolumeKey;
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
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Disable();
        inputActions?.Dispose();
    }

    private void Start()
    {
        slider.onValueChanged.AddListener(OnValueChanged);
        ApplyFromSaved();
    }

    private void Update()
    {
        if (!isNavigating) return;

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

    public void OnValueChanged(float value)
    {
        SetText(value);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(settingType, value);
        }

        Save();
    }

    public void ApplyFromSaved()
    {
        float saved = PlayerPrefs.GetFloat(SettingsKeys.Get(settingType), 0.5f);
        slider.SetValueWithoutNotify(saved);
        SetText(saved);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(settingType, saved);
        }
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(SettingsKeys.Get(settingType), slider.value);
        PlayerPrefs.Save();
    }

    public void Apply(int index) { }
    public void Apply(bool value) { }
    public void RefreshUI() => OnValueChanged(slider.value);

    private void SetText(float volume)
    {
        valueText.text = (volume * 100).ToString("00");
        valueText.color = volume switch
        {
            1f => maxVolumeColor,
            0f => minVolumeColor,
            _ => Color.white
        };
    }

    private void MoveSlider(float incrementAmount)
    {
        if (EventSystem.current.currentSelectedGameObject?.GetComponent<Slider>() != slider)
            return;

        Vector2 nav = inputActions.UI.Navigate.ReadValue<Vector2>();
        float direction = nav.x > 0.5f ? 1 : nav.x < -0.5f ? -1 : 0;

        if (direction != 0)
        {
            slider.value = Mathf.Clamp(slider.value + direction * incrementAmount, slider.minValue, slider.maxValue);
            SetText(slider.value);
        }
    }

    private void OnNavigateStarted(InputAction.CallbackContext _) => (isNavigating, holdStartTime, isHolding) = (true, 0f, false);
    private void OnNavigateCanceled(InputAction.CallbackContext _) => (isNavigating, holdStartTime, isHolding) = (false, 0f, false);
    private void OnNavigatePerformed(InputAction.CallbackContext _)
    {
        if (!isHolding)
        {
            MoveSlider(initialIncrement);
            lastMoveTime = Time.unscaledTime;
        }
    }
}