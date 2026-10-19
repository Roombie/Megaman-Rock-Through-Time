using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helpers for laying out UI on the game-pixel grid (1 canvas unit = 1 game pixel).
/// </summary>
public static class PixelLayout
{
    private static readonly Vector3[] corners = new Vector3[4];

    /// <summary>
    /// Bounds of everything visible under `root` (images and the actual text glyphs), in `space` coordinates.
    /// </summary>
    public static bool TryGetContentBounds(RectTransform root, RectTransform space, out Rect bounds)
    {
        bool any = false;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
        {
            if (!graphic.enabled || graphic.color.a <= 0f) continue;

            if (graphic is TMP_Text text)
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                text.ForceMeshUpdate();
                Bounds b = text.textBounds;
                if (b.size.x <= 0f) continue;
                Include(space, text.rectTransform.TransformPoint(b.min), ref min, ref max);
                Include(space, text.rectTransform.TransformPoint(b.max), ref min, ref max);
            }
            else
            {
                graphic.rectTransform.GetWorldCorners(corners);
                Include(space, corners[0], ref min, ref max);
                Include(space, corners[2], ref min, ref max);
            }
            any = true;
        }

        bounds = any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
        return any;
    }

    private static void Include(RectTransform space, Vector3 world, ref Vector2 min, ref Vector2 max)
    {
        Vector2 p = space.InverseTransformPoint(world);
        min = Vector2.Min(min, p);
        max = Vector2.Max(max, p);
    }

    /// <summary>Moves a RectTransform by a whole number of pixels.</summary>
    public static void Nudge(RectTransform rect, float dx, float dy)
    {
        rect.anchoredPosition += new Vector2(Mathf.Round(dx), Mathf.Round(dy));
    }
}
