using TMPro;

/// <summary>Implement and register this interface to add a new inline text tag without changing TextAnimator.</summary>
public interface IInlineTextEffect
{
    string Tag { get; }

    void Apply(
        TextAnimator animator,
        TMP_Text text,
        TMP_MeshInfo[] meshInfo,
        int characterIndex,
        int localIndex,
        int spanLength,
        float time,
        TextAnimationSpan span);
}
