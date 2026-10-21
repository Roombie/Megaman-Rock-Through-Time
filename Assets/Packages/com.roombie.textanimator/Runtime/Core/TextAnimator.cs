using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TextAnimator : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [Header("Typewriter")]
    [SerializeField, Min(0.01f)] private float charactersPerSecond = 35f;
    [SerializeField] private bool useUnscaledTime = true;
    [Header("Inline Effects")]
    [SerializeField] private bool animateInlineEffects = true;
    [Header("Pixel Text")]
    [SerializeField] private bool pixelPerfect;
    [SerializeField, Min(0.01f)] private float pixelStep = 1f;

    private BaseTextEffect[] componentEffects;
    private TextAnimationDocument document;
    private TMP_MeshInfo[] baseline;
    private Coroutine revealRoutine;
    private int revealedCharacters = int.MaxValue;
    private bool initialized;
    private bool settingText;
    private string sourceMarkup = string.Empty;
    private string renderedText = string.Empty;

    public event Action<string> MarkerReached;
    public event Action<char, int> CharacterRevealed;
    /// <summary>Raised when a typewriter reveal finishes on its own or through SkipReveal. Not raised when SetText replaces the text.</summary>
    public event Action RevealCompleted;
    public TMP_Text Text { get { EnsureInitialized(); return targetText; } }
    public string SourceMarkup => sourceMarkup;
    public bool PixelPerfect => pixelPerfect;
    public float PixelStep => Mathf.Max(0.01f, pixelStep);
    public bool IsRevealing => revealRoutine != null;
    public bool IsTypewriterPlaying => IsRevealing;

    protected virtual void Awake() => EnsureInitialized();
    protected virtual void OnEnable() { EnsureInitialized(); if (targetText != null) Invoke(nameof(StartAllEffects), 0f); }
    protected virtual void OnDisable() { CancelInvoke(nameof(StartAllEffects)); StopReveal(); StopAllEffects(); }
#if UNITY_EDITOR
    private void Reset() => ResolveTargetText();
