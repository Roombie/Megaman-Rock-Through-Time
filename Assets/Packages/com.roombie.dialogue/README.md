# Roombie Dialogue

Reusable node-graph dialogue/cinematic foundation.

Core intentionally does not depend on:
- Unity Localization
- PrimeTween
- AnimalWood
- any inventory/quest/game-state system

Projects integrate those concerns through interfaces/events and their own presenter/interaction layer.

## Variables in text

Lines, speaker names and choices may contain `{name}` placeholders. They are resolved after localization and before Text Animator, so TMP and animator tags are unaffected.

```text
Hi {playerName}, you owe me {coins|0} coins.
Use {{braces}} for literal braces.
```

- `{playerName}`: substituted, or left verbatim when unknown so mistakes are visible.
- `{playerName|Stranger}`: substituted, or the fallback text when unknown.

Values are looked up in this order:
1. `DialogueContext.SetValue` on the context passed to the runner (numbers use invariant formatting).
2. The `IDialogueVariableSource` assigned to `DialogueWindow`, for game-owned state.
3. The scene `DialogueVariableStore`, a simple key/value component like `DialogueFlagStore`.

Values are evaluated when a line is shown. If a value changes while the window is open, the speaker name and choices update at once and the line updates as soon as its typewriter has finished, so the reveal never restarts. A change is detected when `DialogueVariableStore` raises `Changed`, when the source assigned to `DialogueWindow` implements `IDialogueVariableNotifier`, or when you call `DialogueWindow.RefreshVariables()` yourself. Values set through `DialogueContext.SetValue` have no event, so call `RefreshVariables()` after changing them mid-line.
