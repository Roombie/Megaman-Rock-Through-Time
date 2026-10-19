using UnityEngine;

/// <summary>
/// Sizes a panel's border and background around its content (plus padding), on whole pixels, and can centre
/// the whole panel on its own origin. The background grows or shrinks with what the menu shows.
/// Layout: Panel (this) > Border, Background, Content.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PixelPanelFit : MonoBehaviour
{
    public RectTransform content;
    public RectTransform border;
    public RectTransform background;
    public int borderThickness = 2;
    public int paddingLeft = 8;
    public int paddingRight = 8;
    public int paddingTop = 8;
    public int paddingBottom = 8;
    [Tooltip("Move the content so the finished panel is centred on this object.")]
    public bool centre = true;

    public void Fit()
    {
        var self = (RectTransform)transform;
        if (content == null || !PixelLayout.TryGetContentBounds(content, self, out Rect b)) return;

        float left = Mathf.Floor(b.xMin) - paddingLeft - borderThickness;
        float right = Mathf.Ceil(b.xMax) + paddingRight + borderThickness;
        float bottom = Mathf.Floor(b.yMin) - paddingBottom - borderThickness;
        float top = Mathf.Ceil(b.yMax) + paddingTop + borderThickness;

        if (centre)
        {
            float dx = -Mathf.Floor((left + right) / 2f);
            float dy = -Mathf.Floor((top + bottom) / 2f);
            PixelLayout.Nudge(content, dx, dy);
            left += dx; right += dx; bottom += dy; top += dy;
        }

        Place(border, left, right, bottom, top);
        Place(background, left + borderThickness, right - borderThickness, bottom + borderThickness, top - borderThickness);
    }

    private static void Place(RectTransform rect, float left, float right, float bottom, float top)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(right - left, top - bottom);
        rect.anchoredPosition = new Vector2((left + right) / 2f, (bottom + top) / 2f);
    }
}
