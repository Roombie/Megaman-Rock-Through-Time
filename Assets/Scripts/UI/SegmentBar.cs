using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NES-style energy bar drawn on the pixel grid: `segments` units of `segmentWidth` px with `gap` px between them.
/// The segment images are created at runtime inside this RectTransform (left to right, vertically centred),
/// so only the bar's position, colours and sizes need editing in the scene.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SegmentBar : MonoBehaviour
{
    public int segments = 28;
    public int segmentWidth = 1;
    public int gap = 1;
    public int segmentHeight = 6;
    [Tooltip("Space between the bar's left edge and the first segment.")]
    public int padding = 1;
    public Color fillColor = Color.white;
    public Color emptyColor = new Color32(0x3C, 0x3C, 0x3C, 0xFF);

    private Image[] images;

    private void EnsureSegments()
    {
        if (images != null && images.Length == segments) return;

        var rect = (RectTransform)transform;
        for (int i = rect.childCount - 1; i >= 0; i--)
        {
            if (rect.GetChild(i).name == "Segment")
                Destroy(rect.GetChild(i).gameObject);
        }

        images = new Image[segments];
        for (int i = 0; i < segments; i++)
        {
            var go = new GameObject("Segment", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            var seg = (RectTransform)go.transform;
            seg.SetParent(rect, false);
            seg.anchorMin = seg.anchorMax = new Vector2(0f, 0.5f);
            seg.pivot = new Vector2(0f, 0.5f);
            seg.sizeDelta = new Vector2(segmentWidth, segmentHeight);
            // Even heights centre on a whole pixel; odd heights need the half pixel
            seg.anchoredPosition = new Vector2(padding + i * (segmentWidth + gap), (segmentHeight % 2) * 0.5f);
            images[i] = go.GetComponent<Image>();
            images[i].raycastTarget = false;
        }
    }

    /// <summary>value 0..1; color overrides fillColor when its alpha is above 0.</summary>
    public void SetValue(float value, Color? color = null)
    {
        EnsureSegments();
        Color fill = color.HasValue && color.Value.a > 0f ? new Color(color.Value.r, color.Value.g, color.Value.b, 1f) : fillColor;
        int filled = Mathf.Clamp(Mathf.CeilToInt(value * segments - 0.001f), 0, segments);
        for (int i = 0; i < images.Length; i++)
            images[i].color = i < filled ? fill : emptyColor;
    }
}
