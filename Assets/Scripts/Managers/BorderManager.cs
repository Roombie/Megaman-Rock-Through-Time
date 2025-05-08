using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class BorderManager : MonoBehaviour
{
    public static BorderManager Instance { get; private set; }

    [SerializeField] private PixelPerfectCamera pixelPerfectCamera;

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
        SearchForCamera();  // Asegúrate de que la cámara esté asignada

        // Reiniciar la cámara y el borde antes de aplicar el nuevo
        if (pixelPerfectCamera != null)
        {
            pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.None;  // Reiniciar el borde

            // Aplicar el borde seleccionado
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

            // Aplicar los cambios y guardarlos
            PlayerPrefs.SetInt(SettingsKeys.BorderKey, (int)mode);
            PlayerPrefs.Save();

            Debug.Log($"Border set to: {mode}");
        }
        else
        {
            Debug.LogWarning("PixelPerfectCamera not found in BorderManager.");
        }
    }
}

public enum BorderMode { None, Pillarbox, Windowbox }