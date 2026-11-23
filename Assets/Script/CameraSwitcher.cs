
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject cameraLobby;
    public GameObject cameraElevator;

    [Header("Settings")]
    [SerializeField] private bool allowTabToggle = true;

    private bool isElevatorView = false;

    public bool IsElevatorView => isElevatorView;
    public bool AllowManualSwitch { get; set; } = true;
    public System.Func<bool> CanChangeView { get; set; }

    public Camera ActiveCamera
    {
        get
        {
            GameObject activeObject =
                isElevatorView ? cameraElevator : cameraLobby;

            if (activeObject == null)
                return null;

            Camera cam = activeObject.GetComponent<Camera>();

            if (cam == null)
                cam = activeObject.GetComponentInChildren<Camera>(true);

            return cam;
        }
    }

    private void Start()
    {
        ShowLobby();
    }

    private void Update()
    {
        if (allowTabToggle &&
            Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
            TryToggle();
    }

    public void TryToggle()
    {
        if (!AllowManualSwitch || (CanChangeView != null && !CanChangeView())) return;
        if (isElevatorView) ShowLobby();
        else ShowElevator();
    }
    public void ShowLobby()
    {
        isElevatorView = false;

        if (cameraLobby != null)
            cameraLobby.SetActive(true);

        if (cameraElevator != null)
            cameraElevator.SetActive(false);

    }
    public void ShowElevator()
    {
        isElevatorView = true;

        if (cameraLobby != null)
            cameraLobby.SetActive(false);

        if (cameraElevator != null)
            cameraElevator.SetActive(true);

    }
    public Camera GetCurrentCamera()
    {
        return ActiveCamera;
    }
}
