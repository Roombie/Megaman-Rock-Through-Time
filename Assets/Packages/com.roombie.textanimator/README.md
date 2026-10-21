# Roombie Text Animator

Reusable TextMeshPro text presentation package. It has no dependency on Dialogue, Localization, Input System, Audio, or tween libraries.

## Inline animation tags

Roombie tags are removed before TextMeshPro renders the text, while normal TMP tags are preserved.

```text
Hello <wave>there!</wave>
<shake amp=2 speed=20>STOP!</shake>
<b><bounce amp=2>Bold TMP text still works.</bounce></b>
<wobble angle=4>nervous</wobble>
<float amp=1 speed=2>floating</float>
<pulse amount=0.08>important</pulse>
```

Built-ins:
- `wave`: `amp`, `speed`, `freq`
- `shake`: `amp`, `speed`
- `bounce`: `amp`, `speed`, `freq`
- `wobble`: `angle`, `speed`, `freq`
- `float`: `amp`, `speed`, `freq`
- `pulse`: `amount`, `speed`, `freq`

Tags may be nested. Parameters are optional.

## Typewriter control tags

```text
Normal <speed=0.5>slower text</speed> normal again.
<pause=0.35>This starts after a pause.
<instant>This part appears instantly.</instant>
<event=footstep>Text after an event marker.
```

`speed` is a multiplier of the component's Characters Per Second. `pause` uses seconds. `event` invokes both a C# event and the Inspector UnityEvent.

Typical dialogue input behavior:
1. call `SetText(localizedLine, true)`;
2. if Interact is pressed while `IsTypewriterPlaying`, call `SkipTypewriter()`;
3. only advance the dialogue when `SkipTypewriter()` returns false.

`RevealCompleted` fires when a reveal ends naturally or through `SkipReveal()`, not when `SetText` replaces the text. Use it to defer a text swap until the typewriter is done.

## Pixel fonts

Enable `Pixel Perfect` and set `Pixel Step` (normally `1`). Character offsets and transform deltas are quantized so inline effects stay crisp with pixel fonts.

## Extending the tag system

Implement `IInlineTextEffect` and register it once:

```csharp
TextAnimationEffectRegistry.Register(new MyGlitchEffect());
```

The parser receives registered tag names automatically, so a custom tag can be added without editing `TextAnimator` or `TextAnimationTagParser`.

## Legacy component effects

`TextEffectFingerTap` and `TextEffectWaveShake` remain supported for standalone looping UI effects. Avoid combining a legacy `BaseTextEffect` with inline animated tags on the same TMP target because both edit the same mesh.
