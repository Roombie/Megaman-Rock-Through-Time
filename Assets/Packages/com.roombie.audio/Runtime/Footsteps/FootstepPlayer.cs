using UnityEngine;

/// <summary>
/// Surface-aware footstep system.
/// Call Step() from an animation event or from your movement controller
/// whenever a foot contacts the ground.
///
/// ── How it works ─────────────────────────────────────────────────────────
///  Each frame a downward raycast detects the surface beneath the character.
///  A SurfaceTag component on the hit collider identifies the material.
///  The matching FootstepSet's AudioEvent is sent to AudioManager.Play(),
///  which handles clip randomisation and pitch micro-variation automatically.
///
/// ── Setup ────────────────────────────────────────────────────────────────
///  1. Add this component to your player/NPC root or foot bone.
///  2. Create one AudioEvent per surface (grass, stone, wood, etc.) and
///     populate the footstepSets array in the Inspector.
///  3. Make sure the ground objects have a SurfaceTag component, or
///     set defaultSurface to a reasonable fallback.
///  4. Call Step() from an Animation Event (preferred) or every N metres
///     of movement in your controller.
///
/// ── Animation Event setup ────────────────────────────────────────────────
///  In the Animation window select a footfall keyframe → Add Event →
///  Function: "Step"   (no parameters needed).
/// </summary>
public class FootstepPlayer : MonoBehaviour
{
    [Header("Footstep Sets")]
    [Tooltip("One entry per surface type. The Default entry is used as fallback.")]
    public FootstepSet[] footstepSets;

    [Header("Raycast")]
    [Tooltip("Origin offset from this transform's position (e.g. raise above ground).")]
    public Vector3 rayOriginOffset = new(0f, 0.1f, 0f);

    [Tooltip("Maximum raycast distance to detect the surface below.")]
    public float rayDistance = 0.4f;

    [Tooltip("LayerMask for the ground raycast. Set to 'Default' if unsure.")]
    public LayerMask groundLayers = ~0;

    [Header("Fallback")]
    [Tooltip("Surface type used when no SurfaceTag is found on the hit collider.")]
    public SurfaceType defaultSurface = SurfaceType.Default;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Trigger a footstep sound. Call this from an Animation Event on each
    /// footfall, or manually from your character controller.
    /// </summary>
    public void Step()
    {
        SurfaceType surface = DetectSurface(out Vector3 hitPoint);
        AudioEvent  evt     = GetEventForSurface(surface);

        if (evt == null)
        {
            Debug.LogWarning($"[FootstepPlayer] No AudioEvent found for surface '{surface}'. " +
                             "Add an entry in footstepSets.", this);
            return;
        }

        // Play at the actual foot contact point for correct 3D attenuation
        if (evt.spatialBlend > 0f)
            Audio.PlayAtPosition(evt, hitPoint);
        else
            Audio.Play(evt);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    SurfaceType DetectSurface(out Vector3 hitPoint)
    {
        Vector3 origin = transform.position + rayOriginOffset;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundLayers))
        {
            hitPoint = hit.point;

            SurfaceTag tag = hit.collider.GetComponent<SurfaceTag>();
            if (tag != null)
                return tag.GetSurfaceAt(hit.point);
        }
        else
        {
            hitPoint = transform.position;
        }

        return defaultSurface;
    }

    AudioEvent GetEventForSurface(SurfaceType surface)
    {
        if (footstepSets == null || footstepSets.Length == 0) return null;

        // First pass: exact match
        foreach (var set in footstepSets)
            if (set.surface == surface) return set.audioEvent;

        // Second pass: fallback to Default
        foreach (var set in footstepSets)
            if (set.surface == SurfaceType.Default) return set.audioEvent;

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (footstepSets == null || footstepSets.Length == 0)
        {
            Debug.LogWarning("FootstepPlayer: Footstep Sets is empty.", this);
            return;
        }

        System.Collections.Generic.HashSet<SurfaceType> surfaces =
            new System.Collections.Generic.HashSet<SurfaceType>();
        bool hasDefault = false;

        for (int i = 0; i < footstepSets.Length; i++)
        {
            FootstepSet set = footstepSets[i];
            if (!surfaces.Add(set.surface))
                Debug.LogWarning($"FootstepPlayer: surface '{set.surface}' is duplicated.", this);

            if (set.surface == SurfaceType.Default) hasDefault = true;
            if (set.audioEvent == null)
                Debug.LogWarning($"FootstepPlayer: '{set.surface}' has no AudioEvent.", this);
        }

        if (!hasDefault)
            Debug.LogWarning("FootstepPlayer: no Default fallback entry exists.", this);
    }
#endif

    // -------------------------------------------------------------------------
    // Editor visualisation
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + rayOriginOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + Vector3.down * rayDistance);
        Gizmos.DrawWireSphere(origin + Vector3.down * rayDistance, 0.03f);
    }
#endif
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Maps a SurfaceType to the AudioEvent that should play for footsteps.</summary>
[System.Serializable]
public struct FootstepSet
{
    public SurfaceType surface;

    [Tooltip("AudioEvent for this surface. Configure multiple clips + pitch variation " +
             "inside the AudioEvent asset for natural-sounding steps.")]
    public AudioEvent audioEvent;
}