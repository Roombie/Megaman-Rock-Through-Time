using System;
using TMPro;
using UnityEngine;

public class TextEffectWaveShake : BaseTextEffect
{
    public float WaveSpeed = 5f;
    public float WaveHeight = 8f;
    public float CharacterFrequency = 0.5f;

    private TMP_MeshInfo[] cachedMeshInfo;

    public override bool IsSettled => !isPlaying;

    protected override void OnPlay()
    {
        tmpText.ForceMeshUpdate();
        cachedMeshInfo = tmpText.textInfo.CopyMeshInfoVertexData();
    }

    protected override void OnStop()
    {
        cachedMeshInfo = null;
        tmpText?.ForceMeshUpdate(true, true);
    }

    private void Update()
    {
        if (!isPlaying || cachedMeshInfo == null || tmpText == null) return;

        int characterCount = tmpText.textInfo.characterCount;
        TMP_MeshInfo[] meshInfo = tmpText.textInfo.meshInfo;

        for (int i = 0; i < meshInfo.Length && i < cachedMeshInfo.Length; i++)
            Array.Copy(cachedMeshInfo[i].vertices, meshInfo[i].vertices, cachedMeshInfo[i].vertices.Length);

        for (int i = 0; i < characterCount; i++)
        {
            if (!tmpText.textInfo.characterInfo[i].isVisible) continue;
            float y = Mathf.Sin(Time.unscaledTime * WaveSpeed + i * CharacterFrequency) * WaveHeight;
            OffsetCharacterY(meshInfo, i, y);
        }

        animator.UpdateMesh();
    }
}
