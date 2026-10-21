# Roombie Dialogue Audio

Optional speech-audio adapter for Roombie Dialogue, Text Animator, and Audio.

The three base packages remain mutually independent. This adapter is the only assembly that references all three.

## Installation

Install or embed these packages together:

- `com.roombie.audio` 0.1.0+
- `com.roombie.dialogue` 0.1.0+
- `com.roombie.textanimator` 0.2.1+
- `com.roombie.dialogueaudio` 0.1.0+

## Scene setup

1. Add `DialogueAudioPresenter` beside the scene's dialogue UI or runner.
2. Assign its `DialogueRunner` and dialogue `TextAnimator`.
3. Create speech `AudioEvent` assets and set their category to `Voice`.
4. Assign a default blip and optional per-speaker mappings. Each role must match the graph's Speaker Role.

Recommended starting values are one blip every two eligible characters, a minimum interval of 0.035 seconds, punctuation disabled, and replacement of the previous blip enabled.
