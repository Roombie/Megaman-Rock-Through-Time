using UnityEngine;
using UnityEngine.U2D;

[RequireComponent(typeof(Camera))]
public class AspectRatioManager : MonoBehaviour
{
    public float targetAspectRatio = 4f / 3f;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyAspect();
    }

    void ApplyAspect()
    {            
        cam.rect = new Rect(0, 0, 1, 1);

        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspectRatio;

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