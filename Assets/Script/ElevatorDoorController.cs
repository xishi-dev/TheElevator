
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
    [SerializeField] private bool allowManualInput = true;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private bool isOpen;
    private bool initialized;

    public bool IsOpen => isOpen;

    public bool IsFullyClosed =>
        initialized &&
        Vector3.Distance(leftDoor.localPosition, leftClosedPos)
            < 0.005f &&
        Vector3.Distance(rightDoor.localPosition, rightClosedPos)
            < 0.005f;

    public bool IsFullyOpen =>
        initialized &&
        Vector3.Distance(leftDoor.localPosition, leftOpenPos)
            < 0.005f &&
        Vector3.Distance(rightDoor.localPosition, rightOpenPos)
            < 0.005f;

    private void Awake()
    {
        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogError(
                "Assign both elevator doors!", this
            );
            enabled = false;
            return;
        }

        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;

        leftOpenPos =
            leftClosedPos + Vector3.left * openDistance;

        rightOpenPos =
            rightClosedPos + Vector3.right * openDistance;

        initialized = true;
        isOpen = openOnStart;

        if (openOnStart)
        {
            leftDoor.localPosition = leftOpenPos;
            rightDoor.localPosition = rightOpenPos;
        }
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (allowManualInput &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (isOpen)
                CloseDoors();
            else
                OpenDoors();
        }

        Vector3 leftTarget =
            isOpen ? leftOpenPos : leftClosedPos;

        Vector3 rightTarget =
            isOpen ? rightOpenPos : rightClosedPos;

        float step = Mathf.Max(0.01f, moveSpeed)
                     * Time.deltaTime;

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
        if (initialized)
            isOpen = true;
    }

    public void CloseDoors()
    {
        if (initialized)
            isOpen = false;
    }

    public void SetManualInputEnabled(bool allowed)
    {
        allowManualInput = allowed;
    }
}