#endif

    private void LateUpdate()
    {
        if (!initialized || targetText == null) return;
        if (!settingText && !string.Equals(targetText.text, renderedText, StringComparison.Ordinal))
        {
            sourceMarkup = targetText.text ?? string.Empty;
            ParseAndRender(false);
        }
        if (document != null && (IsRevealing || (animateInlineEffects && document.Spans.Count > 0)))
            ApplyInlineEffects();
    }

    public void EnsureInitialized()
    {
        if (initialized) return;
        ResolveTargetText();
        if (targetText == null) { Debug.LogError("[TextAnimator] No TMP_Text target was found.", this); enabled = false; return; }
        componentEffects = GetComponentsInChildren<BaseTextEffect>(true);
        initialized = true;
        sourceMarkup = targetText.text ?? string.Empty;
        ParseAndRender(false);
        foreach (BaseTextEffect effect in componentEffects)
            if (effect != null && effect.GetComponentInParent<TextAnimator>(true) == this) effect.Initialize();
    }

    private void ResolveTargetText()
    {
        if (targetText != null) return;
        targetText = GetComponent<TMP_Text>();
        if (targetText == null) targetText = GetComponentInChildren<TMP_Text>(true);
    }

    public void SetText(string text) => SetText(text, false);
    public void SetText(string text, bool animate)
    {
        EnsureInitialized();
        if (targetText == null) return;
        sourceMarkup = text ?? string.Empty;
        ParseAndRender(animate);
    }
    public void RefreshInlineTags() => ParseAndRender(false);

    private void ParseAndRender(bool animate)
    {
        if (targetText == null) return;
        StopReveal();
        document = TextAnimationTagParser.Parse(sourceMarkup, TextAnimationEffectRegistry.Tags);
        renderedText = document.RenderText;
        settingText = true;
        targetText.text = renderedText;
        targetText.SetAllDirty();
        targetText.ForceMeshUpdate(true, true);
        settingText = false;
        CaptureBaseline();
        targetText.maxVisibleCharacters = int.MaxValue;
        revealedCharacters = animate && document.CharacterCount > 0 ? 0 : int.MaxValue;
        if (animate && isActiveAndEnabled && document.CharacterCount > 0) revealRoutine = StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        int visible = 0, marker = 0;
        float accumulator = 0f;
        while (visible < document.CharacterCount)
        {
            while (marker < document.Markers.Count && document.Markers[marker].CharacterIndex <= visible)
            {
                TextAnimationMarker item = document.Markers[marker++];
                if (item.Type == TextAnimationMarkerType.Event) MarkerReached?.Invoke(item.StringValue);
                else if (item.NumberValue > 0f) yield return Wait(item.NumberValue);
            }
            TextAnimationSpan instant = FindSpan("instant", visible);
            if (instant != null)
            {
                int previousVisible = visible;
                visible = Mathf.Max(visible + 1, instant.EndIndexExclusive);
                revealedCharacters = visible;
                RaiseCharacterRevealed(previousVisible, visible);
                continue;
            }
            TextAnimationSpan speed = FindSpan("speed", visible);
            float multiplier = speed == null ? 1f : Mathf.Max(0.01f, speed.GetFloatAny(1f, "value", "multiplier"));
            accumulator += DeltaTime() * charactersPerSecond * multiplier;
            int count = Mathf.FloorToInt(accumulator);
            if (count > 0)
            {
                accumulator -= count;
                int previousVisible = visible;
                visible = Mathf.Min(document.CharacterCount, visible + count);
                revealedCharacters = visible;
                RaiseCharacterRevealed(previousVisible, visible);
            }
            yield return null;
        }
        revealedCharacters = int.MaxValue;
        ApplyInlineEffects();
        revealRoutine = null;
        RevealCompleted?.Invoke();
    }

    private IEnumerator Wait(float seconds)
    {
        while (seconds > 0f) { seconds -= DeltaTime(); yield return null; }
    }

    private void RaiseCharacterRevealed(int startInclusive, int endExclusive)
    {
        if (targetText == null || CharacterRevealed == null) return;
        int end = Mathf.Min(endExclusive, targetText.textInfo.characterCount);
        for (int i = Mathf.Max(0, startInclusive); i < end; i++)
            CharacterRevealed.Invoke(targetText.textInfo.characterInfo[i].character, i);
    }
    private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    private TextAnimationSpan FindSpan(string tag, int index)
    {
        if (document != null) foreach (TextAnimationSpan span in document.Spans) if (span.Tag == tag && span.Contains(index)) return span;
        return null;
    }
    public void SkipReveal()
    {
        if (targetText == null) return;
        bool wasRevealing = IsRevealing;
        StopReveal();
        revealedCharacters = int.MaxValue;
        ApplyInlineEffects();
        if (wasRevealing) RevealCompleted?.Invoke();
    }
    public bool SkipTypewriter() { if (!IsRevealing) return false; SkipReveal(); return true; }
    private void StopReveal() { if (revealRoutine == null) return; StopCoroutine(revealRoutine); revealRoutine = null; }

    // The rect the baseline was laid out in. Text set the moment its box appears (a speech bubble
    // just switched on) can be laid out before the layout gives the box its width: one letter per
    // line. The baseline is then copied onto the mesh every frame, so that layout would stick even
    // once the box is right; a new size takes a new baseline instead.
    private Vector2 baselineSize;

    private void CaptureBaseline()
    {
        targetText.ForceMeshUpdate(true, true);
        baseline = targetText.textInfo.CopyMeshInfoVertexData();
        baselineSize = targetText.rectTransform.rect.size;
    }

    private bool BaselineMatches()
    {
        if ((targetText.rectTransform.rect.size - baselineSize).sqrMagnitude > 0.0001f) return false;
        TMP_MeshInfo[] current = targetText.textInfo.meshInfo;
        if (baseline == null || current == null || baseline.Length != current.Length) return false;
        for (int i = 0; i < current.Length; i++)
            if (current[i].vertices == null || baseline[i].vertices == null || current[i].vertices.Length != baseline[i].vertices.Length) return false;
        return true;
    }
    private void ApplyInlineEffects()
    {
        if (!BaselineMatches()) CaptureBaseline();
        TMP_MeshInfo[] meshes = targetText.textInfo.meshInfo;
        int visibleLimit = Mathf.Min(revealedCharacters, targetText.textInfo.characterCount);
        for (int i = 0; i < meshes.Length; i++)
        {
            Array.Copy(baseline[i].vertices, meshes[i].vertices, baseline[i].vertices.Length);
            Array.Copy(baseline[i].colors32, meshes[i].colors32, baseline[i].colors32.Length);
        }
        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (animateInlineEffects)
        foreach (TextAnimationSpan span in document.Spans)
        {
            if (!TextAnimationEffectRegistry.TryGet(span.Tag, out IInlineTextEffect effect)) continue;
            int end = Mathf.Min(span.EndIndexExclusive, targetText.textInfo.characterCount, visibleLimit);
            for (int i = Mathf.Max(0, span.StartIndex); i < end; i++) effect.Apply(this, targetText, meshes, i, i - span.StartIndex, span.Length, now, span);
        }

        if (visibleLimit < targetText.textInfo.characterCount)
        {
            for (int i = Mathf.Max(0, visibleLimit); i < targetText.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = targetText.textInfo.characterInfo[i];
                if (!character.isVisible) continue;
                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (material < 0 || material >= meshes.Length || vertex + 3 >= meshes[material].vertices.Length) continue;
                Color32[] colors = meshes[material].colors32;
                if (colors == null || vertex + 3 >= colors.Length) continue;
                for (int v = 0; v < 4; v++)
                {
                    Color32 color = colors[vertex + v];
                    color.a = 0;
                    colors[vertex + v] = color;
                }
            }
        }
        targetText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    public void OffsetCharacter(TMP_MeshInfo[] meshes, int index, Vector3 offset)
    {
        if (!TryGetQuad(meshes, index, out int m, out int v)) return;
        offset = Quantize(offset);
        for (int i = 0; i < 4; i++) meshes[m].vertices[v + i] += offset;
    }
    public void RotateCharacter(TMP_MeshInfo[] meshes, int index, float degrees)
    {
        if (!TryGetQuad(meshes, index, out int m, out int v)) return;
        Vector3[] p = meshes[m].vertices; Vector3 center = (p[v] + p[v + 2]) * 0.5f; Quaternion q = Quaternion.Euler(0f, 0f, degrees);
        for (int i = 0; i < 4; i++) p[v + i] = center + q * (p[v + i] - center);
    }
    public void ScaleCharacter(TMP_MeshInfo[] meshes, int index, float scale)
    {
        if (!TryGetQuad(meshes, index, out int m, out int v)) return;
        Vector3[] p = meshes[m].vertices; Vector3 center = (p[v] + p[v + 2]) * 0.5f;
        for (int i = 0; i < 4; i++) p[v + i] = center + (p[v + i] - center) * scale;
    }
    private bool TryGetQuad(TMP_MeshInfo[] meshes, int index, out int material, out int vertex)
    {
        material = vertex = 0;
        if (index < 0 || index >= targetText.textInfo.characterCount) return false;
        TMP_CharacterInfo c = targetText.textInfo.characterInfo[index];
        if (!c.isVisible) return false;
        material = c.materialReferenceIndex; vertex = c.vertexIndex;
        return material >= 0 && material < meshes.Length && vertex + 3 < meshes[material].vertices.Length;
    }

    public void StartAllEffects()
    {
        EnsureInitialized(); if (targetText == null) return; CaptureBaseline();
        if (componentEffects != null) foreach (BaseTextEffect effect in componentEffects)
            if (effect != null && effect.enabled && effect.GetComponentInParent<TextAnimator>(true) == this) effect.PlayEffect();
    }
    public void StopAllEffects()
    {
        if (componentEffects != null) foreach (BaseTextEffect effect in componentEffects)
            if (effect != null && effect.GetComponentInParent<TextAnimator>(true) == this) effect.StopEffect();
    }
    public void UpdateMesh() { if (targetText != null) targetText.UpdateVertexData(TMP_VertexDataUpdateFlags.All); }
    public float Quantize(float value) { if (!pixelPerfect) return value; return Mathf.Round(value / PixelStep) * PixelStep; }
    public Vector3 Quantize(Vector3 value) => pixelPerfect ? new Vector3(Quantize(value.x), Quantize(value.y), Quantize(value.z)) : value;
}

