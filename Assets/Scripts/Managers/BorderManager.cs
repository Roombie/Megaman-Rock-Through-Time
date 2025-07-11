using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BorderManager : MonoBehaviour
{
    public static BorderManager Instance { get; private set; }

    [SerializeField] private PixelPerfectCamera pixelPerfectCamera;
    private static bool isApplying = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SearchForCamera();
    }

    private void SearchForCamera()
    {
        if (pixelPerfectCamera == null)
            pixelPerfectCamera = FindFirstObjectByType<PixelPerfectCamera>();

        if (pixelPerfectCamera == null)
        {
            Debug.LogWarning("PixelPerfectCamera not found in BorderManager.");
        }
    }

    public void SetBorderMode(BorderMode mode)
    {
        if (isApplying) return;

        isApplying = true;

        SearchForCamera();

        if (pixelPerfectCamera != null)
        {
            pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.None;

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

                    int minWidth = pixelPerfectCamera.refResolutionX * 2;
                    int minHeight = pixelPerfectCamera.refResolutionY * 2;
                    if (Screen.width < minWidth || Screen.height < minHeight)
                        Screen.SetResolution(minWidth, minHeight, Screen.fullScreenMode);
                    break;
            }

            PlayerPrefs.SetInt(SettingsKeys.BorderKey, (int)mode);
            PlayerPrefs.Save();

            Debug.Log($"Border set to: {mode}");
        }
        else
        {
            Debug.LogWarning("PixelPerfectCamera not found in BorderManager.");
        }

        isApplying = false;
    }

    public void ApplyBorderClean(BorderMode mode)
    {
        StartCoroutine(DelayedBorderApplyWithUIRefresh(mode));
    }

    private IEnumerator DelayedBorderApplyWithUIRefresh(BorderMode mode)
    {
        // Wait for the screen change to be applied
        yield return new WaitForEndOfFrame(); // resolution
        yield return null;                    // layouts and UI scaler

        SetBorderMode(mode);

        // Clean UI
        yield return UIUtils.DelayedRefreshUI();
    }
}

public enum BorderMode { None, Pillarbox, Windowbox }