using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class VolumeSettingHandler : MonoBehaviour, ISettingHandler, IDeselectHandler
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

    private PlayerInputActions inputActions;

    private float holdStartTime;
    private float lastMoveTime;
    private bool isHolding;
    private int direction = 0;

    public SettingType SettingType => settingType;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
    
    private void Start()
    {
        slider.onValueChanged.AddListener(OnValueChanged);
        ApplyFromSaved();
    }

    private void Update()
    {
        if (EventSystem.current.currentSelectedGameObject != gameObject)
            return;

        Vector2 nav = inputActions.UI.Navigate.ReadValue<Vector2>();
        int inputDir = nav.x > 0.5f ? 1 : nav.x < -0.5f ? -1 : 0;

        if (inputDir != 0)
        {
            if (direction == 0)
            {
                // New press (TAP)
                direction = inputDir;
                MoveSlider(initialIncrement * direction);
                holdStartTime = 0f;
                lastMoveTime = Time.unscaledTime;
                isHolding = false;
            }
            else
            {
                // Holding
                holdStartTime += Time.unscaledDeltaTime;

                if (holdStartTime >= holdThreshold)
                {
                    isHolding = true;

                    if (Time.unscaledTime - lastMoveTime >= moveCooldown)
                    {
                        MoveSlider(holdIncrement * direction);
                        lastMoveTime = Time.unscaledTime;
                    }
                }
            }
        }
        else if (direction != 0)
        {
            // Released
            direction = 0;
            isHolding = false;
            holdStartTime = 0f;
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
        int intVolume = Mathf.RoundToInt(volume * 100);
        valueText.text = intVolume.ToString("00");

        valueText.color = intVolume switch
        {
            0 => minVolumeColor,
            100 => maxVolumeColor,
            _ => Color.white
        };
    }

    private void MoveSlider(float amount)
    {
        float newValue = Mathf.Clamp(slider.value + amount, slider.minValue, slider.maxValue);
        newValue = Mathf.Round(newValue * 100f) / 100f;
        slider.value = newValue;
        SetText(newValue);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        direction = 0;
        isHolding = false;
        holdStartTime = 0f;
        EventSystem.current.sendNavigationEvents = true;
    }
}