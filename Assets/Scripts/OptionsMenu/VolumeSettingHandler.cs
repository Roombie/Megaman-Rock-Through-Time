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
    [Tooltip("Played while changing the value. Category and looping come from the event (music previews loop).")]
    public AudioEvent previewClip;
    private AudioHandle previewSound;

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

            // Music / voice previews stop when leaving the slider (only this preview, not the other sounds)
            if (previewClip != null && previewClip.category != SoundCategory.SFX && previewClip.category != SoundCategory.UI)
            {
                if (previewClip.category == SoundCategory.BGM) Audio.StopMusic();
                else if (previewSound.IsPlayingEvent(previewClip)) previewSound.Stop();
                previewSound = AudioHandle.Invalid;
            }
        }

        wasSelected = isSelectedNow;

    }

    private void PlayPreview()
    {
        if (previewClip == null || !Audio.IsReady)
            return;

        // Still playing (music previews play on the music sources, which Audio.IsPlaying doesn't see)
        if (previewSound.IsPlayingEvent(previewClip))
            return;

        previewSound = Audio.Play(previewClip);
    }

    public void OnValueChanged(float value)
    {
        SetText(value);

        if (Audio.IsReady)
            AudioSettingsBridge.Apply(settingType, value);

        Save();
        PlayPreview();
    }

    public void ApplyFromSaved()
    {
        float saved = PlayerPrefs.GetFloat(SettingsKeys.Get(settingType), 0.5f);
        slider.SetValueWithoutNotify(saved);
        SetText(saved);

        if (Audio.IsReady)
            AudioSettingsBridge.Apply(settingType, saved);
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