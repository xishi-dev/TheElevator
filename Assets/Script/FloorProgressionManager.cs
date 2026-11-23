
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FloorProgressionManager : MonoBehaviour
{
    [System.Serializable]
    public class FloorSetup
    {
        public string floorName = "Floor";

        [Tooltip("Fixed marker at this floor's elevator stop")]
        public Transform stopPoint;

        public ElevatorDoorController doorController;

        public Transform spawnPoint;
        public Transform queueStart;

        [Min(1f)]
        public float roundSeconds = 30f;
    }

    [Header("Seven Floors")]
    [SerializeField]
    private FloorSetup[] floors =
        new FloorSetup[7];

    [Header("Managers")]
    [SerializeField] private LobbyCountdown countdown;
    [SerializeField] private CameraSwitcher cameraSwitcher;
    [SerializeField] private NPCGrabController grabController;
    [SerializeField] private NPCQueueManager queueManager;

    [Header("Elevator References")]
    [SerializeField] private BoxCollider elevatorZone;
    [SerializeField] private Transform elevatorMoveDirection;

    [Tooltip("Additional objects that travel with the elevator")]
    [SerializeField] private Transform[] extraMovingObjects;

    [Header("Travel")]
    [SerializeField] private float travelDuration = 2f;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private CanvasGroup blackFade;

    [Header("UI")]
    [SerializeField] private TMP_Text currentFloorText;
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private TMP_Text finishText;

    private int floorIndex;
    private bool transitioning;
    private bool finished;

    public int CurrentFloor => floorIndex + 1;

    private void OnEnable()
    {
        if (countdown != null)
            countdown.OnCountdownFinished += OnTimeUp;
    }

    private void OnDisable()
    {
        if (countdown != null)
            countdown.OnCountdownFinished -= OnTimeUp;
    }

    private IEnumerator Start()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            yield break;
        }

        floorIndex = 0;
        transitioning = false;
        finished = false;

        countdown.StopCountdown();
        grabController.enabled = false;
        queueManager.enabled = false;

        if (finishPanel != null)
            finishPanel.SetActive(false);

        if (blackFade != null)
        {
            blackFade.alpha = 0f;
            blackFade.blocksRaycasts = false;
            blackFade.gameObject.SetActive(false);
        }

        for (int i = 0; i < floors.Length; i++)
        {
            floors[i].doorController
                .SetManualInputEnabled(false);

            if (i != 0)
                floors[i].doorController.CloseDoors();
        }

        FloorSetup firstFloor = floors[0];

        queueManager.ConfigureForFloor(
            firstFloor.spawnPoint,
            firstFloor.queueStart,
            new HashSet<NPCQueueAgent>()
        );

        cameraSwitcher.ShowLobby();
        UpdateFloorText();

        firstFloor.doorController.OpenDoors();

        yield return WaitForDoor(
            firstFloor.doorController,
            true
        );

        queueManager.enabled = true;
        grabController.enabled = true;

        countdown.StartCountdown(
            firstFloor.roundSeconds
        );

        Debug.Log("Started Floor 1");
    }

    private bool ValidateSetup()
    {
        if (countdown == null ||
            cameraSwitcher == null ||
            grabController == null ||
            queueManager == null ||
            elevatorZone == null)
        {
            Debug.LogError(
                "Floor manager has missing references.",
                this
            );
            return false;
        }

        if (floors == null || floors.Length != 7)
        {
            Debug.LogError(
                "Floor manager requires exactly 7 floors.",
                this
            );
            return false;
        }

        for (int i = 0; i < floors.Length; i++)
        {
            FloorSetup floor = floors[i];

            if (floor == null ||
                floor.stopPoint == null ||
                floor.doorController == null ||
                floor.spawnPoint == null ||
                floor.queueStart == null)
            {
                Debug.LogError(
                    "Floor " + (i + 1) +
                    " has missing references.",
                    this
                );
                return false;
            }
        }

        return true;
    }

    private void OnTimeUp()
    {
        if (!transitioning && !finished)
            StartCoroutine(CompleteFloor());
    }

    private IEnumerator CompleteFloor()
    {
        transitioning = true;

        grabController.enabled = false;
        queueManager.enabled = false;

        cameraSwitcher.ShowElevator();

        FloorSetup current = floors[floorIndex];

        current.doorController.CloseDoors();

        yield return WaitForDoor(
            current.doorController,
            false
        );

        float settleTimeout = 2f;

        while (settleTimeout > 0f &&
               AnyPassengerLowering())
        {
            settleTimeout -= Time.deltaTime;
            yield return null;
        }

        if (floorIndex >= floors.Length - 1)
        {
            FinishGame();
            yield break;
        }

        yield return FadeTo(1f);

        yield return new WaitForSeconds(
            Mathf.Max(0f, travelDuration)
        );

        List<NPCQueueAgent> passengers =
            CollectPassengers();

        FloorSetup next = floors[floorIndex + 1];

        Vector3 movement =
            next.stopPoint.position -
            current.stopPoint.position;

        MoveElevatorObjects(movement, passengers);

        queueManager.ConfigureForFloor(
            next.spawnPoint,
            next.queueStart,
            new HashSet<NPCQueueAgent>(passengers)
        );

        floorIndex++;

        Physics.SyncTransforms();

        yield return null;

        UpdateFloorText();

        next.doorController.OpenDoors();

        yield return WaitForDoor(
            next.doorController,
            true
        );

        cameraSwitcher.ShowLobby();

        yield return FadeTo(0f);

        queueManager.enabled = true;
        grabController.enabled = true;

        countdown.StartCountdown(
            next.roundSeconds
        );

        transitioning = false;

        Debug.Log(
            "Arrived at Floor " + CurrentFloor
        );
    }

    private IEnumerator WaitForDoor(
        ElevatorDoorController door,
        bool waitForOpen)
    {
        float timeout = 12f;

        while (timeout > 0f)
        {
            bool complete = waitForOpen
                ? door.IsFullyOpen
                : door.IsFullyClosed;

            if (complete)
                yield break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning(
            "Elevator door movement timed out.",
            door
        );
    }

    private bool IsInsideElevator(NPCQueueAgent agent)
    {
        if (agent == null)
            return false;

        Vector3 sample = agent.transform.position;

        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule != null)
        {
            sample = capsule.transform.TransformPoint(
                capsule.center
            );
        }

        Vector3 local =
            elevatorZone.transform.InverseTransformPoint(
                sample
            ) - elevatorZone.center;

        Vector3 half = elevatorZone.size * 0.5f;

        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.y) <= half.y &&
               Mathf.Abs(local.z) <= half.z;
    }

    private List<NPCQueueAgent> CollectPassengers()
    {
        List<NPCQueueAgent> passengers =
            new List<NPCQueueAgent>();

        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (IsInsideElevator(agent))
                passengers.Add(agent);
        }

        return passengers;
    }

    private bool AnyPassengerLowering()
    {
        foreach (NPCQueueAgent agent in CollectPassengers())
        {
            if (agent != null && agent.IsLowering)
                return true;
        }

        return false;
    }
    private void MoveElevatorObjects(
        Vector3 movement,
        List<NPCQueueAgent> passengers)
    {
        HashSet<Transform> targets =
            new HashSet<Transform>();

        if (cameraSwitcher.cameraLobby != null)
            targets.Add(
                cameraSwitcher.cameraLobby.transform
            );

        if (cameraSwitcher.cameraElevator != null)
            targets.Add(
                cameraSwitcher.cameraElevator.transform
            );

        targets.Add(elevatorZone.transform);

        if (elevatorMoveDirection != null)
            targets.Add(elevatorMoveDirection);

        if (extraMovingObjects != null)
        {
            foreach (Transform extra in extraMovingObjects)
            {
                if (extra != null)
                    targets.Add(extra);
            }
        }

        foreach (Transform target in targets)
        {
            if (HasMovingAncestor(target, targets))
                continue;

            target.position += movement;
        }

        foreach (NPCQueueAgent agent in passengers)
        {
            if (agent == null)
                continue;

            if (!HasMovingAncestor(agent.transform, targets) &&
                !targets.Contains(agent.transform))
            {
                agent.transform.position += movement;
            }
        }
    }

    private bool HasMovingAncestor(
        Transform child,
        HashSet<Transform> targets)
    {
        Transform parent = child.parent;

        while (parent != null)
        {
            if (targets.Contains(parent))
                return true;

            parent = parent.parent;
        }

        return false;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (blackFade == null)
            yield break;

        blackFade.gameObject.SetActive(true);
        blackFade.blocksRaycasts = true;

        float startAlpha = blackFade.alpha;
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            blackFade.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsed / duration)
            );

            yield return null;
        }

        blackFade.alpha = targetAlpha;

        if (targetAlpha <= 0f)
        {
            blackFade.blocksRaycasts = false;
            blackFade.gameObject.SetActive(false);
        }
    }

    private void UpdateFloorText()
    {
        if (currentFloorText != null)
        {
            currentFloorText.text =
                "FLOOR " + CurrentFloor + " / 7";
        }
    }

    private void FinishGame()
    {
        finished = true;
        transitioning = false;

        countdown.StopCountdown();

        int passengerCount =
            CollectPassengers().Count;

        if (finishText != null)
        {
            finishText.text =
                "GAME COMPLETE!\n" +
                "FLOOR 7 / 7\n" +
                "Passengers: " + passengerCount;
        }

        if (finishPanel != null)
            finishPanel.SetActive(true);

        Debug.Log(
            "Game Complete! Passengers: " +
            passengerCount
        );
    }
}
