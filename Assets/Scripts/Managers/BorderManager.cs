using UnityEngine;
using UnityEngine.U2D;

public enum CropFrameMode
{
    None,       // No borders
    PillarBox,  // Vertical bars
    WindowBox   // Vertical + Horizontal bars
}

public class BorderManager : MonoBehaviour
{
    public static BorderManager Instance { get; private set; }

    private PixelPerfectCamera pixelPerfectCamera;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        pixelPerfectCamera = FindFirstObjectByType<PixelPerfectCamera>();
        int savedValue = PlayerPrefs.GetInt(SettingsKeys.BorderKey, 0);
        SetCropMode((CropFrameMode)savedValue);
    }

    public void SetCropMode(CropFrameMode mode)
    {
        if (pixelPerfectCamera == null)
            pixelPerfectCamera = FindFirstObjectByType<PixelPerfectCamera>();

        switch (mode)
        {
            case CropFrameMode.None:
                pixelPerfectCamera.cropFrameX = false;
                pixelPerfectCamera.cropFrameY = false;
                break;

            case CropFrameMode.PillarBox:
                pixelPerfectCamera.cropFrameX = true;
                pixelPerfectCamera.cropFrameY = false;
                break;

            case CropFrameMode.WindowBox:
                pixelPerfectCamera.cropFrameX = true;
                pixelPerfectCamera.cropFrameY = true;
                break;
        }

        pixelPerfectCamera.stretchFill = false;

        PlayerPrefs.SetInt(SettingsKeys.BorderKey, (int)mode);
        PlayerPrefs.Save();

        Debug.Log($"Border mode set to {mode}");
    }
}