# Roombie Audio

Reusable Unity audio package extracted from AnimalWood.

## Core responsibilities

- `AudioEvent`-driven playback
- pooled `AudioSource` management
- per-category voice limits
- music crossfades
- positional playback and distance culling
- mixer routing through `AudioChannelConfig`
- runtime volume buses through `AudioBus`
- pause, resume, stop and mute controls
- audio zones, emitters, ambient players, footsteps, occlusion, ducking and debug helpers
- editor tools for creating, browsing and routing audio events

## Deliberate non-responsibilities

The package does **not** know how a game's settings are stored. It does not depend on
`SettingType`, `SettingsStore`, AnimalWood, localization, dialogue, PrimeTween or any
other game-specific system.

A host game should load its saved settings and call:

```csharp
Audio.SetVolume(AudioBus.Master, master);
Audio.SetVolume(AudioBus.BGM, music);
Audio.SetVolume(AudioBus.SFX, sfx);
Audio.SetVolume(AudioBus.UI, ui);
Audio.SetVolume(AudioBus.Voice, voice);
```

## Optional integrations

Addressables support is intentionally not part of the core package in this first
refactor so projects do not gain a mandatory Addressables dependency. AnimalWood's
existing Addressables audio scripts are kept in its integration layer.
