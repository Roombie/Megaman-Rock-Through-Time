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

    [Header("Preview")]
    public AudioClip previewClip;
    public SoundCategory previewCategory = SoundCategory.SFX;
    public bool loopPreview = false;

    private PlayerInputActions inputActions;

    private float holdStartTime;
    private float lastMoveTime;
    private int direction = 0;
    private bool wasSelected = false;

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
        // Movement input
        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            Vector2 nav = inputActions.UI.Navigate.ReadValue<Vector2>();
            int inputDir = nav.x > 0.5f ? 1 : nav.x < -0.5f ? -1 : 0;

            if (inputDir != 0)
            {
                if (direction == 0)
                {
                    direction = inputDir;
                    MoveSlider(initialIncrement * direction);
                    holdStartTime = 0f;
                    lastMoveTime = Time.unscaledTime;
                }
                else
                {
                    holdStartTime += Time.unscaledDeltaTime;

                    if (holdStartTime >= holdThreshold)
                    {
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
                direction = 0;
                holdStartTime = 0f;
            }
        }

        bool isSelectedNow = EventSystem.current.currentSelectedGameObject == slider.gameObject;

        if (wasSelected && !isSelectedNow)
        {
            Debug.Log("[VolumeSettingHandler] Deselected (via slider focus)");

            if (previewCategory != SoundCategory.SFX && AudioManager.Instance != null)
            {
                AudioManager.Instance.StopCategory(previewCategory);
            }
        }

        wasSelected = isSelectedNow;

    }

    private void PlayPreview()
    {
        if (previewClip == null || AudioManager.Instance == null)
            return;

        if (AudioManager.Instance.IsPlaying(previewClip))
            return;

        AudioManager.Instance.Play(previewClip, previewCategory, 1f, 1f, loopPreview);
    }

    public void OnValueChanged(float value)
    {
        SetText(value);

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetVolume(settingType, value);

        Save();
        PlayPreview();
    }

    public void ApplyFromSaved()
    {
        float saved = PlayerPrefs.GetFloat(SettingsKeys.Get(settingType), 0.5f);
        slider.SetValueWithoutNotify(saved);
        SetText(saved);

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetVolume(settingType, saved);
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
}