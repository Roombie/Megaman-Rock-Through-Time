using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tools > Mega Man > Create Pause Menu Canvas: adds the in-game menus (weapons menu, pause menu,
/// exit confirmation) to the open scene as an editable canvas, laid out on the 256x240 game-pixel grid.
/// The look (font, colours, sprites) comes from the PauseManager on the GameManager prefab.
/// </summary>
public static class PauseMenuCanvasTool
{
    private const string GameManagerPrefab = "Assets/Prefabs/UI/GameManager.prefab";

    [MenuItem("Tools/Mega Man/Create Pause Menu Canvas")]
    public static void Create()
    {
        var existing = Object.FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        if (existing != null &&
            !EditorUtility.DisplayDialog("Pause Menu Canvas",
                $"This scene already has a pause menu ({existing.name}). Create another one?", "Create", "Cancel"))
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefab);
        var manager = prefab != null ? prefab.GetComponent<PauseManager>() : null;
        var style = manager != null ? manager.Style : new PauseMenuStyle();
        if (manager == null)
            Debug.LogWarning($"PauseMenuCanvasTool: no PauseManager on {GameManagerPrefab}, using the default look.");

        var ppc = Object.FindFirstObjectByType<PixelPerfectCamera>();
        var gameCamera = ppc != null ? ppc.GetComponent<Camera>() : Camera.main;

        var ui = PauseMenuFactory.Build(style, gameCamera);
        Undo.RegisterCreatedObjectUndo(ui.gameObject, "Create Pause Menu Canvas");

        // Keep it visible while editing (PauseMenuUI hides itself when the game starts) and fit the panels
        ui.gameObject.SetActive(true);
        ui.SetPage(0);
        ui.Relayout();

        Selection.activeGameObject = ui.gameObject;
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Debug.Log("Pause Menu Canvas created. Pages > Page 0 - Pause / Page 1 - Weapons sit side by side: move 'Pages' left to preview page 1. Save the scene to keep it.");
    }
}
