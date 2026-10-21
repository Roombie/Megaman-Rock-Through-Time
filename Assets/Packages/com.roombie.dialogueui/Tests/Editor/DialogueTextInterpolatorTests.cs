using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Edit Mode coverage for {name} placeholder substitution in dialogue text: known and unknown
/// variables, fallbacks, brace escapes, rich-text passthrough, invariant number formatting through
/// DialogueContext, and first-hit-wins ordering in CompositeDialogueVariableSource.
/// </summary>
public class DialogueTextInterpolatorTests
{
    private sealed class DictionarySource : IDialogueVariableSource
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        public DictionarySource Set(string key, string value) { values[key] = value; return this; }
        public bool TryGetVariable(string name, out string value) => values.TryGetValue(name, out value);
    }

    private DictionarySource source;

    [SetUp]
    public void SetUp()
    {
        source = new DictionarySource().Set("playerName", "Guille").Set("coins", "12");
    }

    [Test]
    public void ReplacesKnownPlaceholders()
    {
        Assert.AreEqual("Hi Guille, you have 12 coins.",
            DialogueTextInterpolator.Format("Hi {playerName}, you have {coins} coins.", source));
    }

    [Test]
    public void PlaceholderNameIsTrimmedAndCaseInsensitive()
    {
        Assert.AreEqual("Guille", DialogueTextInterpolator.Format("{ PLAYERNAME }", source));
    }

    [Test]
    public void UnknownPlaceholderStaysVerbatim()
    {
        Assert.AreEqual("Hi {nobody}.", DialogueTextInterpolator.Format("Hi {nobody}.", source));
    }

    [Test]
    public void UnknownPlaceholderUsesFallback()
    {
        Assert.AreEqual("Hi Stranger.", DialogueTextInterpolator.Format("Hi {nobody|Stranger}.", source));
        Assert.AreEqual("Hi Guille.", DialogueTextInterpolator.Format("Hi {playerName|Stranger}.", source));
        Assert.AreEqual("Hi .", DialogueTextInterpolator.Format("Hi {nobody|}.", source));
    }

    [Test]
    public void NullSourceLeavesPlaceholdersAndUsesFallbacks()
    {
        Assert.AreEqual("{a} b", DialogueTextInterpolator.Format("{a} {b|b}", null));
    }

    [Test]
    public void DoubleBracesAreLiteral()
    {
        Assert.AreEqual("{playerName} is Guille", DialogueTextInterpolator.Format("{{playerName}} is {playerName}", source));
    }

    [Test]
    public void UnclosedBraceIsKept()
    {
        Assert.AreEqual("oops {playerName", DialogueTextInterpolator.Format("oops {playerName", source));
        Assert.AreEqual("{}", DialogueTextInterpolator.Format("{}", source));
    }

    [Test]
    public void RichTextAndAnimatorTagsPassThrough()
    {
        const string input = "<b><wave amp=2>{playerName}</wave></b> <pause=0.2>done";
        Assert.AreEqual("<b><wave amp=2>Guille</wave></b> <pause=0.2>done", DialogueTextInterpolator.Format(input, source));
    }

    [Test]
    public void TextWithoutBracesIsReturnedUnchanged()
    {
        Assert.AreEqual("plain", DialogueTextInterpolator.Format("plain", source));
        Assert.AreEqual(string.Empty, DialogueTextInterpolator.Format(null, source));
    }

    [Test]
    public void DialogueContextFormatsValuesInvariantly()
    {
        var context = new DialogueContext();
        context.SetValue("coins", 1.5f);
        context.SetValue("name", "Guille");
        context.SetValue("count", 3);

        Assert.AreEqual("1.5 Guille 3", DialogueTextInterpolator.Format("{Coins} {name} {count}", context));
    }

    private sealed class FakeBinding : IDialogueTextBinding
    {
        public string Value { get; private set; }
        public bool Disposed { get; private set; }
        public event Action<string> Changed;
        public FakeBinding(string value) { Value = value; }
        public void Raise(string value) { Value = value; Changed?.Invoke(value); }
        public void Dispose() { Disposed = true; }
    }

    private sealed class FakeResolver : IDialogueTextResolver
    {
        public FakeBinding Last { get; private set; }
        public IDialogueTextBinding Bind(string tableName, string key) => Last = new FakeBinding("Hola {playerName}");
    }

    [Test]
    public void InterpolatedBindingRefreshTracksVariableChangesWithoutRaisingChanged()
    {
        var resolver = new InterpolatingTextResolver(null, source);
        IDialogueTextBinding binding = resolver.Bind(string.Empty, "Hi {playerName}");
        int changedCalls = 0;
        binding.Changed += _ => changedCalls++;
        var refreshable = (IDialogueRefreshableBinding)binding;

        Assert.AreEqual("Hi Guille", binding.Value);
        Assert.IsFalse(refreshable.Refresh(), "unchanged variables should report no change");

        source.Set("playerName", "Max");
        Assert.IsTrue(refreshable.Refresh());
        Assert.AreEqual("Hi Max", binding.Value);
        Assert.AreEqual(0, changedCalls);
        binding.Dispose();
    }

    [Test]
    public void InterpolatedBindingReformatsWhenLocalizedTextChanges()
    {
        var inner = new FakeResolver();
        var resolver = new InterpolatingTextResolver(inner, source);
        IDialogueTextBinding binding = resolver.Bind("table", "key");
        string received = null;
        binding.Changed += value => received = value;

        Assert.AreEqual("Hola Guille", binding.Value);
        inner.Last.Raise("Bye {playerName}");
        Assert.AreEqual("Bye Guille", received);
        Assert.AreEqual("Bye Guille", binding.Value);

        binding.Dispose();
        Assert.IsTrue(inner.Last.Disposed);
    }

    [Test]
    public void CompositeSourceReturnsFirstHit()
    {
        var first = new DictionarySource().Set("playerName", "First");
        var second = new DictionarySource().Set("playerName", "Second").Set("coins", "7");
        var composite = new CompositeDialogueVariableSource(() => null, () => first, () => second);

        Assert.AreEqual("First 7", DialogueTextInterpolator.Format("{playerName} {coins}", composite));
        Assert.IsFalse(composite.TryGetVariable("missing", out _));
    }
}
