using UnityEngine;

/// <summary>Air bubbles while Mega Man is fully under water.</summary>
[RequireComponent(typeof(Megaman))]
public class MegamanWater : MonoBehaviour
{
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField] private Transform bubblePos;

    private Megaman mm;
    private MegamanStats Stats => mm.Stats;

    private float timer;
    private GameObject currentBubble;
    private static readonly Vector2[] corners = new Vector2[4];

    private void Awake() => mm = GetComponent<Megaman>();

    public void HandleBubbles()
    {
        if (bubblePrefab == null || bubblePos == null) return;

        if (!mm.IsInWater() || !IsFullyInWater(mm.BoxCollider.bounds))
        {
            timer = 0f;
            return;
        }

        switch (Stats.bubbleType)
        {
            case MegamanStats.BubbleType.NextBubble:
                if (currentBubble == null) currentBubble = Instantiate(bubblePrefab, bubblePos.position, Quaternion.identity);
                break;

            case MegamanStats.BubbleType.TimerBased:
                timer += Time.deltaTime;
                if (timer >= Stats.bubbleSpawnInterval)
                {
                    Instantiate(bubblePrefab, bubblePos.position, Quaternion.identity);
                    timer = 0f;
                }
                break;
        }
    }

    private bool IsFullyInWater(Bounds bounds)
    {
        corners[0] = new Vector2(bounds.min.x, bounds.min.y);
        corners[1] = new Vector2(bounds.min.x, bounds.max.y);
        corners[2] = new Vector2(bounds.max.x, bounds.min.y);
        corners[3] = new Vector2(bounds.max.x, bounds.max.y);

        foreach (Vector2 point in corners)
            if (Physics2D.OverlapPoint(point, Stats.waterLayer) == null) return false;
        return true;
    }
}
