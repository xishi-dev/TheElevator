
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "Elevator_Lobby_Main";

    private void Start()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.gameObject.scene != gameObject.scene) continue;
            canvas.enabled = false;
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            foreach (var selectable in canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                selectable.interactable = false;
        }
        ShiftAudio.Ready();
        ShiftHud.OpenMenu(this);
    }

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }
}
