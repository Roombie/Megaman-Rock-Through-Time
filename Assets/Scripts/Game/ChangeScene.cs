using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    //This script can be used to load any scene
    public void LoadScene(string sceneName) {
        // The Options screen opened from the pause menu returns to the game instead of the Title screen
        if (PauseManager.Instance != null && PauseManager.Instance.TryCloseOptionsOverlay())
            return;

        ScreenFader.Instance.FadeToScene(sceneName);
        // Debug.Log("New level");
    }

    //Method to exit the application
    public void ExitApplication() {
        Application.Quit();
        // Debug.Log("Exit Application");
    }
}
