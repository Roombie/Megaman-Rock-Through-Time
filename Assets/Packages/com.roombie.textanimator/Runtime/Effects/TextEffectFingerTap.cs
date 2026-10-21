using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Reusable staggered character bounce. No tween-package dependency.
/// </summary>
public class TextEffectFingerTap : BaseTextEffect
{
    public float BounceHeight = 15f;
    public float StaggerDelay = 0.08f;
    public float CharacterDuration = 0.5f;
    public float PauseBetweenWaves = 0f;

    private float[] characterOffsets;
    private TMP_MeshInfo[] cachedMeshInfo;
    private Coroutine playRoutine;

    public override bool IsSettled => !isPlaying || playRoutine == null;

    protected override void OnPlay()
    {
        // Cache text info immediately so cachedMeshInfo is never null when Update or Settle executes
        if (tmpText != null)
        {
            tmpText.ForceMeshUpdate(true, true);
            cachedMeshInfo = tmpText.textInfo.CopyMeshInfoVertexData();
            characterOffsets = new float[tmpText.textInfo.characterCount];
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    protected override void OnStop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        characterOffsets = null;
        cachedMeshInfo = null;
        tmpText?.ForceMeshUpdate(true, true);
    }

    public override IEnumerator SettleToRest(float duration = 0.15f)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (characterOffsets == null || cachedMeshInfo == null || duration <= 0f)
        {
            StopEffect();
            yield break;
        }

        float[] start = (float[])characterOffsets.Clone();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            
            // Protect against dynamic text character length mismatches
            int length = Mathf.Min(start.Length, characterOffsets.Length);
            for (int i = 0; i < length; i++)
                characterOffsets[i] = Mathf.Lerp(start[i], 0f, t);
                
            yield return null;
        }

        if (characterOffsets != null)
            Array.Clear(characterOffsets, 0, characterOffsets.Length);

        yield return null;
        StopEffect();
    }

    private IEnumerator PlayRoutine()
    {
        yield return null;

        tmpText.ForceMeshUpdate(true, true);
        cachedMeshInfo = tmpText.textInfo.CopyMeshInfoVertexData();
        characterOffsets = new float[tmpText.textInfo.characterCount];

        while (isPlaying)
        {
            float total = Mathf.Max(0.01f, CharacterDuration) +
                          Mathf.Max(0, characterOffsets.Length - 1) * Mathf.Max(0f, StaggerDelay);
            float elapsed = 0f;

            while (elapsed < total && isPlaying)
            {
                elapsed += Time.unscaledDeltaTime;

                for (int i = 0; i < characterOffsets.Length; i++)
                {
                    float local = elapsed - i * StaggerDelay;
                    if (local < 0f || local > CharacterDuration)
                    {
                        characterOffsets[i] = 0f;
                        continue;
                    }

                    float t = Mathf.Clamp01(local / Mathf.Max(0.01f, CharacterDuration));
                    characterOffsets[i] = Mathf.Sin(t * Mathf.PI) * BounceHeight;
                }

                yield return null;
            }

            if (characterOffsets != null)
                Array.Clear(characterOffsets, 0, characterOffsets.Length);

            if (PauseBetweenWaves > 0f)
                yield return new WaitForSecondsRealtime(PauseBetweenWaves);
            else
                yield return null;
        }

        playRoutine = null;
    }

    private void Update()
    {
        if (characterOffsets == null || cachedMeshInfo == null || tmpText == null)
            return;

        TMP_MeshInfo[] meshInfo = tmpText.textInfo.meshInfo;
        for (int i = 0; i < meshInfo.Length && i < cachedMeshInfo.Length; i++)
        {
            if (meshInfo[i].vertices != null && cachedMeshInfo[i].vertices != null)
            {
                Array.Copy(cachedMeshInfo[i].vertices, meshInfo[i].vertices, 
                    Mathf.Min(cachedMeshInfo[i].vertices.Length, meshInfo[i].vertices.Length));
            }
        }

        int count = Mathf.Min(tmpText.textInfo.characterCount, characterOffsets.Length);
        for (int i = 0; i < count; i++)
            OffsetCharacterY(meshInfo, i, characterOffsets[i]);

        animator?.UpdateMesh();
    }
}