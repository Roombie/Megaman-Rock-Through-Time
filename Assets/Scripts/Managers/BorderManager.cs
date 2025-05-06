using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BorderManager : MonoBehaviour
{
    public static BorderManager Instance { get; private set; }

    [SerializeField] private PixelPerfectCamera pixelPerfectCamera;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (pixelPerfectCamera == null)
            pixelPerfectCamera = FindFirstObjectByType<PixelPerfectCamera>();

        var mode = (BorderMode)PlayerPrefs.GetInt(SettingsKeys.BorderKey, 0);
        SetBorderMode(mode);
    }

    public void SetBorderMode(BorderMode mode)
    {
        if (pixelPerfectCamera == null)
            pixelPerfectCamera = FindFirstObjectByType<PixelPerfectCamera>();

        if (pixelPerfectCamera == null)
        {
            Debug.LogWarning("PixelPerfectCamera not found in BorderManager.");
            return;
        }

        switch (mode)
        {
            case BorderMode.None:
                pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.None;
                break;

            case BorderMode.Pillarbox:
                pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.Pillarbox;
                break;

            case BorderMode.Windowbox:
                pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
                break;
        }

        PlayerPrefs.SetInt(SettingsKeys.BorderKey, (int)mode);
        PlayerPrefs.Save();

        Debug.Log($"Border set to: {mode}");
    }
}

public enum BorderMode { None, Pillarbox, Windowbox }