using UnityEngine;

public class BorderManager : MonoBehaviour
{
    public static BorderManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        bool enabled = PlayerPrefs.GetInt(SettingsKeys.BorderKey, 0) != 0;
        SetEnabled(enabled);
    }

    public void SetEnabled(bool enable)
    {
        Debug.Log($"Border enabled: {enable}");
        // TODO: mostrar u ocultar bordes
    }
}