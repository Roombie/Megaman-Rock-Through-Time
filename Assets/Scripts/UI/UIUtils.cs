using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class UIUtils
{
    /// <summary>
    /// Forces all canvases in the scene to update their layout safely, without disabling components.
    /// </summary>
    public static void ForceRefreshLayouts()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                var rect = canvas.GetComponent<RectTransform>();
                if (rect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                }
            }
        }
    }

    /// <summary>
    /// Assigns the given camera to all canvases using ScreenSpace - Camera.
    /// </summary>
    public static void AssignCameraToCanvases(Camera camera)
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = camera;
            }
        }
    }

    /// <summary>
    /// Refresh UI after current frame.
    /// </summary>
    public static IEnumerator DelayedRefreshUI()
    {
        yield return new WaitForEndOfFrame();
        ForceRefreshLayouts();
    }
}