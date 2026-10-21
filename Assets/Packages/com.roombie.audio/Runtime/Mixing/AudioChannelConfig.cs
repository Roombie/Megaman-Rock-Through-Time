using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Defines every audio output channel for this project.
///
/// ── What this replaces ────────────────────────────────────────────────────
///  Previously AudioManager had hardcoded fields (sfxGroup, bgmGroup, etc.)
///  and the Output Manager had a hardcoded CHANNELS array.  This asset
///  is the single source of truth — add or remove channels here and both
///  AudioManager and the Output Manager update automatically.
///
/// ── Setup ─────────────────────────────────────────────────────────────────
///  1. Create one asset: Assets → Create → Audio → Channel Config
///  2. Fill in channels — Master first, then one per mixer group.
///  3. Assign the asset to AudioManager.channelConfig in the Inspector.
///  4. Open Window → Audio → Output Manager — all channels appear there.
///
/// ── Channel fields ────────────────────────────────────────────────────────
///  label       — display name shown in the Output Manager UI
///  mixerGroup  — the AudioMixerGroup this channel routes to (null = Master)
///  volumeParam — exposed parameter name in the AudioMixer (e.g. "BGMVolume")
///  category    — the SoundCategory that routes to this group; None = Master
///  isMaster    — tick for the one Master channel (reads AudioMixer, not group)
///  accentColor — card colour in the Output Manager
///  icon        — single Unicode glyph shown on the card (♛ ★ ♪ ◈ ❖ etc.)
/// </summary>
/// <summary>
/// Which SoundCategory an output channel handles.
/// None = Master channel (not tied to a SoundCategory).
/// </summary>
public enum ChannelCategory
{
    None  = -1,   // Master — not a SoundCategory
    SFX   = 0,
    BGM   = 1,
    UI    = 2,
    Voice = 3,
}

[CreateAssetMenu(menuName = "Audio/Channel Config", fileName = "AudioChannelConfig")]
public class AudioChannelConfig : ScriptableObject
{
    [Serializable]
    public class ChannelEntry
    {
        [Tooltip("Name shown on the card in the Output Manager.")]
        public string            label       = "New Channel";

        [Tooltip("Tick for the Master channel only. Shows the AudioMixer asset instead of a group.")]
        public bool              isMaster    = false;

        [Tooltip("The AudioMixerGroup this channel routes to. Leave null for the Master channel.")]
        public AudioMixerGroup   mixerGroup;

        [Tooltip("The exposed parameter name in the AudioMixer for volume control (e.g. 'BGMVolume').")]
        public string            volumeParam = "";

        [Tooltip("Which SoundCategory plays through this channel. SFX is the default for unlisted categories.")]
        public ChannelCategory   category    = ChannelCategory.SFX;

        [Tooltip("Card accent colour shown in the Output Manager.")]
        public Color             accentColor = new Color(0.30f, 0.55f, 0.90f, 1f);

        [Tooltip("Unicode glyph shown on the card. Keep it a single character.")]
        public string            icon        = "♪";
    }


    [Tooltip("Define one entry per output channel. Put Master first.")]
    public List<ChannelEntry> channels = new List<ChannelEntry>
    {
        new ChannelEntry
        {
            label       = "Master",
            isMaster    = true,
            mixerGroup  = null,
            volumeParam = "MasterVolume",
            category    = ChannelCategory.None,
            accentColor = new Color(0.70f, 0.50f, 0.90f, 1f),
            icon        = "♛",
        },
        new ChannelEntry
        {
            label       = "SFX",
            isMaster    = false,
            volumeParam = "SFXVolume",
            category    = ChannelCategory.SFX,
            accentColor = new Color(0.25f, 0.65f, 0.35f, 1f),
            icon        = "★",
        },
        new ChannelEntry
        {
            label       = "BGM",
            isMaster    = false,
            volumeParam = "BGMVolume",
            category    = ChannelCategory.BGM,
            accentColor = new Color(0.30f, 0.55f, 0.90f, 1f),
            icon        = "♪",
        },
        new ChannelEntry
        {
            label       = "UI",
            isMaster    = false,
            volumeParam = "UIVolume",
            category    = ChannelCategory.UI,
            accentColor = new Color(0.90f, 0.75f, 0.20f, 1f),
            icon        = "◈",
        },
        new ChannelEntry
        {
            label       = "Voice",
            isMaster    = false,
            volumeParam = "VoiceVolume",
            category    = ChannelCategory.Voice,
            accentColor = new Color(0.85f, 0.40f, 0.25f, 1f),
            icon        = "❖",
        },
    };

    // -------------------------------------------------------------------------
    // Convenience lookups used by AudioManager at runtime
    // -------------------------------------------------------------------------

    /// <summary>Returns the AudioMixerGroup configured for a SoundCategory, or null.</summary>
    public AudioMixerGroup GetGroup(SoundCategory category)
    {
        var cat = SoundCategoryToChannelCategory(category);
        foreach (var ch in channels)
            if (ch.category == cat && !ch.isMaster)
                return ch.mixerGroup;
        return null;
    }

    /// <summary>Returns the first AudioMixer found across all assigned groups.</summary>
    public AudioMixer GetMixer()
    {
        foreach (var ch in channels)
            if (ch.mixerGroup != null)
                return ch.mixerGroup.audioMixer;
        return null;
    }

    /// <summary>Returns the volume parameter name for a SoundCategory.</summary>
    public string GetVolumeParam(SoundCategory category)
    {
        var cat = SoundCategoryToChannelCategory(category);
        foreach (var ch in channels)
            if (ch.category == cat)
                return ch.volumeParam;
        return null;
    }


    /// <summary>Returns the exposed mixer parameter for a logical audio bus.</summary>
    public string GetVolumeParam(AudioBus bus)
    {
        if (bus == AudioBus.Master)
            return GetMasterChannel()?.volumeParam;

        return bus switch
        {
            AudioBus.BGM   => GetVolumeParam(SoundCategory.BGM),
            AudioBus.UI    => GetVolumeParam(SoundCategory.UI),
            AudioBus.Voice => GetVolumeParam(SoundCategory.Voice),
            _              => GetVolumeParam(SoundCategory.SFX),
        };
    }

    /// <summary>Returns the Master channel entry, or null if none is marked isMaster.</summary>
    public ChannelEntry GetMasterChannel()
    {
        foreach (var ch in channels)
            if (ch.isMaster) return ch;
        return null;
    }

    static ChannelCategory SoundCategoryToChannelCategory(SoundCategory sc) => sc switch
    {
        SoundCategory.BGM   => ChannelCategory.BGM,
        SoundCategory.UI    => ChannelCategory.UI,
        SoundCategory.Voice => ChannelCategory.Voice,
        _                   => ChannelCategory.SFX,
    };

#if UNITY_EDITOR
    // Warn in the Inspector if any non-master channel has no mixer group assigned.
    void OnValidate()
    {
        if (channels == null) return;
        foreach (var ch in channels)
        {
            if (ch == null || ch.isMaster) continue;
            if (ch.mixerGroup == null)
                UnityEngine.Debug.LogWarning(
                    $"[AudioChannelConfig] Channel '{ch.label}' has no AudioMixerGroup assigned. "
                    + "Audio will not route correctly until a group is set.", this);
            if (string.IsNullOrEmpty(ch.volumeParam))
                UnityEngine.Debug.LogWarning(
                    $"[AudioChannelConfig] Channel '{ch.label}' has no volume parameter name. "
                    + "SetVolume() will have no effect for this channel.", this);
        }
    }
#endif
}