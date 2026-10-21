using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD

/// <summary>
/// Rich audio debug overlay for editor and development builds.
/// The reusable audio package intentionally has no dependency on Unity's Input System.
/// External integrations can call ToggleVisible(), while projects using the legacy
/// input backend can still use toggleKey directly.
/// </summary>
public class AudioDebugger : MonoBehaviour
{
    [SerializeField] AudioManager audioManager;

    [Header("Display")]
    [Tooltip("Screen-space rectangle for the debug panel.")]
    public Rect panelRect = new Rect(8, 32, 340, 400);

    [Tooltip("Legacy Input Manager shortcut. Projects using the new Input System should call ToggleVisible() from an integration component.")]
    public KeyCode toggleKey = KeyCode.F9;

    [SerializeField] bool startVisible = true;

    bool visible;
    Vector2 scroll;

    public bool IsVisible => visible;

    void Awake()
    {
        visible = startVisible;

        if (audioManager == null) audioManager = GetComponentInParent<AudioManager>();
        if (audioManager != null) return;

        AudioManager[] managers = FindObjectsByType<AudioManager>(FindObjectsSortMode.None);
        if (managers.Length == 1) audioManager = managers[0];
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(toggleKey))
            ToggleVisible();
#endif
    }

    public void ToggleVisible()
    {
        visible = !visible;
    }

    public void SetVisible(bool value)
    {
        visible = value;
    }

    void OnGUI()
    {
        if (!visible || audioManager == null) return;

        float width = Mathf.Min(400, Screen.width * 0.4f);
        float height = Mathf.Min(500, Screen.height * 0.5f);
        float x = 8;
        float y = 32;
        Rect dynamicRect = new Rect(x, y, width, height);

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(dynamicRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(dynamicRect);

        GUILayout.Label("── Audio Debugger ──");
        GUILayout.Space(4);

        GUILayout.Label($"Voices   : {audioManager.ActiveVoiceCount} / {audioManager.maxVoices}");

        string bgmName = "—";
        if (audioManager.MusicSource != null && audioManager.MusicSource.isPlaying)
            bgmName = audioManager.MusicSource.clip != null ? audioManager.MusicSource.clip.name : "—";

        GUILayout.Label($"BGM      : {bgmName}");
        GUILayout.Label($"Muted    : {AudioListener.pause}");
        GUILayout.Label($"Volume   : {AudioListener.volume:F2}");

        GUILayout.Space(6);
        GUILayout.Label("── Active AudioSources ──");

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(dynamicRect.height - 140));

        AudioSource[] sources = audioManager.GetComponents<AudioSource>();
        int playing = 0;

        foreach (AudioSource src in sources)
        {
            if (!src.isPlaying) continue;
            playing++;
            string clip = src.clip != null ? src.clip.name : "—";
            string loop = src.loop ? " [loop]" : "";
            string spatial = src.spatialBlend > 0.05f ? $" [3D {src.spatialBlend:F1}]" : " [2D]";
            GUILayout.Label($"  {clip}{loop}{spatial}  vol:{src.volume:F2}  pitch:{src.pitch:F2}");
        }

        if (playing == 0)
            GUILayout.Label("  (none)");

        GUILayout.EndScrollView();

        GUILayout.Label($"Total sources on GO: {sources.Length}  Playing: {playing}");

        GUILayout.EndArea();
    }
}
#endif
