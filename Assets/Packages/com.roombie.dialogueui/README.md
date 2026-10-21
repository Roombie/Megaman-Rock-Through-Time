# Roombie Dialogue UI

Reusable DialogueRunner presentation using TextMeshPro, Unity UI, and Roombie Text Animator. It contains no game-state, generated-input, cancel-stack, audio, or localization implementation. Games drive `Open`, `Advance`, and `Close` through their own bridge.

`DialogueWindow` wraps the assigned `IDialogueTextResolver` in `InterpolatingTextResolver`, so `{name}` placeholders (see the Roombie Dialogue README) work for lines, speaker names and choices with or without localization.
