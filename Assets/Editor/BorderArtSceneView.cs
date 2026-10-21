using UnityEditor;
using UnityEngine;

/// <summary>
/// The border artwork ("Background Image", layer PixelPerfectOutput) is a Screen Space Camera canvas, so the
/// Scene view draws it over the level at the camera's position, covering what's behind it. In game it only shows
/// in the border. This hides that layer in the Scene view (once per project; the Layers dropdown or the menu
/// item brings it back). The Game view is not affected.
/// </summary>
[InitializeOnLoad]
public static class BorderArtSceneView
{
    private const string LayerName = "PixelPerfectOutput";
    private const string MenuPath = "Tools/Mega Man/Show Border Art In Scene View";
    private static string AppliedKey => $"{PlayerSettings.productGUID}.BorderArtHiddenOnce";

    static BorderArtSceneView()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AppliedKey, false)) return;
            EditorPrefs.SetBool(AppliedKey, true);
            SetVisible(false);
        };
    }

    private static int LayerMaskBit
    {
        get
        {
            int layer = LayerMask.NameToLayer(LayerName);
            return layer < 0 ? 0 : 1 << layer;
        }
    }

    private static void SetVisible(bool visible)
    {
        if (LayerMaskBit == 0) return;
        Tools.visibleLayers = visible ? Tools.visibleLayers | LayerMaskBit : Tools.visibleLayers & ~LayerMaskBit;
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath)]
    private static void Toggle() => SetVisible((Tools.visibleLayers & LayerMaskBit) == 0);

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, (Tools.visibleLayers & LayerMaskBit) != 0);
        return LayerMaskBit != 0;
    }
}
