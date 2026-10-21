using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueAudioPresenter : MonoBehaviour
{
    [Serializable]
    private sealed class SpeakerBlipProfile
    {
        [Tooltip("Dialogue speaker role. Matching is case-insensitive.")]
        public string speakerRole;
        public AudioEvent blip;
        [Min(1)] public int playEveryCharacters = 2;
        [Min(0f)] public float minimumInterval = 0.035f;
    }

    [Header("Sources")]
    [SerializeField] private DialogueRunner runner;
    [SerializeField] private TextAnimator textAnimator;

    [Header("Fallback Voice")]
    [SerializeField] private AudioEvent defaultBlip;
    [SerializeField, Min(1)] private int defaultPlayEveryCharacters = 2;
    [SerializeField, Min(0f)] private float defaultMinimumInterval = 0.035f;

    [Header("Speaker Voices")]
    [SerializeField] private SpeakerBlipProfile[] speakerProfiles = Array.Empty<SpeakerBlipProfile>();

    [Header("Playback")]
    [Tooltip("Letters and digits produce blips. Enable this to include punctuation too.")]
    [SerializeField] private bool playOnPunctuation;
    [Tooltip("Stops the previous blip before the next, preventing speech sounds from stacking.")]
    [SerializeField] private bool replacePreviousBlip = true;

    [Serializable]
    private sealed class ExpressionSound
    {
        [Tooltip("Expression id used by the line's node (happy, sad, angry...). Case-insensitive.")]
        public string expressionId;
        public AudioEvent sound;
    }

    [Header("Expressions")]
    [Tooltip("A one-shot played when a line with this expression is shown, before its blips.")]
    [SerializeField] private ExpressionSound[] expressionSounds = Array.Empty<ExpressionSound>();

    private SpeakerBlipProfile activeProfile;
    private AudioHandle activeHandle;
    private int eligibleCharacterCount;
    private float lastPlayTime = float.NegativeInfinity;

    private void Awake()
    {
        if (runner == null)
            runner = GetComponentInParent<DialogueRunner>(true);

        if (textAnimator == null)
            textAnimator = GetComponentInChildren<TextAnimator>(true);
    }

    private void OnEnable()
    {
        if (runner != null)
        {
            runner.OnShowDialogue += HandleLineShown;
            runner.OnShowChoices += HandleChoicesShown;
            runner.OnDialogueEnd += HandleDialogueEnded;
        }

        if (textAnimator != null)
            textAnimator.CharacterRevealed += HandleCharacterRevealed;
    }

    private void OnDisable()
    {
        if (runner != null)
        {
            runner.OnShowDialogue -= HandleLineShown;
            runner.OnShowChoices -= HandleChoicesShown;
            runner.OnDialogueEnd -= HandleDialogueEnded;
        }

        if (textAnimator != null)
            textAnimator.CharacterRevealed -= HandleCharacterRevealed;

        StopActiveBlip();
        activeProfile = null;
    }

    private void HandleLineShown(DialogueLinePresentation line)
    {
        StopActiveBlip();
        eligibleCharacterCount = 0;
        lastPlayTime = float.NegativeInfinity;
        activeProfile = FindProfile(line != null ? line.SpeakerRole : string.Empty);
        PlayExpression(line != null ? line.ExpressionId : string.Empty);
    }

    private void PlayExpression(string expressionId)
    {
        if (string.IsNullOrWhiteSpace(expressionId)) return;
        for (int i = 0; i < expressionSounds.Length; i++)
        {
            ExpressionSound entry = expressionSounds[i];
            if (entry == null || entry.sound == null) continue;
            if (string.Equals(entry.expressionId, expressionId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Audio.Play(entry.sound);
                return;
            }
        }
    }

    private void HandleChoicesShown(System.Collections.Generic.IReadOnlyList<DialogueChoicePresentation> _)
    {
        StopActiveBlip();
        activeProfile = null;
    }

    private void HandleDialogueEnded()
    {
        StopActiveBlip();
        activeProfile = null;
    }

    private void HandleCharacterRevealed(char character, int _)
    {
        if (char.IsWhiteSpace(character)) return;
        if (!playOnPunctuation && !char.IsLetterOrDigit(character)) return;

        eligibleCharacterCount++;

        AudioEvent blip = activeProfile != null && activeProfile.blip != null
            ? activeProfile.blip
            : defaultBlip;
        if (blip == null) return;

        int stride = activeProfile != null
            ? Mathf.Max(1, activeProfile.playEveryCharacters)
            : Mathf.Max(1, defaultPlayEveryCharacters);
        if ((eligibleCharacterCount - 1) % stride != 0) return;

        float interval = activeProfile != null
            ? Mathf.Max(0f, activeProfile.minimumInterval)
            : Mathf.Max(0f, defaultMinimumInterval);
        if (Time.unscaledTime - lastPlayTime < interval) return;

        if (replacePreviousBlip && activeHandle.IsPlaying)
            activeHandle.Stop();

        activeHandle = Audio.Play(blip);
        lastPlayTime = Time.unscaledTime;
    }

    private SpeakerBlipProfile FindProfile(string speakerRole)
    {
        if (string.IsNullOrWhiteSpace(speakerRole) || speakerProfiles == null)
            return null;

        for (int i = 0; i < speakerProfiles.Length; i++)
        {
            SpeakerBlipProfile profile = speakerProfiles[i];
            if (profile != null && string.Equals(
                    profile.speakerRole?.Trim(),
                    speakerRole.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return profile;
        }

        return null;
    }

    private void StopActiveBlip()
    {
        if (activeHandle.IsPlaying)
            activeHandle.Stop();
        activeHandle = AudioHandle.Invalid;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        defaultPlayEveryCharacters = Mathf.Max(1, defaultPlayEveryCharacters);
        defaultMinimumInterval = Mathf.Max(0f, defaultMinimumInterval);

        if (speakerProfiles == null) return;
        for (int i = 0; i < speakerProfiles.Length; i++)
        {
            SpeakerBlipProfile profile = speakerProfiles[i];
            if (profile == null) continue;
            profile.playEveryCharacters = Mathf.Max(1, profile.playEveryCharacters);
            profile.minimumInterval = Mathf.Max(0f, profile.minimumInterval);
        }
    }
#endif
}
