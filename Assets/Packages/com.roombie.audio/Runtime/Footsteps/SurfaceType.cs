using UnityEngine;

/// <summary>
/// Enum of all surface materials in the project.
/// Add new values here when you introduce a new terrain/material type.
/// </summary>
public enum SurfaceType
{
    Default,
    Grass,
    Dirt,
    Gravel,
    Stone,
    Wood,
    Metal,
    Sand,
    Snow,
    Water,
    Mud,
}

/// <summary>
/// Attach to any GameObject (floor, terrain, etc.) to tag its surface type.
/// FootstepPlayer reads this via a downward raycast.
///
/// For Terrain objects, assign the terrain reference and fill in
/// terrainLayers so the system maps each TerrainLayer to a SurfaceType.
/// </summary>
public class SurfaceTag : MonoBehaviour
{
    [Tooltip("Surface type for flat/simple colliders (floors, platforms, walls).")]
    public SurfaceType surfaceType = SurfaceType.Default;

    // -------------------------------------------------------------------------
    // Terrain support (optional)
    // -------------------------------------------------------------------------

    [Header("Terrain (optional)")]
    [Tooltip("Reference to the Unity Terrain on this GameObject (if any).")]
    public Terrain terrain;

    [Tooltip("Maps each TerrainLayer index to a SurfaceType. " +
             "The index matches your terrain's TerrainLayer list order.")]
    public TerrainLayerSurface[] terrainLayers;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (terrainLayers == null || terrainLayers.Length == 0) return;

        int layerCount = terrain != null && terrain.terrainData != null
            ? terrain.terrainData.terrainLayers.Length
            : -1;
        System.Collections.Generic.HashSet<int> indices =
            new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            int index = terrainLayers[i].layerIndex;
            if (!indices.Add(index))
                Debug.LogWarning($"SurfaceTag: terrain layer index {index} is duplicated.", this);

            if (index < 0 || (layerCount >= 0 && index >= layerCount))
            {
                Debug.LogWarning(
                    $"SurfaceTag: terrain layer index {index} is outside the valid range " +
                    $"0..{Mathf.Max(0, layerCount - 1)}.", this);
            }
        }
    }
#endif

    /// <summary>
    /// Returns the dominant SurfaceType at the given world position.
    /// Falls back to surfaceType if this is not a terrain object.
    /// </summary>
    public SurfaceType GetSurfaceAt(Vector3 worldPosition)
    {
        if (terrain == null || terrainLayers == null || terrainLayers.Length == 0)
            return surfaceType;

        int dominantLayer = GetDominantTerrainLayer(worldPosition);

        foreach (var entry in terrainLayers)
            if (entry.layerIndex == dominantLayer)
                return entry.surfaceType;

        return surfaceType;
    }

    int GetDominantTerrainLayer(Vector3 worldPosition)
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        int mapX = Mathf.RoundToInt((worldPosition.x - terrainPos.x) / data.size.x * data.alphamapWidth);
        int mapZ = Mathf.RoundToInt((worldPosition.z - terrainPos.z) / data.size.z * data.alphamapHeight);

        mapX = Mathf.Clamp(mapX, 0, data.alphamapWidth  - 1);
        mapZ = Mathf.Clamp(mapZ, 0, data.alphamapHeight - 1);

        float[,,] alphaMaps = data.GetAlphamaps(mapX, mapZ, 1, 1);

        int   dominant     = 0;
        float maxWeight    = 0f;

        for (int i = 0; i < alphaMaps.GetLength(2); i++)
        {
            if (alphaMaps[0, 0, i] > maxWeight)
            {
                maxWeight = alphaMaps[0, 0, i];
                dominant  = i;
            }
        }

        return dominant;
    }
}

/// <summary>Pairs a TerrainLayer index with a SurfaceType.</summary>
[System.Serializable]
public struct TerrainLayerSurface
{
    [Tooltip("Index of this TerrainLayer in the terrain's layer list.")]
    public int         layerIndex;
    public SurfaceType surfaceType;
}