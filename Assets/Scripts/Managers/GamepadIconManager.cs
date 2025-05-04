using UnityEngine;

public class GamepadIconManager : MonoBehaviour
{
    public static GamepadIconManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetControlScheme(PlayerPrefs.GetInt(SettingsKeys.DisplayModeKey, 0));
    }

    public void SetControlScheme(int index)
    {
        Debug.Log($"Gamepad icon scheme set to index {index}");
        // TODO: cambia sprites usados por UI
    }
}