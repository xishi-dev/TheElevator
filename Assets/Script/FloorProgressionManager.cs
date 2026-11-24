
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

    [Header("LATE Penalty - Per NPC")]
    [Min(0)]
    [SerializeField] private int firstMissPenalty = 200;

    [Min(0)]
    [SerializeField] private int secondMissPenalty = 350;

    [Min(0)]
    [SerializeField] private int additionalMissIncrease = 100;

    [Header("NEW - Combo Settings")]
    [Min(0)]
    [SerializeField] private int comboBonusStep = 25;

    [Min(0)]
    [SerializeField] private int maxComboBonus = 100;

    [Header("NEW - Score Feedback")]
    [SerializeField] private bool showScoreFeedback = true;

    [Min(0.5f)]
    [SerializeField] private float feedbackDuration = 2f;

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

    // NEW: Combo
    private int comboCount;
    private int maxCombo;

    private bool transitioning;
    private bool finished;
    private bool unloadHasTargets;

    private FloorPhase phase = FloorPhase.Boarding;

    private bool phaseBannerActive;
    private string phaseBannerText = "";
    private float phaseBannerEndTime;

    private GUIStyle phaseStyle;
    private GUIStyle bannerStyle;
    private GUIStyle scoreStyle;
    private GUIStyle comboStyle;

    // NEW: Score popups
    private class ScorePopup
    {
        public string message;
        public Color color;
        public float createdAt;
    }

    private readonly List<ScorePopup> scorePopups =
        new List<ScorePopup>();

    public int CurrentFloor => floorIndex + 1;
    public FloorPhase CurrentPhase => phase;
    public int Score => score;

    public int BoardedCount => boardedCount;
    public int DeliveredCount => deliveredCount;
    public int MissedCount => missedCount;

    public int ComboCount => comboCount;
    public int MaxCombo => maxCombo;

    // =====================================
    // EVENTS
    // =====================================

    private void OnEnable()
    {
        if (countdown != null)
        {
            countdown.OnCountdownFinished += OnTimeUp;
        }
    }

    private void OnDisable()
    {
        if (countdown != null)
        {
            countdown.OnCountdownFinished -= OnTimeUp;
        }

        if (grabController != null)
        {
            grabController.CanGrabPassenger = null;
        }
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

        comboCount = 0;
        maxCombo = 0;
        scorePopups.Clear();

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
        {
            finishPanel.SetActive(false);
        }

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
            {
                door.CloseDoors();
            }
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
                    "Missing Floor " + (i + 1) +
                    " references.",
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
        UpdateScorePopups();

        if (finished || transitioning)
        {
            return;
        }

        UpdatePassengerStates();

        if (phase == FloorPhase.Unloading &&
            unloadHasTargets &&
            CountPendingExits() == 0)
        {
            StartCoroutine(FinishUnloading());
        }
    }

    // =====================================
    // NEW: SCORE FEEDBACK POPUPS
    // =====================================

    private void AddScoreFeedback(
        string message,
        Color color)
    {
        if (!showScoreFeedback)
        {
            return;
        }

        scorePopups.Add(new ScorePopup
        {
            message = message,
            color = color,
            createdAt = Time.unscaledTime
        });

        // Prevent a huge popup list.
        while (scorePopups.Count > 5)
        {
            scorePopups.RemoveAt(0);
        }
    }

    private void UpdateScorePopups()
    {
        float duration =
            Mathf.Max(0.5f, feedbackDuration);

        for (int i = scorePopups.Count - 1;
             i >= 0;
             i--)
        {
            float age =
                Time.unscaledTime -
                scorePopups[i].createdAt;

            if (age >= duration)
            {
                scorePopups.RemoveAt(i);
            }
        }
    }

    // =====================================
    // NEW: COMBO SYSTEM
    // =====================================

    private int RegisterSuccessfulExit()
    {
        comboCount++;

        if (comboCount > maxCombo)
        {
            maxCombo = comboCount;
        }

        // 1st success: +0 bonus
        // 2nd success: +25 bonus
        // 3rd success: +50 bonus
        // 4th success: +75 bonus
        // 5th+ success: +100 bonus
        int bonus = (comboCount - 1) *
                    Mathf.Max(0, comboBonusStep);

        return Mathf.Min(
            bonus,
            Mathf.Max(0, maxComboBonus)
        );
    }

    private void ResetCombo()
    {
        if (comboCount > 0)
        {
            Debug.Log(
                "[COMBO] Lost combo x" + comboCount
            );

            AddScoreFeedback(
                "COMBO LOST!",
                new Color(1f, 0.35f, 0.2f)
            );
        }

        comboCount = 0;
    }

    // =====================================
    // PASSENGER STATES AND SCORE
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
            {
                continue;
            }

            bool inside = IsInsideElevator(agent);

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            if (status == null && inside)
            {
                status = agent.gameObject
                    .AddComponent<PassengerExitStatus>();
            }

            if (status == null)
            {
                continue;
            }

            // =================================
            // BOARDING SCORE
            // =================================

            if (phase == FloorPhase.Boarding)
            {
                if (inside &&
                    !agent.IsGrabbed &&
                    !agent.IsLowering &&
                    !status.HasBoarded)
                {
                    status.MarkBoarded();

                    boardedCount++;
                    score += boardingPoints;

                    AddScoreFeedback(
                        "+" + boardingPoints +
                        " BOARDING",
                        new Color(0.45f, 1f, 0.55f)
                    );

                    Debug.Log(
                        "[BOARDING] +" +
                        boardingPoints +
                        " : " + agent.name
                    );
                }
            }

            // =================================
            // DELIVERY SCORE + COMBO
            // =================================

            if (phase == FloorPhase.Unloading)
            {
                if (status.IsPending &&
                    !inside &&
                    !agent.IsGrabbed &&
                    !agent.IsLowering &&
                    !agent.IsInQueue)
                {
                    status.MarkDelivered();

                    deliveredCount++;

                    // NEW:
                    // Successful exits build Combo.
                    int comboBonus =
                        RegisterSuccessfulExit();

                    int earnedPoints =
                        unloadingPoints + comboBonus;

                    score += earnedPoints;

                    string popupText =
                        "+" + earnedPoints +
                        " EXIT";

                    if (comboCount >= 2)
                    {
                        popupText +=
                            "  COMBO x" + comboCount;
                    }

                    AddScoreFeedback(
                        popupText,
                        new Color(0.3f, 1f, 0.4f)
                    );

                    Debug.Log(
                        "[DELIVERED] " +
                        agent.name +
                        " | Base +" + unloadingPoints +
                        " | Combo +" + comboBonus +
                        " | Total +" + earnedPoints +
                        " | Combo x" + comboCount
                    );
                }
            }

            // No per-second LATE penalty.
            // Penalties are applied once
            // per NPC at phase end.
        }

        UpdateFloorText();
    }

    // =====================================
    // COUNT EXIT TARGETS
    // =====================================

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
            {
                count++;
            }
        }

        return count;
    }

    // =====================================
    // LATE PENALTY - PER NPC
    // =====================================

    private int GetMissCount(
        PassengerExitStatus status)
    {
        if (status == null)
        {
            return 1;
        }

        return Mathf.Max(
            1,
            CurrentFloor -
            status.RequestedFloor + 1
        );
    }

    private int CalculateMissPenalty(
        PassengerExitStatus status)
    {
        int missCount = GetMissCount(status);

        if (missCount == 1)
        {
            return firstMissPenalty;
        }

        if (missCount == 2)
        {
            return secondMissPenalty;
        }

        return secondMissPenalty +
               (missCount - 2) *
               additionalMissIncrease;
    }

    // =====================================
    // BOARDING PHASE
    // =====================================

    private void StartBoarding()
    {
        phase = FloorPhase.Boarding;
        unloadHasTargets = false;

        // Normal passengers can be grabbed.
        // LATE passengers are locked during
        // BOARDING only.
        grabController.CanGrabPassenger = agent =>
        {
            if (agent == null)
            {
                return false;
            }

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            if (status == null)
            {
                return true;
            }

            return !status.IsOverdue;
        };

        queueManager.enabled = true;
        grabController.enabled = true;

        float duration = Mathf.Max(
            1f,
            floors[floorIndex].roundSeconds
        );

        countdown.StartCountdown(duration);

        UpdateFloorText();

        Debug.Log(
            "[FLOOR " + CurrentFloor +
            "] BOARDING started"
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
            {
                continue;
            }

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            if (status == null)
            {
                status = agent.gameObject
                    .AddComponent<PassengerExitStatus>();
            }

            // Existing EXIT / LATE passengers
            // remain pending.
            if (status.IsPending)
            {
                continue;
            }

            if (status.State ==
                PassengerExitStatus.ExitState.Delivered)
            {
                continue;
            }

            bool mustExit =
                CurrentFloor == 7 ||
                Random.value < exitChance;

            if (mustExit)
            {
                status.RequestExit(CurrentFloor);
            }
        }

        // Only EXIT and LATE passengers
        // may be grabbed in UNLOADING.
        grabController.CanGrabPassenger = agent =>
        {
            if (agent == null)
            {
                return false;
            }

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            return status != null &&
                   status.IsPending;
        };

        unloadHasTargets =
            CountPendingExits() > 0;

        grabController.enabled = true;

        float duration = unloadHasTargets
            ? Mathf.Max(1f, unloadSeconds)
            : Mathf.Max(1f, noExitSeconds);

        countdown.StartCountdown(duration);

        UpdateFloorText();

        Debug.Log(
            "[FLOOR " + CurrentFloor +
            "] UNLOADING started | " +
            CountPendingExits() + " to exit"
        );
    }

    // =====================================
    // TIMER
    // =====================================

    private void OnTimeUp()
    {
        if (transitioning || finished)
        {
            return;
        }

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
        transitioning = true;

        countdown.StopCountdown();

        grabController.enabled = false;
        queueManager.enabled = false;

        yield return WaitForPassengersToSettle();

        // Count last successful deliveries.
        UpdatePassengerStates();

        bool allExited =
            unloadHasTargets &&
            CountPendingExits() == 0;

        int missedThisFloor = 0;

        PassengerExitStatus[] statuses =
            FindObjectsByType<PassengerExitStatus>(
                FindObjectsSortMode.None
            );

        // Apply penalties separately
        // to every missed NPC.
        foreach (PassengerExitStatus status in statuses)
        {
            if (status == null ||
                !status.IsPending)
            {
                continue;
            }

            NPCQueueAgent agent =
                status.GetComponent<NPCQueueAgent>();

            if (agent == null)
            {
                continue;
            }

            int missNumber =
                GetMissCount(status);

            int penalty =
                CalculateMissPenalty(status);

            // Individual deduction.
            score -= penalty;

            missedCount++;
            missedThisFloor++;

            status.MarkOverdue();

            // NEW: show each NPC penalty.
            AddScoreFeedback(
                "-" + penalty +
                " LATE (MISS " +
                missNumber + ")",
                new Color(1f, 0.3f, 0.3f)
            );

            Debug.Log(
                "[LATE NPC] " + agent.name +
                " | Miss #" + missNumber +
                " | -" + penalty +
                " | Score: " + score
            );
        }

        // NEW:
        // Any missed exit breaks the Combo.
        if (missedThisFloor > 0)
        {
            ResetCombo();
        }

        UpdateFloorText();

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
            message =
                "UNLOADING TIME UP!\n" +
                "MISSED: " + missedThisFloor +
                " NPCs\n" +
                "PENALTY APPLIED PER NPC";
        }

        string nextPhase =
            CurrentFloor == 7
                ? "RESULTS"
                : "BOARDING";

        float pause = !unloadHasTargets
            ? 1f
            : phaseTransitionDelay;

        yield return ShowPhaseBanner(
            message,
            nextPhase,
            pause
        );

        if (CurrentFloor == 7)
        {
            FinishGame();
            yield break;
        }

        StartBoarding();

        transitioning = false;
    }

    // =====================================
    // PHASE TRANSITION PAUSE
    // =====================================

    private IEnumerator ShowPhaseBanner(
        string message,
        string nextPhase,
        float seconds)
    {
        phaseBannerText =
            message + "\nNEXT: " + nextPhase;

        float duration =
            Mathf.Max(0f, seconds);

        phaseBannerEndTime =
            Time.realtimeSinceStartup +
            duration;

        phaseBannerActive = true;

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
    // BOARDING ENDS -> NEXT FLOOR
    // =====================================

    private IEnumerator FinishBoardingAndTravel()
    {
        transitioning = true;

        countdown.StopCountdown();

        grabController.enabled = false;
        queueManager.enabled = false;

        cameraSwitcher.ShowElevator();

        FloorSetup current =
            floors[floorIndex];

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

        FloorSetup next =
            floors[floorIndex + 1];

        Vector3 movement =
            next.stopPoint.position -
            current.stopPoint.position;

        MoveElevatorObjects(
            movement,
            passengers
        );

        // Preserve every NPC inside,
        // including LATE passengers.
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

        StartUnloading();

        transitioning = false;
    }

    // =====================================
    // WAIT FOR NPC TO LAND
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
                if (agent != null &&
                    agent.IsLowering)
                {
                    lowering = true;
                    break;
                }
            }

            if (!lowering)
            {
                yield break;
            }

            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    // =====================================
    // ELEVATOR ZONE
    // =====================================

    private bool IsInsideElevator(NPCQueueAgent agent)
    {
        if (agent == null ||
            elevatorZone == null)
        {
            return false;
        }

        Vector3 sample =
            agent.transform.position;

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
    // WAIT FOR ELEVATOR DOOR
    // =====================================

    private IEnumerator WaitForDoor(
        ElevatorDoorController door,
        bool waitForOpen)
    {
        if (door == null)
        {
            yield break;
        }

        float timeout = 12f;

        while (timeout > 0f)
        {
            if (door == null)
            {
                yield break;
            }

            bool complete = waitForOpen
                ? door.IsFullyOpen
                : door.IsFullyClosed;

            if (complete)
            {
                yield break;
            }

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
        {
            targets.Add(elevatorZone.transform);
        }

        if (elevatorMoveDirection != null)
        {
            targets.Add(elevatorMoveDirection);
        }

        if (extraMovingObjects != null)
        {
            foreach (Transform extra in extraMovingObjects)
            {
                if (extra != null)
                {
                    targets.Add(extra);
                }
            }
        }

        foreach (Transform target in targets)
        {
            if (target == null)
            {
                continue;
            }

            if (HasMovingAncestor(
                target,
                targets
            ))
            {
                continue;
            }

            target.position += movement;
        }

        foreach (NPCQueueAgent agent in passengers)
        {
            if (agent == null)
            {
                continue;
            }

            Transform npc = agent.transform;

            if (npc == null)
            {
                continue;
            }

            if (!HasMovingAncestor(
                npc,
                targets
            ) &&
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
        {
            return false;
        }

        Transform parent = child.parent;

        while (parent != null)
        {
            if (targets.Contains(parent))
            {
                return true;
            }

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
        {
            yield break;
        }

        blackFade.gameObject.SetActive(true);
        blackFade.blocksRaycasts = true;

        float startAlpha = blackFade.alpha;
        float duration =
            Mathf.Max(0.01f, fadeDuration);

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
    // FLOOR TEXT
    // =====================================

    private void UpdateFloorText()
    {
        if (currentFloorText == null)
        {
            return;
        }

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
    // GUI SETUP
    // =====================================

    private void EnsureGUIStyles()
    {
        if (phaseStyle == null)
        {
            phaseStyle =
                new GUIStyle(GUI.skin.label);

            phaseStyle.fontSize = 22;
            phaseStyle.fontStyle = FontStyle.Bold;
            phaseStyle.normal.textColor = Color.white;
            phaseStyle.wordWrap = true;
        }

        if (bannerStyle == null)
        {
            bannerStyle =
                new GUIStyle(GUI.skin.label);

            bannerStyle.fontSize = 26;
            bannerStyle.fontStyle = FontStyle.Bold;
            bannerStyle.normal.textColor = Color.white;
            bannerStyle.alignment =
                TextAnchor.MiddleCenter;
            bannerStyle.wordWrap = true;
        }

        if (scoreStyle == null)
        {
            scoreStyle =
                new GUIStyle(GUI.skin.label);

            scoreStyle.fontSize = 20;
            scoreStyle.fontStyle = FontStyle.Bold;
            scoreStyle.normal.textColor = Color.white;
            scoreStyle.alignment =
                TextAnchor.MiddleCenter;
            scoreStyle.wordWrap = false;
        }

        if (comboStyle == null)
        {
            comboStyle =
                new GUIStyle(GUI.skin.label);

            comboStyle.fontSize = 21;
            comboStyle.fontStyle = FontStyle.Bold;
            comboStyle.normal.textColor = Color.white;
            comboStyle.alignment =
                TextAnchor.MiddleCenter;
        }
    }

    // =====================================
    // NEW: DRAW SCORE FEEDBACK
    // =====================================

    private void DrawScoreFeedback()
    {
        if (!showScoreFeedback)
        {
            return;
        }

        EnsureGUIStyles();

        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;

        GUI.depth = -80;

        float width = Mathf.Min(
            340f,
            Mathf.Max(180f, Screen.width - 24f)
        );

        float x = Screen.width - width - 12f;

        // COMBO indicator.
        if (comboCount > 0)
        {
            Rect comboRect = new Rect(
                x,
                14f,
                width,
                52f
            );

            GUI.color = new Color(
                0.05f, 0.07f, 0.09f, 0.85f
            );

            GUI.DrawTexture(
                comboRect,
                Texture2D.whiteTexture
            );

            GUI.color = new Color(
                1f, 0.85f, 0.35f
            );

            GUI.Label(
                comboRect,
                "COMBO x" + comboCount,
                comboStyle
            );
        }

        // Floating score messages.
        for (int i = 0; i < scorePopups.Count; i++)
        {
            ScorePopup popup = scorePopups[i];

            float age =
                Time.unscaledTime - popup.createdAt;

            float progress = Mathf.Clamp01(
                age / Mathf.Max(
                    0.5f, feedbackDuration
                )
            );

            float alpha = 1f - progress;

            float y =
                Screen.height -
                24f -
                (scorePopups.Count - i) * 48f -
                progress * 18f;

            Rect popupRect = new Rect(
                x,
                y,
                width,
                42f
            );

            // Dark background.
            GUI.color = new Color(
                0.02f,
                0.025f,
                0.035f,
                0.78f * alpha
            );

            GUI.DrawTexture(
                popupRect,
                Texture2D.whiteTexture
            );

            // Colored message.
            GUI.color = new Color(
                popup.color.r,
                popup.color.g,
                popup.color.b,
                alpha
            );

            GUI.Label(
                popupRect,
                popup.message,
                scoreStyle
            );
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    // =====================================
    // TEMPORARY PHASE HUD
    // =====================================

    private void OnGUI()
    {
        if (finished || countdown == null)
        {
            return;
        }

        EnsureGUIStyles();

        if (showPhaseOverlay)
        {
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

                GUI.Box(
                    panel,
                    GUIContent.none
                );

                GUI.Label(
                    panel,
                    phaseBannerText +
                    "\n\n" +
                    secondsLeft + "s",
                    bannerStyle
                );
            }
            else
            {
                string phaseName;

                if (transitioning)
                {
                    phaseName =
                        "MOVING / CHANGING PHASE";
                }
                else if (phase == FloorPhase.Boarding)
                {
                    phaseName =
                        "BOARDING - LOAD NPCs";
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
        }

        // Score feedback is independent
        // from the temporary phase HUD.
        DrawScoreFeedback();
    }

    // =====================================
    // GAME COMPLETE
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
                "MISSED: " + missedCount + "\n" +
                "MAX COMBO: x" + maxCombo;
        }

        if (finishPanel != null)
        {
            finishPanel.SetActive(true);
        }

        Debug.Log(
            "Game Complete | Score: " +
            score + " | Max Combo: x" +
            maxCombo
        );
    }
}
