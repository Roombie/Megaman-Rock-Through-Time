using UnityEngine;

/// <summary>
/// Stacks its active children top to bottom by the size of what they actually show, `spacing` px apart,
/// on whole pixels. A section that grows (more weapons, more items) pushes the ones below it down.
/// Children should be anchored to this object's centre.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PixelStack : MonoBehaviour
{
    public enum Alignment { KeepX, Left, Centre }

    public int spacing = 8;
    public Alignment alignment = Alignment.KeepX;

    public void Rebuild()
    {
        var self = (RectTransform)transform;
        float top = 0f;
        bool first = true;

        for (int i = 0; i < self.childCount; i++)
        {
            var child = self.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf) continue;
            if (!PixelLayout.TryGetContentBounds(child, self, out Rect b)) continue;

            if (first) { top = Mathf.Round(b.yMax); first = false; }

            float dx = alignment switch
            {
                Alignment.Left => -b.xMin,
                Alignment.Centre => -b.center.x,
                _ => 0f,
            };
            float dy = top - b.yMax;
            PixelLayout.Nudge(child, dx, dy);

            top = Mathf.Round(b.yMin + dy) - spacing;
        }
    }
}
