
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FloorProgressionManager : MonoBehaviour
{
    public enum FloorPhase
    {
        Boarding,
        Unloading
    }

    [System.Serializable]
    public class FloorSetup
    {
        public string floorName = "Floor";
        public Transform stopPoint;
        public ElevatorDoorController doorController;
        public Transform spawnPoint;
        public Transform queueStart;

        [Min(1f)]
        public float roundSeconds = 30f;
    }

    [Header("Seven Floors")]
    [SerializeField]
    private FloorSetup[] floors = new FloorSetup[7];

    [Header("Managers")]
    [SerializeField] private LobbyCountdown countdown;
    [SerializeField] private CameraSwitcher cameraSwitcher;
    [SerializeField] private NPCGrabController grabController;
    [SerializeField] private NPCQueueManager queueManager;

    [Header("Elevator References")]
    [SerializeField] private BoxCollider elevatorZone;
    [SerializeField] private Transform elevatorMoveDirection;
    [SerializeField] private Transform[] extraMovingObjects;

    [Header("Travel")]
    [SerializeField] private float travelDuration = 2f;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private CanvasGroup blackFade;

    [Header("Floor Phases")]
    [Min(1f)]
    [SerializeField] private float unloadSeconds = 15f;

    [Min(1f)]
    [SerializeField] private float noExitSeconds = 3f;

    [Range(0f, 1f)]
    [SerializeField] private float exitChance = 0.3f;

    [Header("Phase Transition")]
    [Min(0f)]
    [SerializeField] private float phaseTransitionDelay = 3f;

    [Header("Score Settings")]
    [SerializeField] private int boardingPoints = 100;
    [SerializeField] private int unloadingPoints = 125;
    [SerializeField] private int missedExitPenalty = 100;
    [SerializeField] private float latePenaltyPerSecond = 2f;

    [Header("UI")]
    [SerializeField] private TMP_Text currentFloorText;
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private TMP_Text finishText;

    [Header("Temporary Phase HUD")]
    [SerializeField] private bool showPhaseOverlay = true;

    private int floorIndex;
    private int score;
    private int boardedCount;
    private int deliveredCount;
    private int missedCount;

    private bool transitioning;
    private bool finished;
    private bool unloadHasTargets;

    private FloorPhase phase = FloorPhase.Boarding;

    // New: transition message.
    private bool phaseBannerActive;
    private string phaseBannerText = "";
    private float phaseBannerEndTime;

    private GUIStyle phaseStyle;
    private GUIStyle bannerStyle;

    public int CurrentFloor => floorIndex + 1;
    public FloorPhase CurrentPhase => phase;
    public int Score => score;

    // =====================================
    // EVENTS
    // =====================================

    private void OnEnable()
    {
        if (countdown != null)
            countdown.OnCountdownFinished += OnTimeUp;
    }

    private void OnDisable()
    {
        if (countdown != null)
            countdown.OnCountdownFinished -= OnTimeUp;

        if (grabController != null)
            grabController.CanGrabPassenger = null;
    }

    // =====================================
    // START GAME
    // =====================================

    private IEnumerator Start()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            yield break;
        }

        floorIndex = 0;
        score = 0;
        boardedCount = 0;
        deliveredCount = 0;
        missedCount = 0;

        finished = false;
        transitioning = true;
        unloadHasTargets = false;
        phaseBannerActive = false;
        phase = FloorPhase.Boarding;

        countdown.StopCountdown();

        grabController.enabled = false;
        queueManager.enabled = false;
        grabController.CanGrabPassenger = null;

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
            ElevatorDoorController door =
                floors[i].doorController;

            door.SetManualInputEnabled(false);

            if (i != 0)
                door.CloseDoors();
        }

        FloorSetup first = floors[0];

        queueManager.ConfigureForFloor(
            first.spawnPoint,
            first.queueStart,
            new HashSet<NPCQueueAgent>()
        );

        cameraSwitcher.ShowLobby();

        first.doorController.OpenDoors();

        yield return WaitForDoor(
            first.doorController,
            true
        );

        StartBoarding();

        transitioning = false;
    }

    // =====================================
    // VALIDATION
    // =====================================

    private bool ValidateSetup()
    {
        if (countdown == null ||
            cameraSwitcher == null ||
            grabController == null ||
            queueManager == null ||
            elevatorZone == null)
        {
            Debug.LogError(
                "Floor Manager: Missing references.",
                this
            );

            return false;
        }

        if (floors == null || floors.Length != 7)
        {
            Debug.LogError(
                "Floor Manager requires 7 floors.",
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
                    "Missing references on Floor " + (i + 1),
                    this
                );

                return false;
            }
        }

        return true;
    }

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        if (finished || transitioning)
            return;

        UpdatePassengerStates();

        // When every required NPC has left,
        // enter the transition pause.
        if (phase == FloorPhase.Unloading &&
            unloadHasTargets &&
            CountPendingExits() == 0)
        {
            StartCoroutine(FinishUnloading());
        }
    }

    // =====================================
    // PASSENGERS AND SCORE
    // =====================================

    private void UpdatePassengerStates()
    {
        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent == null)
                continue;

            bool inside = IsInsideElevator(agent);

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            if (status == null && inside)
            {
                status = agent.gameObject
                    .AddComponent<PassengerExitStatus>();
            }

            if (status == null)
                continue;

            // BOARDING SCORE
            if (phase == FloorPhase.Boarding &&
                inside &&
                !agent.IsGrabbed &&
                !agent.IsLowering &&
                !status.HasBoarded)
            {
                status.MarkBoarded();

                boardedCount++;
                score += boardingPoints;
            }

            // DELIVERY SCORE
            if (status.IsPending &&
                !inside &&
                !agent.IsGrabbed &&
                !agent.IsLowering &&
                !agent.IsInQueue)
            {
                status.MarkDelivered();

                deliveredCount++;
                score += unloadingPoints;

                Debug.Log(
                    "Passenger delivered: " +
                    agent.name
                );
            }

            // OVERDUE PENALTY
            if (status.IsOverdue)
            {
                int floorsLate = Mathf.Max(
                    1,
                    CurrentFloor -
                    status.RequestedFloor + 1
                );

                float penaltyRate =
                    latePenaltyPerSecond * floorsLate;

                score -= status.CalculateLatePenalty(
                    Time.deltaTime,
                    penaltyRate
                );
            }
        }

        UpdateFloorText();
    }

    private int CountPendingExits()
    {
        int count = 0;

        PassengerExitStatus[] statuses =
            FindObjectsByType<PassengerExitStatus>(
                FindObjectsSortMode.None
            );

        foreach (PassengerExitStatus status in statuses)
        {
            if (status != null && status.IsPending)
                count++;
        }

        return count;
    }

    // =====================================
    // BOARDING PHASE
    // =====================================

    private void StartBoarding()
    {
        phase = FloorPhase.Boarding;
        unloadHasTargets = false;

        grabController.CanGrabPassenger = null;

        queueManager.enabled = true;
        grabController.enabled = true;

        float duration = Mathf.Max(
            1f,
            floors[floorIndex].roundSeconds
        );

        countdown.StartCountdown(duration);

        UpdateFloorText();

        Debug.Log(
            "FLOOR " + CurrentFloor +
            " | BOARDING | " +
            duration + " seconds"
        );
    }

    // =====================================
    // UNLOADING PHASE
    // =====================================

    private void StartUnloading()
    {
        phase = FloorPhase.Unloading;

        queueManager.enabled = false;

        List<NPCQueueAgent> passengers =
            CollectPassengers();

        foreach (NPCQueueAgent agent in passengers)
        {
            if (agent == null)
                continue;

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            if (status == null)
            {
                status = agent.gameObject
                    .AddComponent<PassengerExitStatus>();
            }

            // Keep overdue NPCs pending.
            if (status.IsPending)
                continue;

            if (status.State ==
                PassengerExitStatus.ExitState.Delivered)
            {
                continue;
            }

            // Floors 2-6: 30% chance to exit.
            // Floor 7: everyone must exit.
            bool mustExit =
                CurrentFloor == 7 ||
                Random.value < exitChance;

            if (mustExit)
                status.RequestExit(CurrentFloor);
        }

        // Only pending passengers can be
        // grabbed during unloading.
        grabController.CanGrabPassenger = agent =>
        {
            if (agent == null)
                return false;

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            return status != null &&
                   status.IsPending;
        };

        unloadHasTargets = CountPendingExits() > 0;

        grabController.enabled = true;

        float duration = unloadHasTargets
            ? Mathf.Max(1f, unloadSeconds)
            : Mathf.Max(1f, noExitSeconds);

        countdown.StartCountdown(duration);

        UpdateFloorText();

        Debug.Log(
            "FLOOR " + CurrentFloor +
            " | UNLOADING | " +
            CountPendingExits() + " to exit"
        );
    }

    // =====================================
    // TIMER
    // =====================================

    private void OnTimeUp()
    {
        if (transitioning || finished)
            return;

        if (phase == FloorPhase.Unloading)
        {
            StartCoroutine(FinishUnloading());
        }
        else
        {
            StartCoroutine(FinishBoardingAndTravel());
        }
    }

    // =====================================
    // FINISH UNLOADING
    // =====================================

    private IEnumerator FinishUnloading()
    {
        // Lock the transition immediately
        // so no second coroutine can start.
        transitioning = true;

        countdown.StopCountdown();

        // Stop grabbing and spawning.
        grabController.enabled = false;
        queueManager.enabled = false;

        // Finish lowering held NPCs.
        yield return WaitForPassengersToSettle();

        // One last check for successful exits.
        UpdatePassengerStates();

        bool allExited =
            unloadHasTargets &&
            CountPendingExits() == 0;

        PassengerExitStatus[] statuses =
            FindObjectsByType<PassengerExitStatus>(
                FindObjectsSortMode.None
            );

        foreach (PassengerExitStatus status in statuses)
        {
            if (status == null ||
                !status.MustExit ||
                status.RequestedFloor != CurrentFloor)
            {
                continue;
            }

            NPCQueueAgent agent =
                status.GetComponent<NPCQueueAgent>();

            if (agent == null)
                continue;

            // Missed passengers remain overdue.
            if (IsInsideElevator(agent))
            {
                status.MarkOverdue();

                missedCount++;
                score -= missedExitPenalty;
            }
        }

        // NEW: Show a message and give
        // players time to prepare.
        string message;

        if (!unloadHasTargets)
        {
            message = "NO PASSENGERS TO EXIT";
        }
        else if (allExited)
        {
            message = "ALL PASSENGERS EXITED!";
        }
        else
        {
            message = "UNLOADING TIME UP!";
        }

        string nextPhase =
            CurrentFloor == 7
                ? "RESULTS"
                : "BOARDING";

        // A no-exit floor already showed a
        // short countdown. Use a shorter pause.
        float pause = !unloadHasTargets
            ? 1f
            : phaseTransitionDelay;

        yield return ShowPhaseBanner(
            message,
            nextPhase,
            pause
        );

        // Floor 7 ends after unloading.
        if (CurrentFloor == 7)
        {
            FinishGame();
            yield break;
        }

        // Begin the next phase AFTER the pause.
        StartBoarding();

        transitioning = false;
    }

    // =====================================
    // NEW: PHASE CHANGE PAUSE
    // =====================================

    private IEnumerator ShowPhaseBanner(
        string message,
        string nextPhase,
        float seconds)
    {
        phaseBannerText =
            message + "\n" +
            "NEXT: " + nextPhase;

        float duration = Mathf.Max(0f, seconds);

        phaseBannerEndTime =
            Time.realtimeSinceStartup + duration;

        phaseBannerActive = true;

        // Replace the old phase time while
        // players prepare for the next phase.
        if (countdown != null &&
            countdown.countdownText != null)
        {
            countdown.countdownText.text = "--";
        }

        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                duration
            );
        }

        phaseBannerActive = false;
        phaseBannerText = "";
    }

    // =====================================
    // FINISH BOARDING AND TRAVEL
    // =====================================

    private IEnumerator FinishBoardingAndTravel()
    {
        transitioning = true;

        countdown.StopCountdown();

        grabController.enabled = false;
        queueManager.enabled = false;

        cameraSwitcher.ShowElevator();

        FloorSetup current = floors[floorIndex];

        current.doorController.CloseDoors();

        yield return WaitForDoor(
            current.doorController,
            false
        );

        yield return WaitForPassengersToSettle();

        UpdatePassengerStates();

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

        MoveElevatorObjects(
            movement,
            passengers
        );

        queueManager.ConfigureForFloor(
            next.spawnPoint,
            next.queueStart,
            new HashSet<NPCQueueAgent>(passengers)
        );

        floorIndex++;

        Physics.SyncTransforms();

        yield return null;

        next.doorController.OpenDoors();

        yield return WaitForDoor(
            next.doorController,
            true
        );

        cameraSwitcher.ShowLobby();

        yield return FadeTo(0f);

        // Every floor after Floor 1
        // starts with unloading.
        StartUnloading();

        transitioning = false;
    }

    // =====================================
    // WAIT FOR PASSENGERS TO LAND
    // =====================================

    private IEnumerator WaitForPassengersToSettle()
    {
        float timeout = 2f;

        while (timeout > 0f)
        {
            bool lowering = false;

            NPCQueueAgent[] agents =
                FindObjectsByType<NPCQueueAgent>(
                    FindObjectsSortMode.None
                );

            foreach (NPCQueueAgent agent in agents)
            {
                if (agent != null && agent.IsLowering)
                {
                    lowering = true;
                    break;
                }
            }

            if (!lowering)
                yield break;

            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    // =====================================
    // CHECK INSIDE ELEVATOR
    // =====================================

    private bool IsInsideElevator(NPCQueueAgent agent)
    {
        if (agent == null || elevatorZone == null)
            return false;

        Vector3 sample = agent.transform.position;

        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

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

        Vector3 half =
            elevatorZone.size * 0.5f;

        return
            Mathf.Abs(local.x) <= half.x &&
            Mathf.Abs(local.y) <= half.y &&
            Mathf.Abs(local.z) <= half.z;
    }

    private List<NPCQueueAgent> CollectPassengers()
    {
        List<NPCQueueAgent> result =
            new List<NPCQueueAgent>();

        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent != null &&
                IsInsideElevator(agent))
            {
                result.Add(agent);
            }
        }

        return result;
    }

    // =====================================
    // WAIT FOR DOOR
    // =====================================

    private IEnumerator WaitForDoor(
        ElevatorDoorController door,
        bool waitForOpen)
    {
        if (door == null)
            yield break;

        float timeout = 12f;

        while (timeout > 0f)
        {
            if (door == null)
                yield break;

            bool complete = waitForOpen
                ? door.IsFullyOpen
                : door.IsFullyClosed;

            if (complete)
                yield break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning(
            "Elevator door timed out.",
            door
        );
    }

    // =====================================
    // MOVE ELEVATOR
    // =====================================

    private void MoveElevatorObjects(
        Vector3 movement,
        List<NPCQueueAgent> passengers)
    {
        HashSet<Transform> targets =
            new HashSet<Transform>();

        if (cameraSwitcher.cameraLobby != null)
        {
            targets.Add(
                cameraSwitcher.cameraLobby.transform
            );
        }

        if (cameraSwitcher.cameraElevator != null)
        {
            targets.Add(
                cameraSwitcher.cameraElevator.transform
            );
        }

        if (elevatorZone != null)
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
            if (target == null)
                continue;

            if (HasMovingAncestor(target, targets))
                continue;

            target.position += movement;
        }

        foreach (NPCQueueAgent agent in passengers)
        {
            if (agent == null)
                continue;

            Transform npc = agent.transform;

            if (npc == null)
                continue;

            if (!HasMovingAncestor(npc, targets) &&
                !targets.Contains(npc))
            {
                npc.position += movement;
            }
        }
    }

    private bool HasMovingAncestor(
        Transform child,
        HashSet<Transform> targets)
    {
        if (child == null)
            return false;

        Transform parent = child.parent;

        while (parent != null)
        {
            if (targets.Contains(parent))
                return true;

            parent = parent.parent;
        }

        return false;
    }

    // =====================================
    // BLACK FADE
    // =====================================

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (blackFade == null)
            yield break;

        blackFade.gameObject.SetActive(true);
        blackFade.blocksRaycasts = true;

        float startAlpha = blackFade.alpha;
        float duration = Mathf.Max(
            0.01f,
            fadeDuration
        );

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

    // =====================================
    // FLOOR HUD
    // =====================================

    private void UpdateFloorText()
    {
        if (currentFloorText == null)
            return;

        string state =
            phase == FloorPhase.Unloading
                ? "UNLOADING"
                : "BOARDING";

        currentFloorText.text =
            "FLOOR " + CurrentFloor + " / 7\n" +
            state + "\n" +
            "SCORE " + score;
    }

    // =====================================
    // TEMPORARY PHASE OVERLAY
    // =====================================

    private void OnGUI()
    {
        if (!showPhaseOverlay || finished ||
            countdown == null)
        {
            return;
        }

        if (phaseStyle == null)
        {
            phaseStyle = new GUIStyle(GUI.skin.label);
            phaseStyle.fontSize = 22;
            phaseStyle.fontStyle = FontStyle.Bold;
            phaseStyle.normal.textColor = Color.white;
            phaseStyle.wordWrap = true;
        }

        if (bannerStyle == null)
        {
            bannerStyle = new GUIStyle(GUI.skin.label);
            bannerStyle.fontSize = 26;
            bannerStyle.fontStyle = FontStyle.Bold;
            bannerStyle.normal.textColor = Color.white;
            bannerStyle.alignment =
                TextAnchor.MiddleCenter;
            bannerStyle.wordWrap = true;
        }

        // NEW: Show a large centered message
        // during the pause between phases.
        if (phaseBannerActive)
        {
            int secondsLeft = Mathf.Max(
                0,
                Mathf.CeilToInt(
                    phaseBannerEndTime -
                    Time.realtimeSinceStartup
                )
            );

            float width = 520f;
            float height = 180f;

            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.35f,
                width,
                height
            );

            GUI.Box(panel, GUIContent.none);

            GUI.Label(
                panel,
                phaseBannerText +
                "\n\n" +
                secondsLeft + "s",
                bannerStyle
            );

            return;
        }

        string phaseName;

        if (transitioning)
        {
            phaseName = "MOVING / CHANGING PHASE";
        }
        else if (phase == FloorPhase.Boarding)
        {
            phaseName = "BOARDING - LOAD NPCs";
        }
        else if (!unloadHasTargets)
        {
            phaseName =
                "UNLOADING - NO ONE TO EXIT";
        }
        else
        {
            phaseName =
                "UNLOADING - TAKE NPCs OUT";
        }

        string timeText = transitioning
            ? "--"
            : Mathf.CeilToInt(
                countdown.RemainingTime
            ).ToString();

        string text =
            "FLOOR " + CurrentFloor + " / 7\n" +
            phaseName + "\n" +
            "TIME: " + timeText + "s" +
            "    EXIT LEFT: " +
            (phase == FloorPhase.Unloading
                ? CountPendingExits()
                : 0) +
            "\nSCORE: " + score;

        GUI.Box(
            new Rect(15, 15, 455, 145),
            GUIContent.none
        );

        GUI.Label(
            new Rect(28, 22, 430, 135),
            text,
            phaseStyle
        );
    }

    // =====================================
    // FINISH GAME
    // =====================================

    private void FinishGame()
    {
        finished = true;
        transitioning = false;
        phaseBannerActive = false;

        countdown.StopCountdown();

        grabController.enabled = false;
        queueManager.enabled = false;
        grabController.CanGrabPassenger = null;

        if (finishText != null)
        {
            finishText.text =
                "GAME COMPLETE!\n" +
                "FLOOR 7 / 7\n\n" +
                "SCORE: " + score + "\n" +
                "BOARDED: " + boardedCount + "\n" +
                "DELIVERED: " + deliveredCount + "\n" +
                "MISSED: " + missedCount;
        }

        if (finishPanel != null)
            finishPanel.SetActive(true);

        Debug.Log(
            "Game Complete | Score: " + score
        );
    }
}