public static class TextAnimationEffectRegistry
{
    private static readonly Dictionary<string, IInlineTextEffect> Effects = new Dictionary<string, IInlineTextEffect>(StringComparer.OrdinalIgnoreCase);
    static TextAnimationEffectRegistry()
    {
        Register(new OffsetEffect("wave", false));
        Register(new OffsetEffect("shake", true));
        Register(new OffsetEffect("bounce", false));
        Register(new RotateEffect());
        Register(new OffsetEffect("float", false));
        Register(new PulseEffect());
    }
    public static IEnumerable<string> Tags => Effects.Keys;
    public static void Register(IInlineTextEffect effect) { if (effect != null && !string.IsNullOrWhiteSpace(effect.Tag)) Effects[effect.Tag.Trim()] = effect; }
    public static bool Unregister(string tag) => !string.IsNullOrWhiteSpace(tag) && Effects.Remove(tag.Trim());
    public static bool TryGet(string tag, out IInlineTextEffect effect) => Effects.TryGetValue(tag ?? string.Empty, out effect);

    private sealed class OffsetEffect : IInlineTextEffect
    {
        private readonly string tag;
        private readonly bool random;
        public string Tag => tag;
        public OffsetEffect(string value, bool isRandom) { tag = value; random = isRandom; }
        public void Apply(TextAnimator a, TMP_Text t, TMP_MeshInfo[] m, int i, int local, int length, float time, TextAnimationSpan s)
        {
            float amp = s.GetFloatAny(tag == "float" ? 1f : 2f, "amp", "amplitude", "height", "strength");
            float speed = s.GetFloat("speed", tag == "float" ? 2f : 5f);
            if (random)
            {
                float seed = (i + 1) * 13.173f;
                a.OffsetCharacter(m, i, new Vector3((Mathf.PerlinNoise(seed, time * speed) - .5f) * 2f * amp, (Mathf.PerlinNoise(seed + 37.19f, time * speed) - .5f) * 2f * amp, 0f));
                return;
            }
            float y = Mathf.Sin(time * speed + local * s.GetFloatAny(tag == "float" ? .22f : .65f, "freq", "frequency")) * amp;
            if (tag == "bounce") y = Mathf.Abs(y);
            a.OffsetCharacter(m, i, new Vector3(0f, y, 0f));
        }
    }

    private sealed class RotateEffect : IInlineTextEffect
    {
        public string Tag => "wobble";
        public void Apply(TextAnimator a, TMP_Text t, TMP_MeshInfo[] m, int i, int local, int length, float time, TextAnimationSpan s)
        {
            a.RotateCharacter(m, i, Mathf.Sin(time * s.GetFloat("speed", 4f) + local * s.GetFloatAny(.45f, "freq", "frequency")) * s.GetFloatAny(4f, "angle", "amp", "amplitude"));
        }
    }

    private sealed class PulseEffect : IInlineTextEffect
    {
        public string Tag => "pulse";
        public void Apply(TextAnimator a, TMP_Text t, TMP_MeshInfo[] m, int i, int local, int length, float time, TextAnimationSpan s)
        {
            a.ScaleCharacter(m, i, 1f + Mathf.Sin(time * s.GetFloat("speed", 4f) + local * s.GetFloatAny(.2f, "freq", "frequency")) * s.GetFloatAny(.08f, "amount", "amp", "amplitude"));
        }
    }
}
