using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An NES spawn point: its enemy appears when the spot scrolls onto the screen, disappears when the enemy goes off
/// screen, and the spot can bring it back once it has scrolled off and on again (killed or not). A screen transition
/// clears every spawned enemy; the new room's spawn points fill it as it comes into view.
/// Place it where the enemy should start (it's what the camera has to see), with the enemy prefab.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Tooltip("What appears here (an enemy prefab).")]
    public GameObject prefab;
    [Tooltip("It comes back after the spot has scrolled off screen and on again (killed or not). Off: once only.")]
    public bool respawn = true;
    [Tooltip("Tiles past the screen edge where the spot counts as on screen (0: as soon as it's inside the view).")]
    [Min(0f)] public float spawnMargin = 0f;
    [Tooltip("Tiles past the screen edge where the enemy is removed.")]
    [Min(0f)] public float despawnMargin = 1f;
    [Tooltip("Faces the nearest player when it appears (flips the enemy's x scale if needed).")]
    public bool facePlayer = true;

    private GameObject instance;
    private bool armed = true;        // may spawn the next time the spot is on screen
    private bool spent;               // spawned once with respawn off

    private static readonly List<EnemySpawner> all = new();

    // One subscription for all the spawners (the first in, the last out)
    private void OnEnable()
    {
        all.Add(this);
        if (all.Count == 1) RoomTransition.Started += OnTransitionStarted;
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (all.Count == 0) RoomTransition.Started -= OnTransitionStarted;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private void Update()
    {
        if (prefab == null || RoomTransition.AnyRunning) return;
        Vector2 spot = transform.position;

        if (instance != null)
        {
            // Wandered off screen: gone (it can come back once the spot is off screen too)
            if (!RoomCamera.Sees(instance.transform.position, despawnMargin * CameraRoom.Tile)) Despawn();
            return;
        }

        bool onScreen = RoomCamera.Sees(spot, spawnMargin * CameraRoom.Tile);
        if (!onScreen)
        {
            armed = !spent;
            return;
        }
        if (armed) Spawn();
    }

    private void Spawn()
    {
        armed = false;
        if (!respawn) spent = true;
        instance = Instantiate(prefab, transform.position, Quaternion.identity);

        if (facePlayer)
        {
            Megaman nearest = Players.Nearest(transform.position);
            if (nearest != null)
            {
                bool playerOnRight = nearest.transform.position.x > transform.position.x;
                Vector3 scale = instance.transform.localScale;
                bool facingRight = scale.x > 0f;
                if (facingRight != playerOnRight) instance.transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
            }
        }
    }

    private void Despawn()
    {
        if (instance != null) Destroy(instance);
        instance = null;
    }

    // NES: a screen transition clears the enemies; the new room's spots spawn theirs as they come into view
    private static void OnTransitionStarted(CameraRoom from, CameraRoom to, RoomTransition.Direction direction)
    {
        foreach (EnemySpawner spawner in all)
        {
            spawner.Despawn();
            if (!spawner.spent) spawner.armed = true;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
        Gizmos.DrawLine(transform.position + new Vector3(-0.4f, -0.4f), transform.position + new Vector3(0.4f, 0.4f));
    }
}
