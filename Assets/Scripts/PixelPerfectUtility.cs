using UnityEngine;
using UnityEngine.U2D;

public static class PixelPerfectUtility
{
    public static void ForcePixelPerfectRefresh()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("No Main Camera found.");
            return;
        }

        var ppc = cam.GetComponent<PixelPerfectCamera>();
        if (ppc == null)
        {
            Debug.LogWarning("Main Camera does not have a PixelPerfectCamera.");
            return;
        }

        // Force update by toggling the component
        bool wasEnabled = ppc.enabled;
        ppc.enabled = false;
        ppc.enabled = true;

        // Optionally, force update camera matrix too
        cam.ResetAspect();
        cam.ResetProjectionMatrix();

        Debug.Log("PixelPerfectCamera refresh forced.");
    }
}