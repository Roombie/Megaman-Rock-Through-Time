using UnityEngine;
using UnityEngine.U2D;

[RequireComponent(typeof(Camera))]
public class AspectRatioManager : MonoBehaviour
{
    public bool enablePillarbox = true;
    public float targetAspectRatio = 4f / 3f;

    private Camera cam;
    private PixelPerfectCamera pixelPerfect;

    void Awake()
    {
        cam = GetComponent<Camera>();
        pixelPerfect = GetComponent<PixelPerfectCamera>();
        enablePillarbox = PlayerPrefs.GetInt(SettingsKeys.BorderKey, 1) == 1;
        ApplyAspect();
    }

    public void TogglePillarbox(bool enabled)
    {
        enablePillarbox = enabled;
        ApplyAspect();
    }

    void ApplyAspect()
    {
        if (!enablePillarbox)
        {
            cam.rect = new Rect(0, 0, 1, 1);
            if (pixelPerfect != null)
            {
                pixelPerfect.cropFrameX = false;
            }
            return;
        }

        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspectRatio;

        if (pixelPerfect != null)
        {
            pixelPerfect.cropFrameX = true;
        }

        if (scaleHeight < 1f)
        {
            Rect rect = new Rect
            {
                width = 1f,
                height = scaleHeight,
                x = 0f,
                y = (1f - scaleHeight) / 2f
            };
            cam.rect = rect;
        }
        else
        {
            float scaleWidth = 1f / scaleHeight;
            Rect rect = new Rect
            {
                width = scaleWidth,
                height = 1f,
                x = (1f - scaleWidth) / 2f,
                y = 0f
            };
            cam.rect = rect;
        }
    }
}