using UnityEngine;

public class ElevatorDoorController : MonoBehaviour
{
    [Header("Elevator Doors")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Door Settings")]
    [SerializeField] private float openDistance = 1.4f;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private bool openOnStart = true;

    [Header("Door Audio")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip openDoorSound;
    [SerializeField] private AudioClip closeDoorSound;
    [SerializeField] private AudioClip travelSound;
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 1f;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private bool isOpen;
    private bool initialized;

    public bool IsOpen => isOpen;

    public bool IsFullyClosed =>
        initialized &&
        Vector3.Distance(leftDoor.localPosition, leftClosedPos) < 0.005f &&
        Vector3.Distance(rightDoor.localPosition, rightClosedPos) < 0.005f;

    public bool IsFullyOpen =>
        initialized &&
        Vector3.Distance(leftDoor.localPosition, leftOpenPos) < 0.005f &&
        Vector3.Distance(rightDoor.localPosition, rightOpenPos) < 0.005f;

    private void Awake()
    {
        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogError("Assign both elevator doors!", this);
            enabled = false;
            return;
        }

        if (doorAudioSource == null)
        {
            doorAudioSource = GetComponent<AudioSource>();
        }

        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;

        leftOpenPos = leftClosedPos + Vector3.left * openDistance;
        rightOpenPos = rightClosedPos + Vector3.right * openDistance;

        initialized = true;
        isOpen = openOnStart;

        if (openOnStart)
        {
            leftDoor.localPosition = leftOpenPos;
            rightDoor.localPosition = rightOpenPos;
        }
        else
        {
            leftDoor.localPosition = leftClosedPos;
            rightDoor.localPosition = rightClosedPos;
        }
    }

    private void Update()
    {
        if (!initialized) return;

        Vector3 leftTarget = isOpen ? leftOpenPos : leftClosedPos;
        Vector3 rightTarget = isOpen ? rightOpenPos : rightClosedPos;

        float step = moveSpeed * Time.deltaTime;

        leftDoor.localPosition = Vector3.MoveTowards(
            leftDoor.localPosition, leftTarget, step);

        rightDoor.localPosition = Vector3.MoveTowards(
            rightDoor.localPosition, rightTarget, step);
    }

    // เรียกใช้เมื่อถึงเวลาต้องเปิดประตู
    public void OpenDoors(bool playSound = true)
    {
        if (!initialized) return;

        bool stateChanged = !isOpen;
        isOpen = true;

        if (playSound && stateChanged)
        {
            PlayDoorSound(openDoorSound);
        }
    }

    // เรียกใช้เมื่อถึงเวลาต้องปิดประตู
    public void CloseDoors(bool playSound = true)
    {
        if (!initialized) return;

        bool stateChanged = isOpen;
        isOpen = false;

        if (playSound && stateChanged)
        {
            PlayDoorSound(closeDoorSound);
        }
    }

    // เรียกใช้เมื่อลิฟต์กำลังเคลื่อนที่ไปชั้นถัดไป
    public void PlayTravelSound()
    {
        PlayDoorSound(travelSound);
    }

    private void PlayDoorSound(AudioClip clip)
    {
        if (doorAudioSource == null || clip == null) return;

        doorAudioSource.Stop();
        doorAudioSource.PlayOneShot(clip, audioVolume);
    }
}