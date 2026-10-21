using System.Collections;
using TMPro;
using UnityEngine;

public abstract class BaseTextEffect : MonoBehaviour
{
    protected TMP_Text tmpText;
    protected TextAnimator animator;
    protected bool isPlaying;
    private bool initialized;

    public virtual bool IsSettled => !isPlaying;

    protected virtual void Awake() => Initialize();

    public void Initialize()
    {
        if (initialized) return;

        animator = GetComponent<TextAnimator>();
        if (animator == null)
            animator = GetComponentInParent<TextAnimator>(true);

        if (animator == null)
        {
            Debug.LogError("[BaseTextEffect] A TextAnimator is required on this GameObject or a parent.", this);
            enabled = false;
            return;
        }

        animator.EnsureInitialized();
        tmpText = animator.Text;

        if (tmpText == null)
        {
            Debug.LogError("[BaseTextEffect] The TextAnimator has no TMP_Text target.", this);
            enabled = false;
            return;
        }

        OnInitialize();
        initialized = true;
    }

    protected virtual void OnInitialize() { }

    public void PlayEffect()
    {
        Initialize();
        if (!enabled || isPlaying || tmpText == null) return;
        isPlaying = true;
        OnPlay();
    }

    public void StopEffect()
    {
        if (!isPlaying) return;
        isPlaying = false;
        OnStop();
    }

    public virtual IEnumerator SettleToRest(float duration = 0.15f)
    {
        StopEffect();
        yield break;
    }

    protected abstract void OnPlay();
    protected abstract void OnStop();

    protected void OffsetCharacter(TMP_MeshInfo[] meshes, int charIndex, Vector3 offset)
    {
        if (tmpText == null || charIndex >= tmpText.textInfo.characterCount)
            return;

        TMP_CharacterInfo info = tmpText.textInfo.characterInfo[charIndex];
        if (!info.isVisible) return;

        offset = animator != null ? animator.Quantize(offset) : offset;

        int materialIndex = info.materialReferenceIndex;
        int vertexIndex = info.vertexIndex;
        Vector3[] vertices = meshes[materialIndex].vertices;

        for (int i = 0; i < 4; i++)
            vertices[vertexIndex + i] += offset;
    }

    protected void OffsetCharacterY(TMP_MeshInfo[] meshes, int charIndex, float yOffset) =>
        OffsetCharacter(meshes, charIndex, new Vector3(0f, yOffset, 0f));
}
