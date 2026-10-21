using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The last checkpoint reached in the current level: where the players come back after dying (a respawn reloads the
/// scene, so it's kept here, outside it). Reached by entering a CameraRoom marked as a checkpoint. Forgotten when
/// another scene loads (a new level, the Title screen, Game Over).
/// </summary>
public static class Checkpoints
{
    private static string scene;
    private static Vector2 spawn;
    private static bool has;

    /// <summary>Where to start in this scene (false: no checkpoint reached here, start where the level says).</summary>
    public static bool TryGet(out Vector2 position)
    {
        position = spawn;
        return has && scene == SceneManager.GetActiveScene().name;
    }

    /// <summary>Reached a checkpoint: dying in this scene brings the players back to `position`.</summary>
    public static void Reach(Vector2 position)
    {
        scene = SceneManager.GetActiveScene().name;
        spawn = position;
        has = true;
    }

    public static void Clear() => has = false;

    // Another level (or a menu): its checkpoints start over. Reloading the same scene (a respawn) keeps them.
    private static void OnSceneLoaded(Scene loaded, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && loaded.name != scene) Clear();
    }

    // With Enter Play Mode Options (no domain reload) statics survive between play sessions: start clean
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clear();
        scene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}
