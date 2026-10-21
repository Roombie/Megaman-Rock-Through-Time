using UnityEngine;

/// <summary>
/// A slope you can place and resize: builds its own triangle (collider + mesh) from Width and Height, in game
/// pixels so it stays on the 16 px grid. It's on the Ground layer, so enemies and items rest on it like any ground.
/// Just a convenient way to place a slope: Mega Man (MegamanMover) walks on any walkable slope, whatever it's made
/// of (tiles with physics shapes, polygons, rotated boxes...).
/// Common NES-style angles: 16×16 (45°), 32×16 (about 27°).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(PolygonCollider2D), typeof(MeshFilter), typeof(MeshRenderer))]
public class SlopeBlock : MonoBehaviour
{
    public enum Rise { UpToTheRight, UpToTheLeft }

    public Rise rise = Rise.UpToTheRight;
    [Tooltip("Width in game pixels (16 = one tile).")]
    [Min(1)] public int widthPixels = 32;
    [Tooltip("Height in game pixels (16 = one tile).")]
    [Min(1)] public int heightPixels = 32;
    public Color color = new(0.35f, 0.4f, 0.55f);
    [Tooltip("Sprite material (the same one the level's sprites use).")]
    public Material material;
    public int sortingOrder;

    private const float PixelsPerUnit = 16f;
    private Mesh mesh;

    public float Width => widthPixels / PixelsPerUnit;
    public float Height => heightPixels / PixelsPerUnit;
    public float AngleDegrees => Mathf.Atan2(heightPixels, widthPixels) * Mathf.Rad2Deg;

    private void OnEnable() => Rebuild();

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (material == null) material = DefaultSpriteMaterial();
        // Rebuilding inside OnValidate isn't allowed to touch other components; do it right after
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
#endif
    }

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }

#if UNITY_EDITOR
    // The render pipeline's default sprite material (what a new SpriteRenderer gets)
    private static Material DefaultSpriteMaterial()
    {
        var probe = new GameObject("probe") { hideFlags = HideFlags.HideAndDontSave };
        var material = probe.AddComponent<SpriteRenderer>().sharedMaterial;
        DestroyImmediate(probe);
        return material;
    }
#endif

    /// <summary>Pivot is the bottom-left corner of the block.</summary>
    public void Rebuild()
    {
        float w = Width, h = Height;
        Vector2[] points = rise == Rise.UpToTheRight
            ? new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h) }
            : new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(0, h) };

        var collider = GetComponent<PolygonCollider2D>();
        collider.pathCount = 1;
        collider.SetPath(0, points);

        // Older versions added an edge collider on the tall side; not needed (the triangle has that side)
        if (TryGetComponent(out EdgeCollider2D oldSide))
        {
            if (Application.isPlaying) Destroy(oldSide);
            else DestroyImmediate(oldSide);
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "Slope", hideFlags = HideFlags.DontSave };
        }
        mesh.Clear();
        mesh.vertices = new Vector3[] { points[0], points[1], points[2] };
        mesh.triangles = new[] { 0, 2, 1 };   // clockwise as the camera sees it
        mesh.colors = new[] { color, color, color };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one };
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = GetComponent<MeshRenderer>();
#if UNITY_EDITOR
        if (material == null) material = DefaultSpriteMaterial();
#endif
        if (material != null) renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
    }
}
