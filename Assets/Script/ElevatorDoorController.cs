
using UnityEngine;
using UnityEngine.InputSystem;

public class ElevatorDoorController : MonoBehaviour
{
    [Header("Elevator Doors")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Door Settings")]
    [SerializeField] private float openDistance = 1.4f;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private bool openOnStart = true;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;

    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private bool isOpen;

    void Start()
    {
        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogError("Please assign both elevator doors!");
            enabled = false;
            return;
        }

        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;

        leftOpenPos = leftClosedPos + Vector3.left * openDistance;
        rightOpenPos = rightClosedPos + Vector3.right * openDistance;

        isOpen = openOnStart;
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (isOpen)
                CloseDoors();
            else
                OpenDoors();
        }

        Vector3 leftTarget = isOpen ? leftOpenPos : leftClosedPos;
        Vector3 rightTarget = isOpen ? rightOpenPos : rightClosedPos;

        float step = moveSpeed * Time.deltaTime;

        leftDoor.localPosition = Vector3.MoveTowards(
            leftDoor.localPosition,
            leftTarget,
            step
        );

        rightDoor.localPosition = Vector3.MoveTowards(
            rightDoor.localPosition,
            rightTarget,
            step
        );
    }

    public void OpenDoors()
    {
        isOpen = true;
    }

    public void CloseDoors()
    {
        isOpen = false;
    }
}
