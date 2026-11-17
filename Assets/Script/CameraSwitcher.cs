
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

    // ตรวจสอบว่ากำลังใช้กล้องลิฟต์หรือไม่
    public bool IsElevatorView => isElevatorView;

    // กล้องที่กำลังใช้งาน
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
        // Tab ใช้ทดสอบสลับกล้องด้วยตัวเอง
        if (allowTabToggle &&
            Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (isElevatorView)
                ShowLobby();
            else
                ShowElevator();
        }
    }

    // เรียกใช้กล้อง Lobby
    public void ShowLobby()
    {
        isElevatorView = false;

        if (cameraLobby != null)
            cameraLobby.SetActive(true);

        if (cameraElevator != null)
            cameraElevator.SetActive(false);

        Debug.Log("Camera: Lobby");
    }

    // เรียกใช้กล้อง Elevator
    public void ShowElevator()
    {
        isElevatorView = true;

        if (cameraLobby != null)
            cameraLobby.SetActive(false);

        if (cameraElevator != null)
            cameraElevator.SetActive(true);

        Debug.Log("Camera: Elevator");
    }

    // รองรับการเรียกกล้องจาก Script อื่น
    public Camera GetCurrentCamera()
    {
        return ActiveCamera;
    }
}
