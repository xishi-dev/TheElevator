   using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject cameraLobby;
    public GameObject cameraElevator;

    private bool isLobbyView = true;

    void Start()
    {
        UpdateCamera();
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
        {
            isLobbyView = !isLobbyView;
            UpdateCamera();
        }
    }

    void UpdateCamera()
    {
        if (cameraLobby == null || cameraElevator == null)
            return;

        cameraLobby.SetActive(isLobbyView);
        cameraElevator.SetActive(!isLobbyView);
    }
}
